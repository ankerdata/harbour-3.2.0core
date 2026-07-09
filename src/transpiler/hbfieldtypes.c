/*
 * Harbour Transpiler - ORM def-class field-type map
 *
 * See include/hbfieldtypes.h for the API and file format.
 *
 * Copyright 2026 harbour.github.io
 */

#include "hbcomp.h"
#include "hbfieldtypes.h"

#define HB_FTMAP_BUCKETS  2048  /* power of two */

typedef struct HB_FTENTRY_
{
   char *               szKey;      /* "class\tmember" lowercased; member "" = class row — owned */
   char *               szCanon;    /* canonical spelling (class or member) — owned */
   char *               szType;     /* C# type token for member rows, NULL for class rows — owned */
   struct HB_FTENTRY_ * pNext;
} HB_FTENTRY, * PHB_FTENTRY;

static PHB_FTENTRY s_table[ HB_FTMAP_BUCKETS ];

/* Runtime path (set by --fieldtypes=<path>). Empty = no map; every
   lookup is a cheap NULL return and ORM receivers stay dynamic. */
static char s_szMapPath[ HB_PATH_MAX ] = { 0 };

static HB_BOOL s_fLoaded = HB_FALSE;


/* ---- string helpers ---- */

static HB_SIZE hb_ftHash( const char * sz )
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

static char * hb_ftDup( const char * sz )
{
   HB_SIZE n = strlen( sz );
   char *  r = ( char * ) hb_xgrab( n + 1 );
   memcpy( r, sz, n + 1 );
   return r;
}

static char * hb_ftKeyJoin( const char * szClass, const char * szMember )
{
   HB_SIZE nClass  = strlen( szClass );
   HB_SIZE nMember = szMember ? strlen( szMember ) : 0;
   char *  r       = ( char * ) hb_xgrab( nClass + nMember + 2 );
   HB_SIZE i;

   for( i = 0; i < nClass; i++ )
   {
      char c = szClass[ i ];
      if( c >= 'A' && c <= 'Z' )
         c = ( char ) ( c + ( 'a' - 'A' ) );
      r[ i ] = c;
   }
   r[ nClass ] = '\t';
   for( i = 0; i < nMember; i++ )
   {
      char c = szMember[ i ];
      if( c >= 'A' && c <= 'Z' )
         c = ( char ) ( c + ( 'a' - 'A' ) );
      r[ nClass + 1 + i ] = c;
   }
   r[ nClass + 1 + nMember ] = '\0';
   return r;
}


/* ---- table ops ---- */

static PHB_FTENTRY hb_ftFind( const char * szClass, const char * szMember )
{
   char *      szKey = hb_ftKeyJoin( szClass, szMember );
   HB_SIZE     slot  = hb_ftHash( szKey ) & ( HB_FTMAP_BUCKETS - 1 );
   PHB_FTENTRY e;

   for( e = s_table[ slot ]; e; e = e->pNext )
   {
      if( strcmp( e->szKey, szKey ) == 0 )
         break;
   }
   hb_xfree( szKey );
   return e;
}

static void hb_ftInsert( const char * szClass, const char * szMember,
                         const char * szCanon, const char * szType )
{
   char *      szKey = hb_ftKeyJoin( szClass, szMember );
   HB_SIZE     slot  = hb_ftHash( szKey ) & ( HB_FTMAP_BUCKETS - 1 );
   PHB_FTENTRY e;

   /* first writer wins — duplicate rows in the TSV are generator
      bugs, not something to silently re-resolve here */
   for( e = s_table[ slot ]; e; e = e->pNext )
   {
      if( strcmp( e->szKey, szKey ) == 0 )
      {
         hb_xfree( szKey );
         return;
      }
   }

   e = ( PHB_FTENTRY ) hb_xgrab( sizeof( HB_FTENTRY ) );
   e->szKey   = szKey;
   e->szCanon = hb_ftDup( szCanon );
   e->szType  = ( szType && *szType ) ? hb_ftDup( szType ) : NULL;
   e->pNext   = s_table[ slot ];
   s_table[ slot ] = e;
}


/* ---- public API ---- */

void hb_fieldTypesSetPath( const char * szPath )
{
   if( szPath && *szPath )
      hb_strncpy( s_szMapPath, szPath, sizeof( s_szMapPath ) - 1 );
   else
      s_szMapPath[ 0 ] = '\0';
   s_fLoaded = HB_FALSE;
}

const char * hb_fieldTypesGetPath( void )
{
   return s_szMapPath;
}

void hb_fieldTypesFree( void )
{
   HB_SIZE i;

   for( i = 0; i < HB_FTMAP_BUCKETS; i++ )
   {
      PHB_FTENTRY e = s_table[ i ];
      while( e )
      {
         PHB_FTENTRY pNext = e->pNext;
         hb_xfree( e->szKey );
         hb_xfree( e->szCanon );
         if( e->szType )
            hb_xfree( e->szType );
         hb_xfree( e );
         e = pNext;
      }
      s_table[ i ] = NULL;
   }
   s_fLoaded = HB_FALSE;
}

void hb_fieldTypesLoad( void )
{
   FILE * fp;
   char   szLine[ 1024 ];

   hb_fieldTypesFree();
   s_fLoaded = HB_TRUE;

   if( ! s_szMapPath[ 0 ] )
      return;

   fp = hb_fopen( s_szMapPath, "r" );
   if( ! fp )
      return;

   while( fgets( szLine, sizeof( szLine ), fp ) )
   {
      /* Class<TAB>accessor<TAB>cstype<TAB>... — extra columns ignored */
      char * szClass = szLine;
      char * szMember;
      char * szType;
      char * szEnd;

      if( szLine[ 0 ] == '#' || szLine[ 0 ] == '\n' || szLine[ 0 ] == '\0' )
         continue;

      szMember = strchr( szClass, '\t' );
      if( ! szMember )
         continue;
      *szMember++ = '\0';

      szType = strchr( szMember, '\t' );
      if( ! szType )
         continue;
      *szType++ = '\0';

      szEnd = strpbrk( szType, "\t\r\n" );
      if( szEnd )
         *szEnd = '\0';

      if( ! *szClass || ! *szMember || ! *szType )
         continue;

      /* class row (member "") registers the canonical class spelling;
         inserted once thanks to first-writer-wins */
      hb_ftInsert( szClass, "", szClass, NULL );
      hb_ftInsert( szClass, szMember, szMember, szType );
   }

   fclose( fp );
}

const char * hb_fieldTypesClassCanon( const char * szClass )
{
   PHB_FTENTRY e;

   if( ! szClass || ! *szClass )
      return NULL;
   if( ! s_fLoaded )
      hb_fieldTypesLoad();

   e = hb_ftFind( szClass, "" );
   return e ? e->szCanon : NULL;
}

const char * hb_fieldTypesMember( const char * szClass,
                                  const char * szMember,
                                  const char ** pszCanonMember )
{
   PHB_FTENTRY e;

   if( ! szClass || ! *szClass || ! szMember || ! *szMember )
      return NULL;
   if( ! s_fLoaded )
      hb_fieldTypesLoad();

   e = hb_ftFind( szClass, szMember );
   if( ! e || ! e->szType )
      return NULL;
   if( pszCanonMember )
      *pszCanonMember = e->szCanon;
   return e->szType;
}

const char * hb_fieldTypesHbType( const char * szCsToken )
{
   if( ! szCsToken )
      return NULL;
   if( hb_stricmp( szCsToken, "int" ) == 0 )
      return "INTEGER";
   if( hb_stricmp( szCsToken, "long" ) == 0 ||
       hb_stricmp( szCsToken, "decimal" ) == 0 )
      return "NUMERIC";
   if( hb_stricmp( szCsToken, "string" ) == 0 )
      return "STRING";
   if( hb_stricmp( szCsToken, "bool" ) == 0 )
      return "LOGICAL";
   if( hb_stricmp( szCsToken, "date" ) == 0 )
      return "DATE";
   if( hb_stricmp( szCsToken, "timestamp" ) == 0 )
      return "TIMESTAMP";
   return NULL;
}
