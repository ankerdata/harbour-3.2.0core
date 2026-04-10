/*
 * Harbour Transpiler - User function signature table + AST scanner
 *
 * See include/hbreftab.h for the API and rationale.
 *
 * Copyright 2026 harbour.github.io
 */

#include "hbcomp.h"
#include "hbast.h"
#include "hbreftab.h"

#define HB_REFTAB_BUCKETS  1024  /* power of two */
#define HB_REFTAB_MAXPARAM 64    /* hard cap; matches the by-ref bitmap width */

typedef struct HB_REFENTRY_
{
   char *                szName;       /* lowercased name (owned) */
   char *                szReturnType; /* inferred return type, or NULL (owned) */
   int                   nParams;      /* declared parameter count, -1 = unknown */
   HB_BOOL               fVariadic;    /* function uses PCount/HB_PValue */
   HB_BOOL               fDefined;     /* set once a real definition has been seen */
   HB_BOOL               fIsClass;     /* this is a CLASS marker, not a function */
   HB_REFPARAM *         pParams;      /* nParams entries (NULL if unknown) */
   HB_U64                bitmap;       /* by-ref bitmap, bit n = arg n is by-ref */
   HB_U64                nilbits;      /* nilable bitmap, bit n = slot n is nilable */
   struct HB_REFENTRY_ * pNext;
} HB_REFENTRY, * PHB_REFENTRY;

struct HB_REFTAB_
{
   PHB_REFENTRY buckets[ HB_REFTAB_BUCKETS ];
   HB_SIZE      nCount;
};

/* ---- runtime path override ----
   Set by --reftab=<path> on the command line so transpiler runs over
   foreign codebases can keep their signature table out of the
   harbour-core test tree. */
static char s_szRefTabPathOverride[ HB_PATH_MAX ] = { 0 };

void hb_refTabSetPath( const char * szPath )
{
   if( ! szPath || ! *szPath )
   {
      s_szRefTabPathOverride[ 0 ] = '\0';
      return;
   }
   hb_strncpy( s_szRefTabPathOverride, szPath,
               sizeof( s_szRefTabPathOverride ) - 1 );
}

const char * hb_refTabGetPath( void )
{
   return s_szRefTabPathOverride[ 0 ] ? s_szRefTabPathOverride
                                      : HB_REFTAB_PATH;
}

/* ---- string helpers (case-folded) ---- */

static HB_SIZE hb_refTabHash( const char * sz )
{
   /* FNV-1a 64-bit, ASCII case-folded */
   HB_U64 h = 0xcbf29ce484222325ULL;
   while( *sz )
   {
      char c = *sz++;
      if( c >= 'A' && c <= 'Z' )
         c = ( char ) ( c + ( 'a' - 'A' ) );
      h ^= ( unsigned char ) c;
      h *= 0x100000001b3ULL;
   }
   return ( HB_SIZE ) h;
}

static char * hb_refTabDupLower( const char * sz )
{
   HB_SIZE n = strlen( sz );
   char *  r = ( char * ) hb_xgrab( n + 1 );
   HB_SIZE i;
   for( i = 0; i < n; i++ )
   {
      char c = sz[ i ];
      if( c >= 'A' && c <= 'Z' )
         c = ( char ) ( c + ( 'a' - 'A' ) );
      r[ i ] = c;
   }
   r[ n ] = '\0';
   return r;
}

static char * hb_refTabDup( const char * sz )
{
   HB_SIZE n = strlen( sz );
   char *  r = ( char * ) hb_xgrab( n + 1 );
   memcpy( r, sz, n + 1 );
   return r;
}

static PHB_REFENTRY hb_refTabFindEntry( PHB_REFTAB pTab, const char * szName,
                                        HB_SIZE * pSlot )
{
   HB_SIZE      slot = hb_refTabHash( szName ) & ( HB_REFTAB_BUCKETS - 1 );
   PHB_REFENTRY e    = pTab->buckets[ slot ];

   if( pSlot )
      *pSlot = slot;
   while( e )
   {
      if( hb_stricmp( e->szName, szName ) == 0 )
         return e;
      e = e->pNext;
   }
   return NULL;
}

static PHB_REFENTRY hb_refTabFindOrCreate( PHB_REFTAB pTab, const char * szName )
{
   HB_SIZE      slot;
   PHB_REFENTRY e = hb_refTabFindEntry( pTab, szName, &slot );

   if( ! e )
   {
      e            = ( PHB_REFENTRY ) hb_xgrabz( sizeof( HB_REFENTRY ) );
      e->szName    = hb_refTabDupLower( szName );
      e->nParams   = -1;            /* unknown until a definition is registered */
      e->pNext     = pTab->buckets[ slot ];
      pTab->buckets[ slot ] = e;
      pTab->nCount++;
   }
   return e;
}

static void hb_refEntryFreeParams( PHB_REFENTRY e )
{
   if( e->pParams )
   {
      int i;
      for( i = 0; i < e->nParams; i++ )
      {
         if( e->pParams[ i ].szName )
            hb_xfree( e->pParams[ i ].szName );
         if( e->pParams[ i ].szType )
            hb_xfree( e->pParams[ i ].szType );
      }
      hb_xfree( e->pParams );
      e->pParams = NULL;
   }
}

/* ---- public table API ---- */

PHB_REFTAB hb_refTabNew( void )
{
   return ( PHB_REFTAB ) hb_xgrabz( sizeof( HB_REFTAB ) );
}

const char * hb_refTabMethodKey( const char * szClass, const char * szMember )
{
   /* Static buffer is fine here: every caller uses the result
      immediately to look up or insert into the table, never holding
      it across other hb_refTabMethodKey calls. The hash table
      duplicates the key on insertion so the buffer's lifetime is
      irrelevant beyond the immediate call. */
   static char s_szBuf[ 256 ];

   if( ! szMember )
      return NULL;
   if( ! szClass || ! *szClass )
      return szMember;

   hb_snprintf( s_szBuf, sizeof( s_szBuf ), "%s::%s", szClass, szMember );
   return s_szBuf;
}

void hb_refTabFree( PHB_REFTAB pTab )
{
   HB_SIZE i;

   if( ! pTab )
      return;
   for( i = 0; i < HB_REFTAB_BUCKETS; i++ )
   {
      PHB_REFENTRY e = pTab->buckets[ i ];
      while( e )
      {
         PHB_REFENTRY pNext = e->pNext;
         hb_refEntryFreeParams( e );
         if( e->szReturnType )
            hb_xfree( e->szReturnType );
         hb_xfree( e->szName );
         hb_xfree( e );
         e = pNext;
      }
   }
   hb_xfree( pTab );
}

void hb_refTabAddFunc( PHB_REFTAB    pTab,
                       const char *  szName,
                       int           nParams,
                       const char ** ppNames,
                       const char ** ppTypes,
                       HB_BOOL       fVariadic )
{
   PHB_REFENTRY e;
   int          i;

   if( ! pTab || ! szName || nParams < 0 || nParams > HB_REFTAB_MAXPARAM )
      return;

   e = hb_refTabFindOrCreate( pTab, szName );

   /* Preserve any refined per-slot types from a prior registration
      (typically loaded from disk or refined by an earlier scan in
      this session) when the newly-supplied types are USUAL. The
      intra-function inferencer only knows Hungarian-prefix types;
      anything more specific must have come from call-site analysis
      and should not be clobbered. */
   {
      HB_REFPARAM * pOld    = e->pParams;
      int            nOld   = e->nParams;
      HB_BOOL        fOldConflict[ HB_REFTAB_MAXPARAM ];

      /* Remember the conflict bits before we free pOld. */
      for( i = 0; i < HB_REFTAB_MAXPARAM; i++ )
         fOldConflict[ i ] = HB_FALSE;
      if( pOld )
      {
         for( i = 0; i < nOld && i < HB_REFTAB_MAXPARAM; i++ )
            fOldConflict[ i ] = pOld[ i ].fConflict;
      }

      e->nParams   = nParams;
      e->fVariadic = fVariadic;
      e->fDefined  = HB_TRUE;
      if( nParams > 0 )
         e->pParams = ( HB_REFPARAM * ) hb_xgrabz( sizeof( HB_REFPARAM ) * nParams );
      else
         e->pParams = NULL;

      for( i = 0; i < nParams; i++ )
      {
         const char * szPName = ppNames ? ppNames[ i ] : NULL;
         const char * szPType = ppTypes ? ppTypes[ i ] : NULL;
         const char * szOldType = NULL;

         if( pOld && i < nOld )
            szOldType = pOld[ i ].szType;

         e->pParams[ i ].szName = hb_refTabDup( szPName ? szPName : "" );

         /* Prefer the prior refined type over a fresh USUAL. */
         if( ( ! szPType || ! *szPType || hb_stricmp( szPType, "USUAL" ) == 0 ) &&
             szOldType && hb_stricmp( szOldType, "USUAL" ) != 0 )
            e->pParams[ i ].szType = hb_refTabDup( szOldType );
         else
            e->pParams[ i ].szType = hb_refTabDup( szPType ? szPType : "USUAL" );

         /* Carry forward any byref / nilable / conflict flags. */
         e->pParams[ i ].fByRef =
            ( e->bitmap  & ( ( ( HB_U64 ) 1 ) << i ) ) != 0;
         e->pParams[ i ].fNilable =
            ( e->nilbits & ( ( ( HB_U64 ) 1 ) << i ) ) != 0;
         e->pParams[ i ].fConflict = fOldConflict[ i ];
      }

      /* Free the old parameter array now that we've pulled what we
         needed from it. */
      if( pOld )
      {
         int k;
         for( k = 0; k < nOld; k++ )
         {
            if( pOld[ k ].szName ) hb_xfree( pOld[ k ].szName );
            if( pOld[ k ].szType ) hb_xfree( pOld[ k ].szType );
         }
         hb_xfree( pOld );
      }
   }
}

void hb_refTabMark( PHB_REFTAB pTab, const char * szFunc, int iPos )
{
   PHB_REFENTRY e;

   if( ! pTab || ! szFunc || iPos < 0 || iPos >= HB_REFTAB_MAXPARAM )
      return;

   e = hb_refTabFindOrCreate( pTab, szFunc );
   e->bitmap |= ( ( HB_U64 ) 1 ) << iPos;
   /* If we already have the parameter list, sync the per-slot flag too */
   if( e->pParams && iPos < e->nParams )
      e->pParams[ iPos ].fByRef = HB_TRUE;
}

void hb_refTabSetNilable( PHB_REFTAB pTab, const char * szFunc, int iPos )
{
   PHB_REFENTRY e;

   if( ! pTab || ! szFunc || iPos < 0 || iPos >= HB_REFTAB_MAXPARAM )
      return;

   e = hb_refTabFindOrCreate( pTab, szFunc );
   e->nilbits |= ( ( HB_U64 ) 1 ) << iPos;
   if( e->pParams && iPos < e->nParams )
      e->pParams[ iPos ].fNilable = HB_TRUE;
}

void hb_refTabSetReturnType( PHB_REFTAB pTab, const char * szFunc,
                             const char * szRetType )
{
   PHB_REFENTRY e;

   if( ! pTab || ! szFunc )
      return;

   e = hb_refTabFindOrCreate( pTab, szFunc );
   if( e->szReturnType )
   {
      hb_xfree( e->szReturnType );
      e->szReturnType = NULL;
   }
   if( szRetType && *szRetType )
      e->szReturnType = hb_refTabDup( szRetType );
}

HB_REFINE_RESULT hb_refTabRefineParamType( PHB_REFTAB pTab,
                                           const char * szFunc,
                                           int iPos,
                                           const char * szNewType )
{
   PHB_REFENTRY e;
   HB_REFPARAM * pParam;

   if( ! pTab || ! szFunc || iPos < 0 || iPos >= HB_REFTAB_MAXPARAM )
      return HB_REFINE_OK;
   if( ! szNewType || ! *szNewType || hb_stricmp( szNewType, "USUAL" ) == 0 )
      return HB_REFINE_OK;

   /* We only refine functions that have been formally registered with
      hb_refTabAddFunc. A stub entry created by a by-ref mark has no
      pParams array and no business being type-refined. */
   e = hb_refTabFindEntry( pTab, szFunc, NULL );
   if( ! e || ! e->pParams || iPos >= e->nParams )
      return HB_REFINE_OK;

   pParam = &e->pParams[ iPos ];

   if( pParam->fConflict )
      return HB_REFINE_ALREADY_CONFLICT;

   if( ! pParam->szType || hb_stricmp( pParam->szType, "USUAL" ) == 0 )
   {
      /* Fresh slot — adopt the new type. */
      if( pParam->szType )
         hb_xfree( pParam->szType );
      pParam->szType = hb_refTabDup( szNewType );
      return HB_REFINE_REFINED;
   }

   if( hb_stricmp( pParam->szType, szNewType ) == 0 )
      return HB_REFINE_OK;   /* agreement */

   /* Genuine disagreement: freeze the slot as USUAL with fConflict set
      so later refinements stop trying. The old type is discarded. */
   hb_xfree( pParam->szType );
   pParam->szType    = hb_refTabDup( "USUAL" );
   pParam->fConflict = HB_TRUE;
   return HB_REFINE_CONFLICT;
}

HB_BOOL hb_refTabIsRef( PHB_REFTAB pTab, const char * szFunc, int iPos )
{
   PHB_REFENTRY e;

   if( ! pTab || ! szFunc || iPos < 0 || iPos >= HB_REFTAB_MAXPARAM )
      return HB_FALSE;
   e = hb_refTabFindEntry( pTab, szFunc, NULL );
   if( ! e )
      return HB_FALSE;
   return ( e->bitmap & ( ( ( HB_U64 ) 1 ) << iPos ) ) != 0;
}

HB_BOOL hb_refTabIsNilable( PHB_REFTAB pTab, const char * szFunc, int iPos )
{
   PHB_REFENTRY e;

   if( ! pTab || ! szFunc || iPos < 0 || iPos >= HB_REFTAB_MAXPARAM )
      return HB_FALSE;
   e = hb_refTabFindEntry( pTab, szFunc, NULL );
   if( ! e )
      return HB_FALSE;
   return ( e->nilbits & ( ( ( HB_U64 ) 1 ) << iPos ) ) != 0;
}

void hb_refTabMarkClass( PHB_REFTAB pTab, const char * szName )
{
   PHB_REFENTRY e;
   if( ! pTab || ! szName )
      return;
   e = hb_refTabFindOrCreate( pTab, szName );
   e->fIsClass = HB_TRUE;
   e->fDefined = HB_TRUE;     /* persist across saves */
   if( e->nParams < 0 )
      e->nParams = 0;          /* class stubs have no params */
}

HB_BOOL hb_refTabIsClass( PHB_REFTAB pTab, const char * szName )
{
   PHB_REFENTRY e;
   if( ! pTab || ! szName )
      return HB_FALSE;
   e = hb_refTabFindEntry( pTab, szName, NULL );
   return e && e->fIsClass;
}

const char * hb_refTabReturnType( PHB_REFTAB pTab, const char * szFunc )
{
   PHB_REFENTRY e;
   if( ! pTab || ! szFunc )
      return NULL;
   e = hb_refTabFindEntry( pTab, szFunc, NULL );
   return e ? e->szReturnType : NULL;
}

int hb_refTabParamCount( PHB_REFTAB pTab, const char * szFunc )
{
   PHB_REFENTRY e;
   if( ! pTab || ! szFunc )
      return -1;
   e = hb_refTabFindEntry( pTab, szFunc, NULL );
   return e ? e->nParams : -1;
}

HB_BOOL hb_refTabIsVariadic( PHB_REFTAB pTab, const char * szFunc )
{
   PHB_REFENTRY e;
   if( ! pTab || ! szFunc )
      return HB_FALSE;
   e = hb_refTabFindEntry( pTab, szFunc, NULL );
   return e ? e->fVariadic : HB_FALSE;
}

const HB_REFPARAM * hb_refTabParam( PHB_REFTAB pTab,
                                    const char * szFunc, int iPos )
{
   PHB_REFENTRY e;
   if( ! pTab || ! szFunc || iPos < 0 )
      return NULL;
   e = hb_refTabFindEntry( pTab, szFunc, NULL );
   if( ! e || ! e->pParams || iPos >= e->nParams )
      return NULL;
   return &e->pParams[ iPos ];
}

/* ---- persistence ---- */

HB_BOOL hb_refTabSave( PHB_REFTAB pTab, const char * szPath )
{
   FILE *  fp;
   HB_SIZE i;

   if( ! pTab || ! szPath )
      return HB_FALSE;
   fp = hb_fopen( szPath, "w" );
   if( ! fp )
      return HB_FALSE;
   fprintf( fp, "# Harbour transpiler user-function signature table\n" );
   fprintf( fp, "# Format: NAME<TAB>FLAGS<TAB>RETTYPE<TAB>NPARAMS<TAB>PARAM_1<TAB>...\n" );
   fprintf( fp, "# FLAGS:    V = variadic, - = none\n" );
   fprintf( fp, "# RETTYPE:  inferred return type, or - if unknown\n" );
   fprintf( fp, "# PARAM:    name:type:pflags\n" );
   fprintf( fp, "# pflags letters:  R = byref, N = nilable, C = conflict, - = none\n" );
   fprintf( fp, "#\n" );
   fprintf( fp, "# THIS FILE IS GENERATED — see `hbtranspiler -GF`\n" );

   for( i = 0; i < HB_REFTAB_BUCKETS; i++ )
   {
      PHB_REFENTRY e = pTab->buckets[ i ];
      while( e )
      {
         /* Skip stub entries that only have flags and no registered
            definition — they would round-trip as zero-arg functions,
            which is wrong. The flags are still useful in the live
            in-memory table during a single transpile, but they
            shouldn't be persisted without a definition. */
         if( e->fDefined )
         {
            int p;
            char fnFlags[ 4 ];
            int  fnK = 0;
            if( e->fVariadic ) fnFlags[ fnK++ ] = 'V';
            if( e->fIsClass )  fnFlags[ fnK++ ] = 'K';
            if( fnK == 0 )     fnFlags[ fnK++ ] = '-';
            fnFlags[ fnK ] = '\0';
            fprintf( fp, "%s\t%s\t%s\t%d",
                     e->szName,
                     fnFlags,
                     e->szReturnType ? e->szReturnType : "-",
                     e->nParams );
            for( p = 0; p < e->nParams; p++ )
            {
               HB_BOOL fByRef =
                  ( e->bitmap  & ( ( ( HB_U64 ) 1 ) << p ) ) != 0;
               HB_BOOL fNilable =
                  ( e->nilbits & ( ( ( HB_U64 ) 1 ) << p ) ) != 0;
               HB_BOOL fConflict = e->pParams[ p ].fConflict;
               char flags[ 5 ];
               int  k = 0;
               if( fByRef )    flags[ k++ ] = 'R';
               if( fNilable )  flags[ k++ ] = 'N';
               if( fConflict ) flags[ k++ ] = 'C';
               if( k == 0 )    flags[ k++ ] = '-';
               flags[ k ] = '\0';
               fprintf( fp, "\t%s:%s:%s",
                        e->pParams[ p ].szName,
                        e->pParams[ p ].szType,
                        flags );
            }
            fprintf( fp, "\n" );
         }
         e = e->pNext;
      }
   }
   fclose( fp );
   return HB_TRUE;
}

/* Helper for the loader: split a parameter "name:type:flags" triple in
   place. Writes pointers into the line; flags is a (possibly empty)
   string of letter codes. Returns 1 on success. */
static int hb_refTabParseParam( char * sz, char ** ppName,
                                char ** ppType, char ** ppFlags )
{
   char * c1 = strchr( sz, ':' );
   char * c2;
   if( ! c1 )
      return 0;
   *c1 = '\0';
   c2 = strchr( c1 + 1, ':' );
   if( ! c2 )
      return 0;
   *c2 = '\0';
   *ppName  = sz;
   *ppType  = c1 + 1;
   *ppFlags = c2 + 1;
   return 1;
}

HB_BOOL hb_refTabLoad( PHB_REFTAB pTab, const char * szPath )
{
   FILE * fp;
   char   line[ 4096 ];

   if( ! pTab || ! szPath )
      return HB_FALSE;
   fp = hb_fopen( szPath, "r" );
   if( ! fp )
      return HB_FALSE;

   while( fgets( line, sizeof( line ), fp ) )
   {
      char * fields[ 4 + HB_REFTAB_MAXPARAM ];
      int    nFields = 0;
      char * p;
      HB_SIZE n;
      int    nParams;
      HB_BOOL fVariadic;
      const char * names[ HB_REFTAB_MAXPARAM ];
      const char * types[ HB_REFTAB_MAXPARAM ];
      HB_BOOL refs[ HB_REFTAB_MAXPARAM ];
      HB_BOOL nils[ HB_REFTAB_MAXPARAM ];
      HB_BOOL cons[ HB_REFTAB_MAXPARAM ];
      int i;

      if( line[ 0 ] == '#' || line[ 0 ] == '\n' || line[ 0 ] == '\0' )
         continue;
      /* Strip trailing newline */
      n = strlen( line );
      while( n > 0 && ( line[ n - 1 ] == '\n' || line[ n - 1 ] == '\r' ) )
         line[ --n ] = '\0';
      if( n == 0 )
         continue;

      /* Tokenise on tabs in place */
      fields[ nFields++ ] = line;
      for( p = line; *p; p++ )
      {
         if( *p == '\t' )
         {
            *p = '\0';
            if( nFields < ( int ) ( sizeof( fields ) / sizeof( fields[ 0 ] ) ) )
               fields[ nFields++ ] = p + 1;
         }
      }

      if( nFields < 4 )
         continue;
      {
         /* FLAGS is a string of letters: V (variadic), K (klass), - */
         char * c;
         HB_BOOL fIsClass = HB_FALSE;
         fVariadic = HB_FALSE;
         for( c = fields[ 1 ]; *c; c++ )
         {
            if( *c == 'V' || *c == 'v' )
               fVariadic = HB_TRUE;
            else if( *c == 'K' || *c == 'k' )
               fIsClass = HB_TRUE;
         }
         if( fIsClass )
            hb_refTabMarkClass( pTab, fields[ 0 ] );
      }
      /* fields[2] is RETTYPE, possibly "-" */
      nParams = atoi( fields[ 3 ] );
      if( nParams < 0 || nParams > HB_REFTAB_MAXPARAM )
         continue;
      if( nFields < 4 + nParams )
         continue;

      for( i = 0; i < nParams; i++ )
      {
         char * pn;
         char * pt;
         char * pf;
         char * c;
         if( ! hb_refTabParseParam( fields[ 4 + i ], &pn, &pt, &pf ) )
         {
            pn = ( char * ) "";
            pt = ( char * ) "USUAL";
            pf = ( char * ) "-";
         }
         names[ i ] = pn;
         types[ i ] = pt;
         refs[ i ]  = HB_FALSE;
         nils[ i ]  = HB_FALSE;
         cons[ i ]  = HB_FALSE;
         for( c = pf; *c; c++ )
         {
            if( *c == 'R' || *c == 'r' )
               refs[ i ] = HB_TRUE;
            else if( *c == 'N' || *c == 'n' )
               nils[ i ] = HB_TRUE;
            else if( *c == 'C' || *c == 'c' )
               cons[ i ] = HB_TRUE;
         }
      }

      hb_refTabAddFunc( pTab, fields[ 0 ], nParams, names, types, fVariadic );
      if( fields[ 2 ][ 0 ] && strcmp( fields[ 2 ], "-" ) != 0 )
         hb_refTabSetReturnType( pTab, fields[ 0 ], fields[ 2 ] );
      for( i = 0; i < nParams; i++ )
      {
         if( refs[ i ] )
            hb_refTabMark( pTab, fields[ 0 ], i );
         if( nils[ i ] )
            hb_refTabSetNilable( pTab, fields[ 0 ], i );
      }
      /* Rehydrate conflict flags by reaching into the entry directly;
         there is no public setter because conflicts are meant to be
         discovered during scan, not asserted externally. */
      if( nParams > 0 )
      {
         PHB_REFENTRY pe = hb_refTabFindEntry( pTab, fields[ 0 ], NULL );
         if( pe && pe->pParams )
         {
            for( i = 0; i < nParams && i < pe->nParams; i++ )
               if( cons[ i ] )
                  pe->pParams[ i ].fConflict = HB_TRUE;
         }
      }
   }

   fclose( fp );
   return HB_TRUE;
}

/* ================================================================
 * AST scanner
 *
 * Three responsibilities:
 *   1. Register every function definition we see (name + params).
 *   2. Record by-ref usage seen at every call site, and detect
 *      variadic functions by spotting calls to PCount/HB_PValue/etc.
 *   3. Detect parameters that the function body compares to NIL or
 *      assigns NIL to, and mark them nilable.
 *
 * (2) and (3) need to know which function body we're currently inside.
 * The HB_SCANCTX struct carries that "current function" state down
 * through the recursive scan; it is NULL when scanning top-level code
 * outside any function (e.g. CLASS bodies in the auto-generated
 * startup wrapper).
 * ================================================================ */

typedef struct
{
   const char *  szFunc;          /* enclosing function name (already keyed) */
   const char *  szClass;         /* enclosing class, NULL for free function */
   const char ** ppParamNames;    /* parameter names of the enclosing func */
   int           nParams;         /* parameter count */
   HB_BOOL       fVariadic;       /* set if PCount/HB_PValue is called */
} HB_SCANCTX;

static void hb_refTabScanExpr( PHB_REFTAB pTab, PHB_EXPR pExpr,
                               HB_SCANCTX * pCtx );
static void hb_refTabScanStmt( PHB_REFTAB pTab, PHB_AST_NODE pStmt,
                               HB_SCANCTX * pCtx );

/* If szName matches a parameter of the enclosing function, mark that
   slot nilable. No-op if there is no enclosing function or the name
   isn't a parameter. */
static void hb_refTabMaybeMarkNilable( PHB_REFTAB pTab, HB_SCANCTX * pCtx,
                                       const char * szName )
{
   int i;
   if( ! pCtx || ! pCtx->szFunc || ! szName )
      return;
   for( i = 0; i < pCtx->nParams; i++ )
   {
      if( pCtx->ppParamNames[ i ] &&
          hb_stricmp( pCtx->ppParamNames[ i ], szName ) == 0 )
      {
         hb_refTabSetNilable( pTab, pCtx->szFunc, i );
         return;
      }
   }
}

/* Inspect an expression for NIL-comparison or NIL-assignment patterns
   targeting a parameter of the enclosing function. Called for the
   condition of IFs/WHILEs and the contents of statement expressions. */
static void hb_refTabCheckNilPattern( PHB_REFTAB pTab, HB_SCANCTX * pCtx,
                                      PHB_EXPR pExpr )
{
   if( ! pExpr || ! pCtx )
      return;

   /* IF/WHILE conditions arrive wrapped in HB_ET_LIST (the parser
      stores the condition as a single-element list). Peel any such
      wrappers off before pattern-matching. */
   while( ( pExpr->ExprType == HB_ET_LIST ||
            pExpr->ExprType == HB_ET_ARGLIST ) &&
          pExpr->value.asList.pExprList &&
          ! pExpr->value.asList.pExprList->pNext )
      pExpr = pExpr->value.asList.pExprList;

   switch( pExpr->ExprType )
   {
      /* Comparison to NIL: param == NIL, param != NIL, param = NIL,
         and the symmetric NIL == param etc. */
      case HB_EO_EQ:
      case HB_EO_EQUAL:
      case HB_EO_NE:
      {
         PHB_EXPR pL = pExpr->value.asOperator.pLeft;
         PHB_EXPR pR = pExpr->value.asOperator.pRight;
         if( pL && pR )
         {
            if( pL->ExprType == HB_ET_VARIABLE && pR->ExprType == HB_ET_NIL )
               hb_refTabMaybeMarkNilable( pTab, pCtx,
                                          pL->value.asSymbol.name );
            else if( pR->ExprType == HB_ET_VARIABLE && pL->ExprType == HB_ET_NIL )
               hb_refTabMaybeMarkNilable( pTab, pCtx,
                                          pR->value.asSymbol.name );
         }
         break;
      }

      /* Assignment to NIL: param := NIL */
      case HB_EO_ASSIGN:
      {
         PHB_EXPR pL = pExpr->value.asOperator.pLeft;
         PHB_EXPR pR = pExpr->value.asOperator.pRight;
         if( pL && pR &&
             pL->ExprType == HB_ET_VARIABLE && pR->ExprType == HB_ET_NIL )
            hb_refTabMaybeMarkNilable( pTab, pCtx,
                                       pL->value.asSymbol.name );
         break;
      }

      default:
         break;
   }
}

/* Names that, when called inside a function body, mark that function
   as variadic (uses more args than declared). */
static HB_BOOL hb_refTabIsVariadicProbe( const char * szName )
{
   return szName &&
      ( hb_stricmp( szName, "PCOUNT"       ) == 0 ||
        hb_stricmp( szName, "HB_PVALUE"    ) == 0 ||
        hb_stricmp( szName, "HB_PARAMETER" ) == 0 ||
        hb_stricmp( szName, "HB_APARAMS"   ) == 0 );
}

/* Walk an HB_ET_LIST/HB_ET_ARGLIST argument list and:
     1) mark positions where the arg is HB_ET_VARREF/HB_ET_REFERENCE
     2) recurse into each arg expression so nested calls are scanned too */
static void hb_refTabScanArgList( PHB_REFTAB pTab, const char * szFunc,
                                  PHB_EXPR pParms, HB_SCANCTX * pCtx )
{
   PHB_EXPR pArg;
   int      iPos = 0;

   if( ! pParms )
      return;

   if( pParms->ExprType == HB_ET_LIST ||
       pParms->ExprType == HB_ET_ARGLIST )
      pArg = pParms->value.asList.pExprList;
   else
      pArg = pParms;  /* defensive: a single bare arg */

   while( pArg )
   {
      /* HB_ET_NONE is the empty-arglist sentinel — count it as a slot
         (for the Fred(x, , z) case) but don't recurse. */
      if( pArg->ExprType == HB_ET_NONE )
      {
         pArg = pArg->pNext;
         iPos++;
         continue;
      }
      if( pArg->ExprType == HB_ET_VARREF )
      {
         if( szFunc )
            hb_refTabMark( pTab, szFunc, iPos );
      }
      else if( pArg->ExprType == HB_ET_REFERENCE )
      {
         if( szFunc )
            hb_refTabMark( pTab, szFunc, iPos );
         hb_refTabScanExpr( pTab, pArg->value.asReference, pCtx );
      }
      else
         hb_refTabScanExpr( pTab, pArg, pCtx );
      pArg = pArg->pNext;
      iPos++;
   }
}

static void hb_refTabScanExpr( PHB_REFTAB pTab, PHB_EXPR pExpr,
                               HB_SCANCTX * pCtx )
{
   if( ! pExpr )
      return;

   switch( pExpr->ExprType )
   {
      case HB_ET_FUNCALL:
      {
         const char * szName = NULL;
         if( pExpr->value.asFunCall.pFunName &&
             pExpr->value.asFunCall.pFunName->ExprType == HB_ET_FUNNAME )
            szName = pExpr->value.asFunCall.pFunName->value.asSymbol.name;

         /* If this call probes argument count, the *enclosing*
            function is variadic. */
         if( pCtx && hb_refTabIsVariadicProbe( szName ) )
            pCtx->fVariadic = HB_TRUE;

         hb_refTabScanArgList( pTab, szName,
                               pExpr->value.asFunCall.pParms, pCtx );
         hb_refTabScanExpr( pTab, pExpr->value.asFunCall.pFunName, pCtx );
         break;
      }

      case HB_ET_SEND:
      {
         /* For method calls we want the by-ref bitmap keyed on
            Class::Method when we can determine the class. The two
            cases the scanner can statically resolve are:
              - Self:method(...) inside a method body, where the
                enclosing class is known via pCtx->szClass
              - ::method(...) (the same thing, syntactic sugar)
            For everything else we leave the lookup as the bare method
            name; the type-aware refinement walker in hb_astPropagate
            picks up the strongly-typed cases. */
         const char * szMethod = pExpr->value.asMessage.szMessage;
         const char * szLookup = szMethod;
         char szKeyBuf[ 256 ];

         if( szMethod && pCtx && pCtx->szClass )
         {
            PHB_EXPR pObj = pExpr->value.asMessage.pObject;
            HB_BOOL fSelfCall = HB_FALSE;

            if( ! pObj )
               fSelfCall = HB_TRUE;   /* ::method shorthand */
            else if( pObj->ExprType == HB_ET_VARIABLE &&
                     hb_stricmp( pObj->value.asSymbol.name, "Self" ) == 0 )
               fSelfCall = HB_TRUE;

            if( fSelfCall )
            {
               hb_snprintf( szKeyBuf, sizeof( szKeyBuf ), "%s::%s",
                            pCtx->szClass, szMethod );
               szLookup = szKeyBuf;
            }
         }

         hb_refTabScanExpr( pTab, pExpr->value.asMessage.pObject, pCtx );
         hb_refTabScanArgList( pTab, szLookup,
                               pExpr->value.asMessage.pParms, pCtx );
         hb_refTabScanExpr( pTab, pExpr->value.asMessage.pMessage, pCtx );
         break;
      }

      case HB_ET_REFERENCE:
         hb_refTabScanExpr( pTab, pExpr->value.asReference, pCtx );
         break;

      case HB_ET_ARRAYAT:
         hb_refTabScanExpr( pTab, pExpr->value.asList.pExprList, pCtx );
         hb_refTabScanExpr( pTab, pExpr->value.asList.pIndex, pCtx );
         break;

      case HB_ET_LIST:
      case HB_ET_ARGLIST:
      case HB_ET_MACROARGLIST:
      case HB_ET_ARRAY:
      case HB_ET_HASH:
      case HB_ET_IIF:
      {
         PHB_EXPR p = pExpr->value.asList.pExprList;
         while( p )
         {
            hb_refTabScanExpr( pTab, p, pCtx );
            p = p->pNext;
         }
         break;
      }

      case HB_ET_CODEBLOCK:
      {
         PHB_EXPR p = pExpr->value.asCodeblock.pExprList;
         while( p )
         {
            hb_refTabScanExpr( pTab, p, pCtx );
            p = p->pNext;
         }
         break;
      }

      case HB_ET_ALIAS:
      case HB_ET_ALIASVAR:
      case HB_ET_ALIASEXPR:
         hb_refTabScanExpr( pTab, pExpr->value.asAlias.pAlias, pCtx );
         hb_refTabScanExpr( pTab, pExpr->value.asAlias.pVar, pCtx );
         hb_refTabScanExpr( pTab, pExpr->value.asAlias.pExpList, pCtx );
         break;

      case HB_ET_SETGET:
         hb_refTabScanExpr( pTab, pExpr->value.asAlias.pAlias, pCtx );
         hb_refTabScanExpr( pTab, pExpr->value.asAlias.pVar, pCtx );
         break;

      default:
         /* Operators (>= HB_EO_POSTINC) live in the asOperator union */
         if( pExpr->ExprType >= HB_EO_POSTINC )
         {
            hb_refTabScanExpr( pTab, pExpr->value.asOperator.pLeft, pCtx );
            hb_refTabScanExpr( pTab, pExpr->value.asOperator.pRight, pCtx );
         }
         break;
   }
}

static void hb_refTabScanStmt( PHB_REFTAB pTab, PHB_AST_NODE pStmt,
                               HB_SCANCTX * pCtx )
{
   while( pStmt )
   {
      switch( pStmt->type )
      {
         case HB_AST_EXPRSTMT:
            hb_refTabCheckNilPattern( pTab, pCtx, pStmt->value.asExprStmt.pExpr );
            hb_refTabScanExpr( pTab, pStmt->value.asExprStmt.pExpr, pCtx );
            break;

         case HB_AST_RETURN:
            hb_refTabScanExpr( pTab, pStmt->value.asReturn.pExpr, pCtx );
            break;

         case HB_AST_QOUT:
         case HB_AST_QQOUT:
            hb_refTabScanExpr( pTab, pStmt->value.asQOut.pExprList, pCtx );
            break;

         case HB_AST_LOCAL:
         case HB_AST_STATIC:
         case HB_AST_FIELD:
         case HB_AST_MEMVAR:
         case HB_AST_PRIVATE:
         case HB_AST_PUBLIC:
            hb_refTabScanExpr( pTab, pStmt->value.asVar.pInit, pCtx );
            break;

         case HB_AST_IF:
            hb_refTabCheckNilPattern( pTab, pCtx, pStmt->value.asIf.pCondition );
            hb_refTabScanExpr( pTab, pStmt->value.asIf.pCondition, pCtx );
            hb_refTabScanStmt( pTab, pStmt->value.asIf.pThen, pCtx );
            {
               PHB_AST_NODE p = pStmt->value.asIf.pElseIfs;
               while( p )
               {
                  hb_refTabCheckNilPattern( pTab, pCtx, p->value.asElseIf.pCondition );
                  hb_refTabScanExpr( pTab, p->value.asElseIf.pCondition, pCtx );
                  hb_refTabScanStmt( pTab, p->value.asElseIf.pBody, pCtx );
                  p = p->pNext;
               }
            }
            hb_refTabScanStmt( pTab, pStmt->value.asIf.pElse, pCtx );
            break;

         case HB_AST_DOWHILE:
            hb_refTabCheckNilPattern( pTab, pCtx, pStmt->value.asWhile.pCondition );
            hb_refTabScanExpr( pTab, pStmt->value.asWhile.pCondition, pCtx );
            hb_refTabScanStmt( pTab, pStmt->value.asWhile.pBody, pCtx );
            break;

         case HB_AST_FOR:
            hb_refTabScanExpr( pTab, pStmt->value.asFor.pStart, pCtx );
            hb_refTabScanExpr( pTab, pStmt->value.asFor.pEnd, pCtx );
            hb_refTabScanExpr( pTab, pStmt->value.asFor.pStep, pCtx );
            hb_refTabScanStmt( pTab, pStmt->value.asFor.pBody, pCtx );
            break;

         case HB_AST_FOREACH:
            hb_refTabScanExpr( pTab, pStmt->value.asForEach.pVar, pCtx );
            hb_refTabScanExpr( pTab, pStmt->value.asForEach.pEnum, pCtx );
            hb_refTabScanStmt( pTab, pStmt->value.asForEach.pBody, pCtx );
            break;

         case HB_AST_DOCASE:
         {
            PHB_AST_NODE p = pStmt->value.asDoCase.pCases;
            while( p )
            {
               hb_refTabCheckNilPattern( pTab, pCtx, p->value.asCase.pCondition );
               hb_refTabScanExpr( pTab, p->value.asCase.pCondition, pCtx );
               hb_refTabScanStmt( pTab, p->value.asCase.pBody, pCtx );
               p = p->pNext;
            }
            hb_refTabScanStmt( pTab, pStmt->value.asDoCase.pOtherwise, pCtx );
            break;
         }

         case HB_AST_SWITCH:
         {
            PHB_AST_NODE p = pStmt->value.asSwitch.pCases;
            hb_refTabScanExpr( pTab, pStmt->value.asSwitch.pSwitch, pCtx );
            while( p )
            {
               hb_refTabScanExpr( pTab, p->value.asCase.pCondition, pCtx );
               hb_refTabScanStmt( pTab, p->value.asCase.pBody, pCtx );
               p = p->pNext;
            }
            hb_refTabScanStmt( pTab, pStmt->value.asSwitch.pDefault, pCtx );
            break;
         }

         case HB_AST_BEGINSEQ:
            hb_refTabScanStmt( pTab, pStmt->value.asSeq.pBody, pCtx );
            hb_refTabScanStmt( pTab, pStmt->value.asSeq.pRecover, pCtx );
            hb_refTabScanStmt( pTab, pStmt->value.asSeq.pAlways, pCtx );
            break;

         case HB_AST_WITHOBJECT:
            hb_refTabScanExpr( pTab, pStmt->value.asWithObj.pObject, pCtx );
            hb_refTabScanStmt( pTab, pStmt->value.asWithObj.pBody, pCtx );
            break;

         case HB_AST_BREAK:
            hb_refTabScanExpr( pTab, pStmt->value.asBreak.pExpr, pCtx );
            break;

         case HB_AST_BLOCK:
            hb_refTabScanStmt( pTab, pStmt->value.asBlock.pFirst, pCtx );
            break;

         default:
            break;
      }
      pStmt = pStmt->pNext;
   }
}

/* If pFunc is a method implementation (its body starts with an
   HB_AST_CLASSMETHOD marker carrying the owning class name), return
   that class name. Otherwise return NULL — meaning this is a free
   function. */
static const char * hb_refTabFuncClass( PHB_AST_NODE pFunc )
{
   PHB_AST_NODE pFirstStmt;

   if( ! pFunc || pFunc->type != HB_AST_FUNCTION ||
       ! pFunc->value.asFunc.pBody ||
       pFunc->value.asFunc.pBody->type != HB_AST_BLOCK )
      return NULL;

   pFirstStmt = pFunc->value.asFunc.pBody->value.asBlock.pFirst;
   if( pFirstStmt && pFirstStmt->type == HB_AST_CLASSMETHOD )
      return pFirstStmt->value.asClassMethod.szClass;
   return NULL;
}

void hb_refTabCollect( PHB_REFTAB pTab, HB_COMP_DECL )
{
   PHB_AST_NODE pFunc;
   PHB_HFUNC    pCompFunc;

   /* ----- Pass 0: mark every CLASS so cross-file callers can detect
      ClassName():New() patterns even when ClassName lives elsewhere.
      The class list is stored in the auto-generated startup function's
      body alongside #include / #define. */
   {
      PHB_AST_NODE pFirst = HB_COMP_PARAM->ast.pFuncList;
      if( pFirst && pFirst->type == HB_AST_FUNCTION &&
          pFirst->value.asFunc.pBody &&
          pFirst->value.asFunc.pBody->type == HB_AST_BLOCK )
      {
         PHB_AST_NODE p = pFirst->value.asFunc.pBody->value.asBlock.pFirst;
         while( p )
         {
            if( p->type == HB_AST_CLASS && p->value.asClass.szName )
               hb_refTabMarkClass( pTab, p->value.asClass.szName );
            p = p->pNext;
         }
      }
   }

   /* ----- Pass 1: register every function's signature.
      Must run before any body walks so that cross-references within
      the same file (e.g. Main calling Foo which is defined later in
      source order) work. */
   pFunc     = HB_COMP_PARAM->ast.pFuncList;
   pCompFunc = HB_COMP_PARAM->functions.pFirst;
   while( pFunc )
   {
      if( pFunc->type == HB_AST_FUNCTION )
      {
         while( pCompFunc && ( pCompFunc->funFlags & HB_FUNF_FILE_DECL ) )
            pCompFunc = pCompFunc->pNext;

         if( pCompFunc && ! ( pCompFunc->funFlags & HB_FUNF_FILE_FIRST ) )
         {
            PHB_HVAR pVar = pFunc->value.asFunc.pParams;
            int nParams = 0;
            int nCount  = pCompFunc->wParamCount;
            const char * names[ HB_REFTAB_MAXPARAM ];
            const char * types[ HB_REFTAB_MAXPARAM ];
            const char * szClass = hb_refTabFuncClass( pFunc );
            const char * szKey   = hb_refTabMethodKey(
               szClass, pFunc->value.asFunc.szName );

            while( pVar && nParams < nCount && nParams < HB_REFTAB_MAXPARAM )
            {
               names[ nParams ] = pVar->szName;
               types[ nParams ] = hb_astInferType( pVar->szName, NULL );
               nParams++;
               pVar = pVar->pNext;
            }
            hb_refTabAddFunc( pTab, szKey, nParams, names, types, HB_FALSE );
         }

         if( pCompFunc )
            pCompFunc = pCompFunc->pNext;
      }
      pFunc = pFunc->pNext;
   }

   /* ----- Pass 2: scan each body for variadic/nilable/by-ref flags. */
   pFunc     = HB_COMP_PARAM->ast.pFuncList;
   pCompFunc = HB_COMP_PARAM->functions.pFirst;
   while( pFunc )
   {
      if( pFunc->type == HB_AST_FUNCTION )
      {
         while( pCompFunc && ( pCompFunc->funFlags & HB_FUNF_FILE_DECL ) )
            pCompFunc = pCompFunc->pNext;

         if( pCompFunc && ! ( pCompFunc->funFlags & HB_FUNF_FILE_FIRST ) )
         {
            PHB_HVAR pVar = pFunc->value.asFunc.pParams;
            int nParams = 0;
            int nCount  = pCompFunc->wParamCount;
            const char * names[ HB_REFTAB_MAXPARAM ];
            const char * szClass = hb_refTabFuncClass( pFunc );
            const char * szKey   = hb_refTabMethodKey(
               szClass, pFunc->value.asFunc.szName );
            /* hb_refTabMethodKey returns a static buffer when szClass
               is set; copy it before any other call to the helper
               could overwrite it. */
            char szKeyBuf[ 256 ];
            hb_strncpy( szKeyBuf, szKey, sizeof( szKeyBuf ) - 1 );

            while( pVar && nParams < nCount && nParams < HB_REFTAB_MAXPARAM )
            {
               names[ nParams ] = pVar->szName;
               nParams++;
               pVar = pVar->pNext;
            }

            {
               HB_SCANCTX ctx;
               ctx.szFunc       = szKeyBuf;
               ctx.szClass      = szClass;   /* NULL for free functions */
               ctx.ppParamNames = names;
               ctx.nParams      = nParams;
               ctx.fVariadic    = HB_FALSE;

               if( pFunc->value.asFunc.pBody )
                  hb_refTabScanStmt( pTab, pFunc->value.asFunc.pBody, &ctx );

               /* Promote the variadic flag if we found PCount() etc. */
               if( ctx.fVariadic )
               {
                  PHB_REFENTRY e = hb_refTabFindEntry( pTab, ctx.szFunc, NULL );
                  if( e )
                     e->fVariadic = HB_TRUE;
               }
            }
         }
         else if( pCompFunc && pFunc->value.asFunc.pBody )
         {
            /* Auto-generated startup function body. */
            HB_SCANCTX ctx = { NULL, NULL, NULL, 0, HB_FALSE };
            hb_refTabScanStmt( pTab, pFunc->value.asFunc.pBody, &ctx );
         }

         if( pCompFunc )
            pCompFunc = pCompFunc->pNext;
      }
      pFunc = pFunc->pNext;
   }

   /* ----- Pass 3: compute return types AND run the call-site
      parameter-type refinement walker. hb_astPropagate internally
      runs Pass 5 of the type inference, which visits call sites in
      source order and refines callee parameter types. Because Pass 1
      already registered every function in this file, the walker
      finds entries to refine regardless of source order. */
   pFunc     = HB_COMP_PARAM->ast.pFuncList;
   pCompFunc = HB_COMP_PARAM->functions.pFirst;
   while( pFunc )
   {
      if( pFunc->type == HB_AST_FUNCTION )
      {
         while( pCompFunc && ( pCompFunc->funFlags & HB_FUNF_FILE_DECL ) )
            pCompFunc = pCompFunc->pNext;

         if( pCompFunc && ! ( pCompFunc->funFlags & HB_FUNF_FILE_FIRST ) &&
             pFunc->value.asFunc.pBody )
         {
            const char * szClass = hb_refTabFuncClass( pFunc );
            const char * szKey   = hb_refTabMethodKey(
               szClass, pFunc->value.asFunc.szName );
            char szKeyBuf[ 256 ];
            hb_strncpy( szKeyBuf, szKey, sizeof( szKeyBuf ) - 1 );
            {
               const char * szRetType =
                  hb_astPropagate( pFunc->value.asFunc.pBody, NULL, pTab );
               if( szRetType )
                  hb_refTabSetReturnType( pTab, szKeyBuf, szRetType );
            }
         }
         else if( pCompFunc && pFunc->value.asFunc.pBody )
         {
            /* Startup function — still walk for call-site refinement,
               but we're not interested in its return type. */
            hb_astPropagate( pFunc->value.asFunc.pBody, NULL, pTab );
         }

         if( pCompFunc )
            pCompFunc = pCompFunc->pNext;
      }
      pFunc = pFunc->pNext;
   }
}
