/*
 * Harbour Transpiler - Function knowledge table loader
 *
 * Reads HB_FUNCTAB_PATH (defined in hbfunctab.h) into a small chained
 * hash table on first access.
 *
 * Copyright 2026 harbour.github.io
 */

#include "hbcomp.h"
#include "hbfunctab.h"

#define HB_FUNCTAB_BUCKETS  512  /* power of two */

typedef struct HB_FUNCENTRY_
{
   char *                 szName;       /* lowercased name (owned) */
   char *                 szPrefix;     /* namespace prefix or NULL (owned) */
   char *                 szRetType;    /* return type or NULL (owned) */
   struct HB_FUNCENTRY_ * pNext;
} HB_FUNCENTRY, * PHB_FUNCENTRY;

static PHB_FUNCENTRY s_buckets[ HB_FUNCTAB_BUCKETS ];
static HB_BOOL       s_fLoaded = HB_FALSE;

/* ---- helpers ---- */

static HB_SIZE hb_funcTabHash( const char * sz )
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

static char * hb_funcTabDupLower( const char * sz, HB_SIZE n )
{
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

static char * hb_funcTabDupUpper( const char * sz, HB_SIZE n )
{
   char *  r = ( char * ) hb_xgrab( n + 1 );
   HB_SIZE i;
   for( i = 0; i < n; i++ )
   {
      char c = sz[ i ];
      if( c >= 'a' && c <= 'z' )
         c = ( char ) ( c - ( 'a' - 'A' ) );
      r[ i ] = c;
   }
   r[ n ] = '\0';
   return r;
}

static char * hb_funcTabDup( const char * sz, HB_SIZE n )
{
   char * r = ( char * ) hb_xgrab( n + 1 );
   memcpy( r, sz, n );
   r[ n ] = '\0';
   return r;
}

static PHB_FUNCENTRY hb_funcTabFindEntry( const char * szName )
{
   HB_SIZE       slot;
   PHB_FUNCENTRY e;

   if( ! szName )
      return NULL;
   slot = hb_funcTabHash( szName ) & ( HB_FUNCTAB_BUCKETS - 1 );
   e    = s_buckets[ slot ];
   while( e )
   {
      if( hb_stricmp( e->szName, szName ) == 0 )
         return e;
      e = e->pNext;
   }
   return NULL;
}

/* Insert / update an entry. szPrefix and szRetType may be NULL. */
static void hb_funcTabInsert( const char * szName,
                              const char * szRetType,
                              const char * szPrefix )
{
   HB_SIZE       slot;
   PHB_FUNCENTRY e;

   if( ! szName || ! *szName )
      return;
   e = hb_funcTabFindEntry( szName );
   if( ! e )
   {
      slot = hb_funcTabHash( szName ) & ( HB_FUNCTAB_BUCKETS - 1 );
      e    = ( PHB_FUNCENTRY ) hb_xgrabz( sizeof( HB_FUNCENTRY ) );
      e->szName = hb_funcTabDupLower( szName, strlen( szName ) );
      e->pNext  = s_buckets[ slot ];
      s_buckets[ slot ] = e;
   }
   if( szRetType )
   {
      if( e->szRetType )
         hb_xfree( e->szRetType );
      e->szRetType = hb_funcTabDupUpper( szRetType, strlen( szRetType ) );
   }
   if( szPrefix )
   {
      if( e->szPrefix )
         hb_xfree( e->szPrefix );
      e->szPrefix = hb_funcTabDup( szPrefix, strlen( szPrefix ) );
   }
}

/* ---- parsing ---- */

/* Read one tab-separated field. Trims leading/trailing spaces and tabs.
   Sets *pnLen to the trimmed length and *ppNext to the start of the
   next field (one past the tab) or to the line terminator. */
static const char * hb_funcTabField( const char * sz, HB_SIZE * pnLen,
                                     const char ** ppNext )
{
   const char * p;
   const char * pEnd;

   while( *sz == ' ' || *sz == '\t' )
      sz++;
   p = sz;
   while( *p && *p != '\t' && *p != '\n' && *p != '\r' )
      p++;
   pEnd = p;
   while( pEnd > sz && ( pEnd[ -1 ] == ' ' || pEnd[ -1 ] == '\t' ) )
      pEnd--;
   *pnLen = ( HB_SIZE ) ( pEnd - sz );
   if( *p == '\t' )
      *ppNext = p + 1;
   else
      *ppNext = p;
   return sz;
}

static void hb_funcTabLoad( void )
{
   FILE * fp;
   char   line[ 512 ];

   if( s_fLoaded )
      return;
   s_fLoaded = HB_TRUE;  /* mark loaded even on failure to avoid retry */

   fp = hb_fopen( HB_FUNCTAB_PATH, "r" );
   if( ! fp )
   {
      fprintf( stderr, "hbtranspiler: warning: cannot open %s\n",
               HB_FUNCTAB_PATH );
      return;
   }

   while( fgets( line, sizeof( line ), fp ) )
   {
      const char * p = line;
      const char * pNext;
      const char * szName;
      const char * szRet;
      const char * szPfx;
      HB_SIZE      nName, nRet, nPfx;
      char         szNameBuf[ 64 ];
      char         szRetBuf[ 32 ];
      char         szPfxBuf[ 64 ];

      /* Skip leading whitespace */
      while( *p == ' ' || *p == '\t' )
         p++;
      if( *p == '#' || *p == '\n' || *p == '\r' || *p == '\0' )
         continue;

      szName = hb_funcTabField( p, &nName, &pNext );
      if( nName == 0 )
         continue;
      szRet = hb_funcTabField( pNext, &nRet, &pNext );
      szPfx = hb_funcTabField( pNext, &nPfx, &pNext );

      if( nName >= sizeof( szNameBuf ) ||
          nRet  >= sizeof( szRetBuf )  ||
          nPfx  >= sizeof( szPfxBuf )  )
         continue;

      memcpy( szNameBuf, szName, nName ); szNameBuf[ nName ] = '\0';
      memcpy( szRetBuf,  szRet,  nRet  ); szRetBuf[  nRet  ] = '\0';
      memcpy( szPfxBuf,  szPfx,  nPfx  ); szPfxBuf[  nPfx  ] = '\0';

      hb_funcTabInsert(
         szNameBuf,
         ( nRet > 0 && strcmp( szRetBuf, "-" ) != 0 ) ? szRetBuf : NULL,
         ( nPfx > 0 && strcmp( szPfxBuf, "-" ) != 0 ) ? szPfxBuf : NULL );
   }

   fclose( fp );
}

/* ---- public API ---- */

const char * hb_funcTabPrefix( const char * szName )
{
   PHB_FUNCENTRY e;
   hb_funcTabLoad();
   e = hb_funcTabFindEntry( szName );
   return e ? e->szPrefix : NULL;
}

const char * hb_funcTabReturnType( const char * szName )
{
   PHB_FUNCENTRY e;
   hb_funcTabLoad();
   e = hb_funcTabFindEntry( szName );
   return e ? e->szRetType : NULL;
}

void hb_funcTabFree( void )
{
   HB_SIZE i;
   for( i = 0; i < HB_FUNCTAB_BUCKETS; i++ )
   {
      PHB_FUNCENTRY e = s_buckets[ i ];
      while( e )
      {
         PHB_FUNCENTRY pNext = e->pNext;
         if( e->szName )    hb_xfree( e->szName );
         if( e->szPrefix )  hb_xfree( e->szPrefix );
         if( e->szRetType ) hb_xfree( e->szRetType );
         hb_xfree( e );
         e = pNext;
      }
      s_buckets[ i ] = NULL;
   }
   s_fLoaded = HB_FALSE;
}
