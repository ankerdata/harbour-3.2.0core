/*
 * Harbour Transpiler - Variable type-hint table loader
 *
 * Reads the file path set via hb_varTabSetPath() into a small
 * chained hash table on first access. Mirrors the hbfunctab.c
 * pattern; see hbvartypes.h for format and use.
 *
 * Copyright 2026 harbour.github.io
 */

#include "hbcomp.h"
#include "hbvartypes.h"

#define HB_VARTAB_BUCKETS  64        /* power of two — small list expected */

typedef struct HB_VARENTRY_
{
   char *                szName;     /* lowercased name (owned) */
   char *                szType;     /* canonical type, owned */
   struct HB_VARENTRY_ * pNext;
} HB_VARENTRY, * PHB_VARENTRY;

static PHB_VARENTRY s_buckets[ HB_VARTAB_BUCKETS ];
static HB_BOOL      s_fLoaded = HB_FALSE;
static char         s_szPath[ 512 ] = "";

/* ---- helpers ---- */

static HB_SIZE hb_varTabHash( const char * sz )
{
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

static char * hb_varTabDupLower( const char * sz, HB_SIZE n )
{
   char * r = ( char * ) hb_xgrab( n + 1 );
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

static char * hb_varTabDupUpper( const char * sz, HB_SIZE n )
{
   char * r = ( char * ) hb_xgrab( n + 1 );
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

static PHB_VARENTRY hb_varTabFindEntry( const char * szName )
{
   HB_SIZE      slot;
   PHB_VARENTRY e;

   if( ! szName )
      return NULL;
   slot = hb_varTabHash( szName ) & ( HB_VARTAB_BUCKETS - 1 );
   e    = s_buckets[ slot ];
   while( e )
   {
      if( hb_stricmp( e->szName, szName ) == 0 )
         return e;
      e = e->pNext;
   }
   return NULL;
}

static void hb_varTabInsert( const char * szName, const char * szType )
{
   HB_SIZE      slot;
   PHB_VARENTRY e;

   if( ! szName || ! *szName )
      return;
   if( hb_varTabFindEntry( szName ) )
      return;                                 /* first-wins */
   slot = hb_varTabHash( szName ) & ( HB_VARTAB_BUCKETS - 1 );
   e    = ( PHB_VARENTRY ) hb_xgrabz( sizeof( HB_VARENTRY ) );
   e->szName = hb_varTabDupLower( szName, strlen( szName ) );
   if( szType && *szType && strcmp( szType, "-" ) != 0 )
      e->szType = hb_varTabDupUpper( szType, strlen( szType ) );
   e->pNext  = s_buckets[ slot ];
   s_buckets[ slot ] = e;
}

/* ---- parsing ---- */

static const char * hb_varTabField( const char * sz, HB_SIZE * pnLen,
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

static void hb_varTabLoad( void )
{
   FILE * fp;
   char   line[ 256 ];

   if( s_fLoaded )
      return;
   s_fLoaded = HB_TRUE;
   if( ! s_szPath[ 0 ] )
      return;
   fp = hb_fopen( s_szPath, "r" );
   if( ! fp )
   {
      fprintf( stderr,
               "hbtranspiler: warning: cannot open --var-types file %s\n",
               s_szPath );
      return;
   }
   while( fgets( line, sizeof( line ), fp ) )
   {
      const char * p     = line;
      const char * pNext = NULL;
      HB_SIZE      nName = 0;
      HB_SIZE      nType = 0;
      const char * szName;
      const char * szType;
      char         szNameBuf[ 64 ];
      char         szTypeBuf[ 32 ];

      while( *p == ' ' || *p == '\t' )
         p++;
      if( *p == '\0' || *p == '\n' || *p == '\r' || *p == '#' )
         continue;

      szName = hb_varTabField( p, &nName, &pNext );
      if( nName == 0 || nName >= sizeof( szNameBuf ) )
         continue;
      memcpy( szNameBuf, szName, nName );
      szNameBuf[ nName ] = '\0';

      szType = hb_varTabField( pNext, &nType, &pNext );
      if( nType >= sizeof( szTypeBuf ) )
         nType = sizeof( szTypeBuf ) - 1;
      memcpy( szTypeBuf, szType, nType );
      szTypeBuf[ nType ] = '\0';

      hb_varTabInsert( szNameBuf, nType > 0 ? szTypeBuf : NULL );
   }
   fclose( fp );
}

/* ---- public API ---- */

void hb_varTabSetPath( const char * szPath )
{
   if( szPath && *szPath )
      hb_strncpy( s_szPath, szPath, sizeof( s_szPath ) - 1 );
   else
      s_szPath[ 0 ] = '\0';
   s_fLoaded = HB_FALSE;
}

const char * hb_varTabType( const char * szName )
{
   PHB_VARENTRY e;
   hb_varTabLoad();
   e = hb_varTabFindEntry( szName );
   return e ? e->szType : NULL;
}

void hb_varTabFree( void )
{
   HB_SIZE i;
   for( i = 0; i < HB_VARTAB_BUCKETS; i++ )
   {
      PHB_VARENTRY e = s_buckets[ i ];
      while( e )
      {
         PHB_VARENTRY pNext = e->pNext;
         if( e->szName ) hb_xfree( e->szName );
         if( e->szType ) hb_xfree( e->szType );
         hb_xfree( e );
         e = pNext;
      }
      s_buckets[ i ] = NULL;
   }
   s_fLoaded = HB_FALSE;
}
