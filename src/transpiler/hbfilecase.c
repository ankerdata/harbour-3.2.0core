/*
 * Harbour Transpiler - File-stem canonical-casing map
 *
 * See include/hbfilecase.h for the API and file format.
 *
 * Copyright 2026 harbour.github.io
 */

#include "hbcomp.h"
#include "hbfilecase.h"

#define HB_FILECASE_BUCKETS  256  /* power of two */

typedef struct HB_FILECASE_ENTRY_
{
   char *                       szStemLower; /* lowercased stem — owned */
   char *                       szCanon;     /* canonical CamelCase — owned */
   struct HB_FILECASE_ENTRY_ *  pNext;
} HB_FILECASE_ENTRY, * PHB_FILECASE_ENTRY;

static PHB_FILECASE_ENTRY s_table[ HB_FILECASE_BUCKETS ];
static char               s_szMapPath[ HB_PATH_MAX ] = { 0 };
static HB_BOOL            s_fLoaded = HB_FALSE;


static HB_SIZE hb_fcHash( const char * sz )
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

static char * hb_fcDup( const char * sz )
{
   HB_SIZE n = strlen( sz );
   char *  r = ( char * ) hb_xgrab( n + 1 );
   memcpy( r, sz, n + 1 );
   return r;
}

static char * hb_fcDupLower( const char * sz )
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

static void hb_fcInsert( const char * szStem, const char * szCanon )
{
   char *             szLower = hb_fcDupLower( szStem );
   HB_SIZE            iBucket = hb_fcHash( szLower ) & ( HB_FILECASE_BUCKETS - 1 );
   PHB_FILECASE_ENTRY p;

   /* Last-writer-wins on the lowercased stem so a manual override
      later in the file can replace an earlier auto-generated row. */
   for( p = s_table[ iBucket ]; p; p = p->pNext )
   {
      if( strcmp( p->szStemLower, szLower ) == 0 )
      {
         hb_xfree( szLower );
         hb_xfree( p->szCanon );
         p->szCanon = hb_fcDup( szCanon );
         return;
      }
   }
   p              = ( PHB_FILECASE_ENTRY ) hb_xgrab( sizeof( HB_FILECASE_ENTRY ) );
   p->szStemLower = szLower;
   p->szCanon     = hb_fcDup( szCanon );
   p->pNext       = s_table[ iBucket ];
   s_table[ iBucket ] = p;
}

void hb_fileCaseSetPath( const char * szPath )
{
   if( szPath )
      hb_strncpy( s_szMapPath, szPath, sizeof( s_szMapPath ) - 1 );
   else
      s_szMapPath[ 0 ] = '\0';
   /* Force reload next lookup. */
   hb_fileCaseFree();
   s_fLoaded = HB_FALSE;
}

const char * hb_fileCaseGetPath( void )
{
   return s_szMapPath;
}

static void hb_fcLoad( void )
{
   FILE * pf;
   char   szLine[ 512 ];

   s_fLoaded = HB_TRUE;
   if( s_szMapPath[ 0 ] == '\0' )
      return;

   pf = hb_fopen( s_szMapPath, "r" );
   if( ! pf )
      return;

   while( fgets( szLine, sizeof( szLine ), pf ) )
   {
      char * p = szLine;
      char * szStem;
      char * szCanon;
      char * szEnd;

      /* Skip leading whitespace */
      while( *p == ' ' || *p == '\t' )
         p++;
      /* Comments and blank lines */
      if( *p == '#' || *p == '\n' || *p == '\r' || *p == '\0' )
         continue;

      szStem = p;
      /* Stem ends at TAB or whitespace */
      while( *p && *p != '\t' && *p != ' ' )
         p++;
      if( ! *p )
         continue;   /* no separator → skip */
      *p++ = '\0';

      /* Skip separator whitespace */
      while( *p == ' ' || *p == '\t' )
         p++;
      szCanon = p;
      /* Canon ends at whitespace / newline */
      szEnd = p;
      while( *szEnd && *szEnd != '\n' && *szEnd != '\r' &&
             *szEnd != ' ' && *szEnd != '\t' )
         szEnd++;
      *szEnd = '\0';

      if( szStem[ 0 ] && szCanon[ 0 ] )
         hb_fcInsert( szStem, szCanon );
   }
   fclose( pf );
}

const char * hb_fileCaseLookup( const char * szStem )
{
   char *             szLower;
   HB_SIZE            iBucket;
   PHB_FILECASE_ENTRY p;
   const char *       szResult = NULL;

   if( ! szStem || ! *szStem )
      return NULL;
   if( ! s_fLoaded )
      hb_fcLoad();
   if( s_szMapPath[ 0 ] == '\0' )
      return NULL;

   szLower = hb_fcDupLower( szStem );
   iBucket = hb_fcHash( szLower ) & ( HB_FILECASE_BUCKETS - 1 );
   for( p = s_table[ iBucket ]; p; p = p->pNext )
   {
      if( strcmp( p->szStemLower, szLower ) == 0 )
      {
         szResult = p->szCanon;
         break;
      }
   }
   hb_xfree( szLower );
   return szResult;
}

void hb_fileCaseFree( void )
{
   int i;
   for( i = 0; i < HB_FILECASE_BUCKETS; i++ )
   {
      PHB_FILECASE_ENTRY p = s_table[ i ];
      while( p )
      {
         PHB_FILECASE_ENTRY pNext = p->pNext;
         hb_xfree( p->szStemLower );
         hb_xfree( p->szCanon );
         hb_xfree( p );
         p = pNext;
      }
      s_table[ i ] = NULL;
   }
}
