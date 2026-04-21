/*
 * Harbour Transpiler - User function signature table + AST scanner
 *
 * See include/hbreftab.h for the API and rationale.
 *
 * Copyright 2026 harbour.github.io
 */

#include <stdlib.h>          /* strtoull for the A=<hex> arity bitmap */

#include "hbcomp.h"
#include "hbast.h"
#include "hbreftab.h"

#define HB_REFTAB_BUCKETS  1024  /* power of two */
#define HB_REFTAB_MAXPARAM 64    /* hard cap; matches the by-ref bitmap width */

typedef struct HB_REFENTRY_
{
   char *                szName;       /* declaration-site casing, owned.
                                          Lookup is hb_stricmp so casing
                                          doesn't affect matching, but the
                                          C# emitter uses this verbatim to
                                          collapse mixed-case call sites
                                          onto the single declared form. */
   char *                szReturnType; /* inferred return type, or NULL (owned) */
   char *                szPublicOwner;/* if fIsPublic: base filename of first .prg that declared PUBLIC szName (owned) */
   int                   nParams;      /* declared parameter count, -1 = unknown */
   HB_BOOL               fVariadic;    /* function uses PCount/HB_PValue */
   HB_BOOL               fCalledVarargs; /* some call site forwards `...` to this function */
   HB_BOOL               fDefined;     /* set once a real definition has been seen */
   HB_BOOL               fIsClass;     /* this is a CLASS marker, not a function */
   HB_BOOL               fClassDynamic;/* fIsClass + class uses ::&(name) — emit as `dynamic` */
   HB_BOOL               fIsPublic;    /* this is a PUBLIC variable marker */
   HB_BOOL               fPublicArrayDim; /* PUBLIC declared with [size] — emit `new dynamic[N]` at runtime */
   HB_REFPARAM *         pParams;      /* nParams entries (NULL if unknown) */
   HB_U64                bitmap;       /* by-ref bitmap, bit n = arg n is by-ref */
   HB_U64                nilbits;      /* nilable bitmap, bit n = slot n is nilable */
   HB_U64                bitCallArities; /* observed call-site arities: bit n = a
                                           caller passed n args. Lets the emitter
                                           skip the short overload when every
                                           caller already uses the canonical full
                                           arity, avoiding dead methods. */
   struct HB_REFENTRY_ * pNext;
} HB_REFENTRY, * PHB_REFENTRY;

/* Deferred-free list: superseded type-string allocations from
   hb_refTabRefineParamType. We cannot free eagerly because the type
   inference env in hb_astPropagate may still hold pointers to these
   strings (the env is seeded from pParams[i].szType and mutations in
   Pass 5 would otherwise invalidate env entries for the current
   function's own param slots). We keep the superseded allocations
   alive for the table's lifetime and release them all in
   hb_refTabFree. The strings are small (class names, ~10-30 bytes
   each) and the number of refinements per scan is bounded by call
   sites, so the retained memory is negligible (< 1 MB per scan). */
typedef struct HB_REFTAB_DEFERRED_
{
   char *                       sz;
   struct HB_REFTAB_DEFERRED_ * pNext;
} HB_REFTAB_DEFERRED, * PHB_REFTAB_DEFERRED;

struct HB_REFTAB_
{
   PHB_REFENTRY         buckets[ HB_REFTAB_BUCKETS ];
   HB_SIZE              nCount;
   PHB_REFTAB_DEFERRED  pDeferred;    /* freed on hb_refTabFree */
};

static void hb_refTabDefer( PHB_REFTAB pTab, char * sz )
{
   PHB_REFTAB_DEFERRED p;
   if( ! sz )
      return;
   p = ( PHB_REFTAB_DEFERRED ) hb_xgrab( sizeof( HB_REFTAB_DEFERRED ) );
   p->sz        = sz;
   p->pNext     = pTab->pDeferred;
   pTab->pDeferred = p;
}

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

/* True iff sz names one of the built-in scalar type tags emitted by
   the type propagator. USUAL/OBJECT are intentionally absent — the
   conflict branch filters them upstream. Anything not on this list
   is treated as a user class name. */
static HB_BOOL hb_refTabIsScalarType( const char * sz )
{
   return sz && (
      hb_stricmp( sz, "NUMERIC"   ) == 0 ||
      hb_stricmp( sz, "STRING"    ) == 0 ||
      hb_stricmp( sz, "LOGICAL"   ) == 0 ||
      hb_stricmp( sz, "DATE"      ) == 0 ||
      hb_stricmp( sz, "TIMESTAMP" ) == 0 ||
      hb_stricmp( sz, "ARRAY"     ) == 0 ||
      hb_stricmp( sz, "HASH"      ) == 0 ||
      hb_stricmp( sz, "BLOCK"     ) == 0 ||
      hb_stricmp( sz, "CODEBLOCK" ) == 0 ||
      hb_stricmp( sz, "CHARACTER" ) == 0 ||
      hb_stricmp( sz, "INTEGER"   ) == 0 ||
      hb_stricmp( sz, "DECIMAL"   ) == 0 ||
      hb_stricmp( sz, "NIL"       ) == 0 ||
      hb_stricmp( sz, "SYMBOL"    ) == 0 ||
      hb_stricmp( sz, "POINTER"   ) == 0 );
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
      e->szName    = hb_refTabDup( szName );
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
   PHB_REFTAB_DEFERRED pDef;

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
         if( e->szPublicOwner )
            hb_xfree( e->szPublicOwner );
         hb_xfree( e->szName );
         hb_xfree( e );
         e = pNext;
      }
   }
   pDef = pTab->pDeferred;
   while( pDef )
   {
      PHB_REFTAB_DEFERRED pNext = pDef->pNext;
      if( pDef->sz )
         hb_xfree( pDef->sz );
      hb_xfree( pDef );
      pDef = pNext;
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

   /* Overwrite szName with the declaration-site casing. Prior stub
      entries (created by a by-ref mark at a call site) recorded
      whatever spelling the call site used; the real definition
      wins and becomes the canonical form that the C# emitter will
      use to collapse mixed-case call-site references. */
   if( e->szName && strcmp( e->szName, szName ) != 0 )
   {
      hb_refTabDefer( pTab, e->szName );
      e->szName = hb_refTabDup( szName );
   }

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

      /* Multi-def arity disagreement: same name declared in two .prg
         files with different parameter counts (e.g. EcrDir has a
         zero-arg and a one-arg flavour). The reftab holds one entry
         per name, so the second definition's AddFunc call overwrites
         the first — call sites matching the clobbered signature then
         surface as bogus CS1501 / W0018. Fold the two by taking the
         wider arity and marking the entry variadic; the emitter
         widens the C# sig to `params dynamic[]` and both caller
         shapes resolve. */
      int iSuppliedParams = nParams;
      if( e->fDefined && nOld != nParams )
      {
         if( nOld > nParams )
            nParams = nOld;
         fVariadic = HB_TRUE;
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
         /* Only pull from ppNames/ppTypes for slots the caller
            actually supplied. When we widened via the multi-def
            merge, trailing slots fall back to whatever the old
            entry had. */
         const char * szPName = ( ppNames && i < iSuppliedParams ) ? ppNames[ i ] : NULL;
         const char * szPType = ( ppTypes && i < iSuppliedParams ) ? ppTypes[ i ] : NULL;
         const char * szOldType = NULL;

         if( pOld && i < nOld )
         {
            szOldType = pOld[ i ].szType;
            if( i >= iSuppliedParams && pOld[ i ].szName )
               szPName = pOld[ i ].szName;
         }

         e->pParams[ i ].szName = hb_refTabDup( szPName ? szPName : "" );

         /* Prefer the prior refined type over a fresh USUAL or OBJECT.
            OBJECT from Hungarian is just "some object" — a specific
            class name from call-site refinement should survive. */
         if( ( ! szPType || ! *szPType ||
               hb_stricmp( szPType, "USUAL" ) == 0 ||
               hb_stricmp( szPType, "OBJECT" ) == 0 ) &&
             szOldType &&
             hb_stricmp( szOldType, "USUAL" ) != 0 &&
             hb_stricmp( szOldType, "OBJECT" ) != 0 )
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

   /* Agreement check runs BEFORE the mutation branches so that
      szNewType pointing into pParam->szType itself (recursive call:
      env for function X was seeded from X's pParams, and the body
      passes X's own param back to X) cannot trigger a free-then-read
      UAF. Same content → no-op, regardless of aliasing. */
   if( pParam->szType && hb_stricmp( pParam->szType, szNewType ) == 0 )
      return HB_REFINE_OK;

   if( ! pParam->szType || hb_stricmp( pParam->szType, "USUAL" ) == 0 ||
       hb_stricmp( pParam->szType, "OBJECT" ) == 0 )
   {
      /* Fresh/untyped/generic-OBJECT slot — adopt the new type.
         OBJECT is upgradeable to a specific class name when the call
         site passes a constructor-typed variable. Defer-free the old
         allocation: the type-inference env in the enclosing
         hb_astPropagate may have seeded a pointer to it and would
         otherwise dangle. */
      char * szDup = hb_refTabDup( szNewType );
      if( pParam->szType )
         hb_refTabDefer( pTab, pParam->szType );
      pParam->szType = szDup;
      return HB_REFINE_REFINED;
   }

   /* One side OBJECT, the other a specific class: keep the specific
      class. OBJECT is just "I don't know the class" from Hungarian
      prefix — it shouldn't conflict with a concrete class name that
      a constructor-typed call site provides. BUT: if the existing
      type is a scalar (NUMERIC/STRING/…), incoming OBJECT is a real
      type disagreement and must fall through to the conflict
      resolver below. Silently keeping the scalar would lock the
      slot to a type that its users treat as an object — the mistake
      cascades across files via call-site propagation and produces
      CS1061 (:member on decimal) at emit time. */
   if( hb_stricmp( szNewType, "OBJECT" ) == 0 &&
       ! hb_refTabIsScalarType( pParam->szType ) )
      return HB_REFINE_OK;   /* incoming OBJECT doesn't override a class */

   /* Genuine disagreement: freeze the slot with fConflict set so
      later refinements stop trying. If NEITHER side is a built-in
      scalar tag, both are class names — fall back to OBJECT rather
      than USUAL, since both sides at least agree on "it's an object,
      just not which class". A scalar tag disagreeing with anything —
      another scalar or a class — widens to USUAL. Without the class
      branch, USUAL:C settled here would flip to OBJECT:C on the very
      next scan pass (hb_refTabAddFunc re-applies Hungarian 'o' over
      USUAL), costing one extra pass to reach convergence.
      Defer-free the old allocation for the same reason as above. */
   {
      HB_BOOL fBothClass = ! hb_refTabIsScalarType( pParam->szType ) &&
                           ! hb_refTabIsScalarType( szNewType );
      hb_refTabDefer( pTab, pParam->szType );
      pParam->szType = hb_refTabDup( fBothClass ? "OBJECT" : "USUAL" );
   }
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

void hb_refTabMarkClassDynamic( PHB_REFTAB pTab, const char * szName )
{
   PHB_REFENTRY e;
   if( ! pTab || ! szName )
      return;
   e = hb_refTabFindOrCreate( pTab, szName );
   e->fIsClass      = HB_TRUE;
   e->fClassDynamic = HB_TRUE;
   e->fDefined      = HB_TRUE;
   if( e->nParams < 0 )
      e->nParams = 0;
}

HB_BOOL hb_refTabIsClassDynamic( PHB_REFTAB pTab, const char * szName )
{
   PHB_REFENTRY e;
   if( ! pTab || ! szName )
      return HB_FALSE;
   e = hb_refTabFindEntry( pTab, szName, NULL );
   return e && e->fClassDynamic;
}

void hb_refTabMarkPublic( PHB_REFTAB pTab, const char * szName,
                          const char * szOwnerBase, HB_BOOL fArrayDim )
{
   PHB_REFENTRY e;
   if( ! pTab || ! szName )
      return;
   e = hb_refTabFindOrCreate( pTab, szName );
   e->fIsPublic = HB_TRUE;
   e->fDefined  = HB_TRUE;
   if( fArrayDim )
      e->fPublicArrayDim = HB_TRUE;
   if( e->nParams < 0 )
      e->nParams = 0;
   /* First declarer wins — subsequent PUBLICs of the same name in
      other files share the single Program-partial field. */
   if( ! e->szPublicOwner && szOwnerBase && *szOwnerBase )
      e->szPublicOwner = hb_refTabDup( szOwnerBase );
}

HB_BOOL hb_refTabIsPublic( PHB_REFTAB pTab, const char * szName )
{
   PHB_REFENTRY e;
   if( ! pTab || ! szName )
      return HB_FALSE;
   e = hb_refTabFindEntry( pTab, szName, NULL );
   return e && e->fIsPublic;
}

const char * hb_refTabPublicOwner( PHB_REFTAB pTab, const char * szName )
{
   PHB_REFENTRY e;
   if( ! pTab || ! szName )
      return NULL;
   e = hb_refTabFindEntry( pTab, szName, NULL );
   return e ? e->szPublicOwner : NULL;
}

HB_BOOL hb_refTabIsPublicArrayDim( PHB_REFTAB pTab, const char * szName )
{
   PHB_REFENTRY e;
   if( ! pTab || ! szName )
      return HB_FALSE;
   e = hb_refTabFindEntry( pTab, szName, NULL );
   return e && e->fPublicArrayDim;
}

void hb_refTabForEachPublic( PHB_REFTAB pTab,
                              const char * szOwnerBase,
                              void ( * pCallback )( const char * szName,
                                                    HB_BOOL fArrayDim,
                                                    void * userdata ),
                              void * userdata )
{
   HB_SIZE i;
   if( ! pTab || ! pCallback )
      return;
   for( i = 0; i < HB_REFTAB_BUCKETS; i++ )
   {
      PHB_REFENTRY e = pTab->buckets[ i ];
      while( e )
      {
         if( e->fIsPublic && e->szName &&
             ( ! szOwnerBase ||
               ( e->szPublicOwner &&
                 hb_stricmp( e->szPublicOwner, szOwnerBase ) == 0 ) ) )
            pCallback( e->szName, e->fPublicArrayDim, userdata );
         e = e->pNext;
      }
   }
}

void hb_refTabMarkCalledVarargs( PHB_REFTAB pTab, const char * szName )
{
   PHB_REFENTRY e;
   if( ! pTab || ! szName )
      return;
   e = hb_refTabFindOrCreate( pTab, szName );
   e->fCalledVarargs = HB_TRUE;
}

HB_BOOL hb_refTabIsCalledVarargs( PHB_REFTAB pTab, const char * szName )
{
   PHB_REFENTRY e;
   if( ! pTab || ! szName )
      return HB_FALSE;
   e = hb_refTabFindEntry( pTab, szName, NULL );
   return e && e->fCalledVarargs;
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

HB_U64 hb_refTabCallArities( PHB_REFTAB pTab, const char * szFunc )
{
   PHB_REFENTRY e;
   if( ! pTab || ! szFunc )
      return 0;
   e = hb_refTabFindEntry( pTab, szFunc, NULL );
   return e ? e->bitCallArities : 0;
}

const char * hb_refTabFuncCanon( PHB_REFTAB pTab, const char * szFunc )
{
   PHB_REFENTRY e;
   if( ! pTab || ! szFunc )
      return szFunc;
   e = hb_refTabFindEntry( pTab, szFunc, NULL );
   return e && e->szName ? e->szName : szFunc;
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

static int hb_refTabCmpEntryByName( const void * a, const void * b )
{
   const PHB_REFENTRY ea = *( const PHB_REFENTRY * ) a;
   const PHB_REFENTRY eb = *( const PHB_REFENTRY * ) b;
   return strcmp( ea->szName, eb->szName );
}

HB_BOOL hb_refTabSave( PHB_REFTAB pTab, const char * szPath )
{
   FILE *          fp;
   HB_SIZE         i;
   HB_SIZE         nEntries = 0;
   HB_SIZE         nCap     = 0;
   PHB_REFENTRY *  ppSorted = NULL;

   if( ! pTab || ! szPath )
      return HB_FALSE;
   fp = hb_fopen( szPath, "w" );
   if( ! fp )
      return HB_FALSE;
   fprintf( fp, "# Harbour transpiler user-function signature table\n" );
   fprintf( fp, "# Format: NAME<TAB>FLAGS<TAB>RETTYPE<TAB>NPARAMS<TAB>PARAM_1<TAB>...\n" );
   fprintf( fp, "# FLAGS:    V = variadic, S = called-with-spread, K = class, D = dynamic class (extends HbDynamicObject),\n" );
   fprintf( fp, "#           P = public var, A = public var with array-dim, - = none\n" );
   fprintf( fp, "# RETTYPE:  inferred return type, or - if unknown; for P entries this slot holds the owning .prg basename\n" );
   fprintf( fp, "# PARAM:    name:type:pflags\n" );
   fprintf( fp, "# pflags letters:  R = byref, N = nilable, C = conflict, - = none\n" );
   fprintf( fp, "#\n" );
   fprintf( fp, "# THIS FILE IS GENERATED — see `hbtranspiler -GF`\n" );

   /* Collect defined entries then sort by name. Hash-table bucket +
      linked-list order depends on insertion sequence, which isn't
      stable between scan passes — sorting gives a deterministic
      file so md5-based convergence checks actually work. */
   for( i = 0; i < HB_REFTAB_BUCKETS; i++ )
   {
      PHB_REFENTRY e = pTab->buckets[ i ];
      while( e )
      {
         if( e->fDefined )
         {
            if( nEntries == nCap )
            {
               nCap = nCap ? nCap * 2 : 256;
               ppSorted = ( PHB_REFENTRY * ) hb_xrealloc(
                  ppSorted, nCap * sizeof( PHB_REFENTRY ) );
            }
            ppSorted[ nEntries++ ] = e;
         }
         e = e->pNext;
      }
   }
   if( nEntries > 1 )
      qsort( ppSorted, nEntries, sizeof( PHB_REFENTRY ),
             hb_refTabCmpEntryByName );

   for( i = 0; i < nEntries; i++ )
   {
      PHB_REFENTRY e = ppSorted[ i ];
      {
         /* Skip stub entries that only have flags and no registered
            definition — they would round-trip as zero-arg functions,
            which is wrong. The flags are still useful in the live
            in-memory table during a single transpile, but they
            shouldn't be persisted without a definition. */
         if( e->fDefined )
         {
            int p;
            char fnFlags[ 8 ];
            int  fnK = 0;
            const char * szRet;
            if( e->fVariadic )      fnFlags[ fnK++ ] = 'V';
            if( e->fCalledVarargs ) fnFlags[ fnK++ ] = 'S';
            if( e->fIsClass )       fnFlags[ fnK++ ] = 'K';
            if( e->fClassDynamic )  fnFlags[ fnK++ ] = 'D';
            if( e->fIsPublic )      fnFlags[ fnK++ ] = 'P';
            if( e->fPublicArrayDim) fnFlags[ fnK++ ] = 'A';
            if( fnK == 0 )          fnFlags[ fnK++ ] = '-';
            fnFlags[ fnK ] = '\0';
            /* For P entries we overload the RETTYPE slot with the owning
               .prg basename. The owner is never useful as a return type
               so the reuse is unambiguous. */
            szRet = e->fIsPublic
               ? ( e->szPublicOwner ? e->szPublicOwner : "-" )
               : ( e->szReturnType ? e->szReturnType : "-" );
            fprintf( fp, "%s\t%s\t%s\t%d",
                     e->szName, fnFlags, szRet, e->nParams );
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
            /* Optional trailing "A=<hex>" field carries the observed
               call-site arity bitmap. Omitted when no call sites were
               seen (bit 0 set is a real 0-arg call, not absence). Old
               loaders ignore unknown tail fields; new loaders key off
               the `A=` prefix. */
            if( e->bitCallArities )
               fprintf( fp, "\tA=%llx",
                        ( unsigned long long ) e->bitCallArities );
            fprintf( fp, "\n" );
         }
      }
   }
   if( ppSorted )
      hb_xfree( ppSorted );
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
         /* FLAGS is a string of letters: V (variadic), S (called with
            `...` spread), K (klass), D (dynamic klass), P (public
            var), A (public array-dim), - */
         char * c;
         HB_BOOL fIsClass   = HB_FALSE;
         HB_BOOL fSpread    = HB_FALSE;
         HB_BOOL fClassDyn  = HB_FALSE;
         HB_BOOL fPub       = HB_FALSE;
         HB_BOOL fPubArr    = HB_FALSE;
         fVariadic = HB_FALSE;
         for( c = fields[ 1 ]; *c; c++ )
         {
            if( *c == 'V' || *c == 'v' )
               fVariadic = HB_TRUE;
            else if( *c == 'S' || *c == 's' )
               fSpread = HB_TRUE;
            else if( *c == 'K' || *c == 'k' )
               fIsClass = HB_TRUE;
            else if( *c == 'D' || *c == 'd' )
               fClassDyn = HB_TRUE;
            else if( *c == 'P' || *c == 'p' )
               fPub = HB_TRUE;
            else if( *c == 'A' || *c == 'a' )
               fPubArr = HB_TRUE;
         }
         if( fPub )
         {
            /* For P entries fields[2] is the owner basename (or '-'). */
            const char * szOwner = ( fields[ 2 ] && fields[ 2 ][ 0 ] &&
                                     strcmp( fields[ 2 ], "-" ) != 0 )
               ? fields[ 2 ] : NULL;
            hb_refTabMarkPublic( pTab, fields[ 0 ], szOwner, fPubArr );
            /* P entries have no params / no return type — skip the
               generic function registration below. */
            continue;
         }
         if( fClassDyn )
            hb_refTabMarkClassDynamic( pTab, fields[ 0 ] );
         else if( fIsClass )
            hb_refTabMarkClass( pTab, fields[ 0 ] );
         if( fSpread )
            hb_refTabMarkCalledVarargs( pTab, fields[ 0 ] );
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
      /* Trailing optional fields. Currently only `A=<hex>` — the
         observed call-site arity bitmap. Tolerates unknown prefixes so
         future tags can be added without breaking loaders in the
         field. */
      for( i = 4 + nParams; i < nFields; i++ )
      {
         if( fields[ i ][ 0 ] == 'A' && fields[ i ][ 1 ] == '=' )
         {
            PHB_REFENTRY pe = hb_refTabFindEntry( pTab, fields[ 0 ], NULL );
            if( pe )
               pe->bitCallArities = ( HB_U64 ) strtoull(
                  fields[ i ] + 2, NULL, 16 );
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
   const char *  szFileBase;      /* current .prg basename (no extension), for PUBLIC ownership */
   const char ** ppParamNames;    /* parameter names of the enclosing func */
   int           nParams;         /* parameter count */
   HB_BOOL       fVariadic;       /* set if PCount/HB_PValue is called */
} HB_SCANCTX;

static void hb_refTabScanExpr( PHB_REFTAB pTab, PHB_EXPR pExpr,
                               HB_SCANCTX * pCtx );
static void hb_refTabScanStmt( PHB_REFTAB pTab, PHB_AST_NODE pStmt,
                               HB_SCANCTX * pCtx );

/* Detect obj:&(name) macro-send anywhere in an expression tree. The
   emitter uses this to flag classes that need an HbDynamicObject base
   (so runtime member resolution works for ::&(name) patterns). We run
   the same detection at scan time and record the result in the reftab
   so callers in OTHER .prg files see the class as `dynamic`-typed. */
static HB_BOOL hb_refTabExprHasMacroSend( PHB_EXPR pExpr )
{
   if( ! pExpr )
      return HB_FALSE;
   switch( pExpr->ExprType )
   {
      case HB_ET_SEND:
         if( pExpr->value.asMessage.pMessage &&
             pExpr->value.asMessage.pMessage->ExprType == HB_ET_MACRO )
            return HB_TRUE;
         if( hb_refTabExprHasMacroSend( pExpr->value.asMessage.pObject ) )
            return HB_TRUE;
         if( hb_refTabExprHasMacroSend( pExpr->value.asMessage.pParms ) )
            return HB_TRUE;
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
            if( hb_refTabExprHasMacroSend( p ) )
               return HB_TRUE;
            p = p->pNext;
         }
         break;
      }
      default:
         if( pExpr->ExprType >= HB_EO_POSTINC )
         {
            if( hb_refTabExprHasMacroSend( pExpr->value.asOperator.pLeft ) )
               return HB_TRUE;
            if( hb_refTabExprHasMacroSend( pExpr->value.asOperator.pRight ) )
               return HB_TRUE;
         }
         break;
   }
   return HB_FALSE;
}

static HB_BOOL hb_refTabBlockHasMacroSend( PHB_AST_NODE pBlock )
{
   PHB_AST_NODE pStmt;
   if( ! pBlock || pBlock->type != HB_AST_BLOCK )
      return HB_FALSE;
   for( pStmt = pBlock->value.asBlock.pFirst; pStmt; pStmt = pStmt->pNext )
   {
      switch( pStmt->type )
      {
         case HB_AST_EXPRSTMT:
            if( hb_refTabExprHasMacroSend( pStmt->value.asExprStmt.pExpr ) )
               return HB_TRUE;
            break;
         case HB_AST_RETURN:
            if( hb_refTabExprHasMacroSend( pStmt->value.asReturn.pExpr ) )
               return HB_TRUE;
            break;
         case HB_AST_LOCAL:
         case HB_AST_STATIC:
         case HB_AST_MEMVAR:
         case HB_AST_PRIVATE:
         case HB_AST_PUBLIC:
            if( hb_refTabExprHasMacroSend( pStmt->value.asVar.pInit ) )
               return HB_TRUE;
            break;
         case HB_AST_IF:
            if( hb_refTabExprHasMacroSend( pStmt->value.asIf.pCondition ) )
               return HB_TRUE;
            if( hb_refTabBlockHasMacroSend( pStmt->value.asIf.pThen ) )
               return HB_TRUE;
            if( hb_refTabBlockHasMacroSend( pStmt->value.asIf.pElse ) )
               return HB_TRUE;
            break;
         case HB_AST_DOWHILE:
            if( hb_refTabBlockHasMacroSend( pStmt->value.asWhile.pBody ) )
               return HB_TRUE;
            break;
         case HB_AST_FOR:
            if( hb_refTabBlockHasMacroSend( pStmt->value.asFor.pBody ) )
               return HB_TRUE;
            break;
         case HB_AST_FOREACH:
            if( hb_refTabBlockHasMacroSend( pStmt->value.asForEach.pBody ) )
               return HB_TRUE;
            break;
         default:
            break;
      }
   }
   return HB_FALSE;
}

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
     2) recurse into each arg expression so nested calls are scanned too
     3) record the observed call arity (bitCallArities bit) so the
        emitter can skip a short overload no caller ever uses */
static void hb_refTabScanArgList( PHB_REFTAB pTab, const char * szFunc,
                                  PHB_EXPR pParms, HB_SCANCTX * pCtx )
{
   PHB_EXPR pArg;
   int      iPos      = 0;
   int      iLastReal = -1;   /* last non-NONE slot; trailing gaps don't count */

   if( ! pParms )
   {
      /* Bare `Foo()` — zero-arg call site. Record arity 0 so the
         emitter knows callers use the shortest form. */
      if( szFunc )
      {
         PHB_REFENTRY e = hb_refTabFindOrCreate( pTab, szFunc );
         e->bitCallArities |= ( HB_U64 ) 1;
      }
      return;
   }

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
      /* `...` as a call argument lands here as an HB_ET_ARGLIST item
         with `reference=TRUE` and an empty child list (see
         hb_compExprNewArgRef). When that shape appears inside an
         argument list, the caller is forwarding its varargs to the
         callee — mark the callee so the emitter knows to widen its
         C# signature to `params dynamic[] hbva`. */
      if( pArg->ExprType == HB_ET_ARGLIST &&
          pArg->value.asList.reference &&
          ! pArg->value.asList.pExprList )
      {
         if( szFunc )
            hb_refTabMarkCalledVarargs( pTab, szFunc );
         iLastReal = iPos;
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
      iLastReal = iPos;
      pArg = pArg->pNext;
      iPos++;
   }

   /* Record the effective call arity — trailing HB_ET_NONE slots are
      dropped to match what hb_csEmitCallArgs emits. */
   if( szFunc && iLastReal + 1 < HB_REFTAB_MAXPARAM )
   {
      PHB_REFENTRY e = hb_refTabFindOrCreate( pTab, szFunc );
      e->bitCallArities |= ( ( HB_U64 ) 1 ) << ( iLastReal + 1 );
   }
}

static void hb_refTabScanExpr( PHB_REFTAB pTab, PHB_EXPR pExpr,
                               HB_SCANCTX * pCtx )
{
   if( ! pExpr )
      return;

   /* A bare `...` anywhere in the body — in an array literal
      (`aArgs := { ... }`), inside any expression, etc. — means the
      enclosing function captures varargs. It's distinct from the
      call-site case `foo(...)` (handled in hb_refTabScanArgList),
      which marks the *callee* as called-with-spread. */
   if( pCtx && pExpr->ExprType == HB_ET_ARGLIST &&
       pExpr->value.asList.reference &&
       ! pExpr->value.asList.pExprList )
      pCtx->fVariadic = HB_TRUE;

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
         /* HB_ET_ALIAS is the bare alias-keyword node created by
            hb_compExprNewAlias() — e.g. `FIELD->` or `MEMVAR->`. It
            uses the asSymbol union variant (just a char* name), not
            asAlias, so there are no child expressions to recurse into.
            Don't read asAlias fields here — they would alias with the
            name pointer and produce a bogus PHB_EXPR. */
         break;

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
            hb_refTabScanExpr( pTab, pStmt->value.asVar.pInit, pCtx );
            break;

         case HB_AST_PUBLIC:
            /* Register this PUBLIC in the reftab so cross-file callers
               (and the emitter in the owning file) can find it. First
               file to declare wins ownership. */
            if( pCtx && pCtx->szFileBase )
               hb_refTabMarkPublic( pTab, pStmt->value.asVar.szName,
                                    pCtx->szFileBase,
                                    pStmt->value.asVar.fArrayDim );
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
               ctx.szFileBase   = HB_COMP_PARAM->pFileName ?
                                     HB_COMP_PARAM->pFileName->szName : NULL;
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

               /* If this method body uses ::&(name) macro member access,
                  mark the enclosing class as dynamic so cross-file
                  callers emit references to it as `dynamic` (runtime
                  dispatch covers the arbitrary member names). */
               if( szClass && pFunc->value.asFunc.pBody &&
                   hb_refTabBlockHasMacroSend( pFunc->value.asFunc.pBody ) )
                  hb_refTabMarkClassDynamic( pTab, szClass );
            }
         }
         else if( pCompFunc && pFunc->value.asFunc.pBody )
         {
            /* Auto-generated startup function body. */
            HB_SCANCTX ctx;
            ctx.szFunc       = NULL;
            ctx.szClass      = NULL;
            ctx.szFileBase   = HB_COMP_PARAM->pFileName ?
                                  HB_COMP_PARAM->pFileName->szName : NULL;
            ctx.ppParamNames = NULL;
            ctx.nParams      = 0;
            ctx.fVariadic    = HB_FALSE;
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
                  hb_astPropagate( pFunc->value.asFunc.pBody, NULL, pTab, szKeyBuf );
               if( szRetType )
                  hb_refTabSetReturnType( pTab, szKeyBuf, szRetType );
            }
         }
         else if( pCompFunc && pFunc->value.asFunc.pBody )
         {
            /* Startup function — still walk for call-site refinement,
               but we're not interested in its return type. */
            hb_astPropagate( pFunc->value.asFunc.pBody, NULL, pTab, NULL );
         }

         if( pCompFunc )
            pCompFunc = pCompFunc->pNext;
      }
      pFunc = pFunc->pNext;
   }
}
