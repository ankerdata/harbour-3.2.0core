/*
 * Harbour Transpiler - Per-source-file #define class map
 *
 * See include/hbdefinemap.h for the API and file format.
 *
 * Copyright 2026 harbour.github.io
 */

#include "hbcomp.h"
#include "hbdefinemap.h"

#define HB_DEFMAP_BUCKETS  1024  /* power of two */

typedef struct HB_DEFENTRY_
{
   char *                szKey;       /* compound "owner\tname" (lowercased) — owned */
   char *                szCanonName; /* canonical-cased NAME for emission — owned */
   char *                szClass;     /* owning class name — owned */
   struct HB_DEFENTRY_ * pNext;
} HB_DEFENTRY, * PHB_DEFENTRY;

/* Two side-by-side tables: globals (owner == "") and locals (owner == basename).
   Kept separate so lookup can check the local bucket first without
   iterating past global noise. */
static PHB_DEFENTRY s_globals[ HB_DEFMAP_BUCKETS ];
static PHB_DEFENTRY s_locals [ HB_DEFMAP_BUCKETS ];

/* Runtime path override (set by --defines-map=<path>). Empty string
   means "no map configured" — lookups are a no-op. No default path:
   a transpile run without the flag behaves like the pre-map code. */
static char s_szMapPath[ HB_PATH_MAX ] = { 0 };

/* Basename of the source file currently being emitted (e.g.
   "deadbill.prg"). Lookups use it to decide if a local row applies. */
static char s_szCurrentFile[ HB_PATH_MAX ] = { 0 };

/* Set once the map file has been read in (or determined absent).
   Gates lazy loading inside the lookup path so callers don't have to
   orchestrate the load explicitly. Reset by hb_defineMapSetPath so a
   late path change picks up a fresh table. */
static HB_BOOL s_fLoaded = HB_FALSE;


/* ---- string helpers ---- */

static HB_SIZE hb_defMapHash( const char * sz )
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

static char * hb_defMapDup( const char * sz )
{
   HB_SIZE n = strlen( sz );
   char *  r = ( char * ) hb_xgrab( n + 1 );
   memcpy( r, sz, n + 1 );
   return r;
}

static char * hb_defMapDupLower( const char * sz )
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

static char * hb_defMapKeyJoin( const char * szOwner, const char * szName )
{
   HB_SIZE nOwner = szOwner ? strlen( szOwner ) : 0;
   HB_SIZE nName  = strlen( szName );
   char *  r      = ( char * ) hb_xgrab( nOwner + nName + 2 );
   HB_SIZE i;

   for( i = 0; i < nOwner; i++ )
   {
      char c = szOwner[ i ];
      if( c >= 'A' && c <= 'Z' )
         c = ( char ) ( c + ( 'a' - 'A' ) );
      r[ i ] = c;
   }
   r[ nOwner ] = '\t';
   for( i = 0; i < nName; i++ )
   {
      char c = szName[ i ];
      if( c >= 'A' && c <= 'Z' )
         c = ( char ) ( c + ( 'a' - 'A' ) );
      r[ nOwner + 1 + i ] = c;
   }
   r[ nOwner + 1 + nName ] = '\0';
   return r;
}


/* ---- table ops ---- */

static PHB_DEFENTRY hb_defMapFind( PHB_DEFENTRY * table, const char * szKey )
{
   HB_SIZE      slot = hb_defMapHash( szKey ) & ( HB_DEFMAP_BUCKETS - 1 );
   PHB_DEFENTRY e    = table[ slot ];

   while( e )
   {
      if( strcmp( e->szKey, szKey ) == 0 )
         return e;
      e = e->pNext;
   }
   return NULL;
}

static void hb_defMapInsert( PHB_DEFENTRY * table, const char * szOwner,
                             const char * szName, const char * szClass )
{
   char *       szKey = hb_defMapKeyJoin( szOwner, szName );
   HB_SIZE      slot  = hb_defMapHash( szKey ) & ( HB_DEFMAP_BUCKETS - 1 );
   PHB_DEFENTRY e;

   /* First-wins on duplicate keys. The map file is produced by
      gendefines.py which already resolves conflicts (header/header
      first-wins, globals separate from locals), so duplicates here
      are unexpected — silently keep the first and drop the rest. */
   for( e = table[ slot ]; e; e = e->pNext )
   {
      if( strcmp( e->szKey, szKey ) == 0 )
      {
         hb_xfree( szKey );
         return;
      }
   }

   e = ( PHB_DEFENTRY ) hb_xgrab( sizeof( HB_DEFENTRY ) );
   e->szKey       = szKey;
   e->szCanonName = hb_defMapDup( szName );
   e->szClass     = hb_defMapDup( szClass );
   e->pNext       = table[ slot ];
   table[ slot ]  = e;
}

static void hb_defMapClear( PHB_DEFENTRY * table )
{
   HB_SIZE i;
   for( i = 0; i < HB_DEFMAP_BUCKETS; i++ )
   {
      PHB_DEFENTRY e = table[ i ];
      while( e )
      {
         PHB_DEFENTRY next = e->pNext;
         hb_xfree( e->szKey );
         hb_xfree( e->szCanonName );
         hb_xfree( e->szClass );
         hb_xfree( e );
         e = next;
      }
      table[ i ] = NULL;
   }
}


/* ---- public API ---- */

void hb_defineMapSetPath( const char * szPath )
{
   s_fLoaded = HB_FALSE;
   if( ! szPath || ! *szPath )
   {
      s_szMapPath[ 0 ] = '\0';
      return;
   }
   hb_strncpy( s_szMapPath, szPath, sizeof( s_szMapPath ) - 1 );
}

const char * hb_defineMapGetPath( void )
{
   return s_szMapPath[ 0 ] ? s_szMapPath : NULL;
}

void hb_defineMapSetCurrentFile( const char * szBasename )
{
   if( ! szBasename || ! *szBasename )
   {
      s_szCurrentFile[ 0 ] = '\0';
      return;
   }
   hb_strncpy( s_szCurrentFile, szBasename, sizeof( s_szCurrentFile ) - 1 );
}

void hb_defineMapFree( void )
{
   hb_defMapClear( s_globals );
   hb_defMapClear( s_locals );
}

void hb_defineMapLoad( void )
{
   FILE * fh;
   char   szLine[ 2048 ];

   hb_defineMapFree();
   s_fLoaded = HB_TRUE;

   if( ! s_szMapPath[ 0 ] )
      return;

   fh = hb_fopen( s_szMapPath, "r" );
   if( ! fh )
      return;

   while( fgets( szLine, sizeof( szLine ), fh ) )
   {
      char * p     = szLine;
      char * szName;
      char * szClass;
      char * szOwner;
      char * szTab1;
      char * szTab2;
      char * szEnd;

      /* Trim trailing newline(s). */
      szEnd = szLine + strlen( szLine );
      while( szEnd > szLine && ( szEnd[ -1 ] == '\n' || szEnd[ -1 ] == '\r' ) )
         *--szEnd = '\0';

      if( ! *p || *p == '#' )
         continue;

      szName = p;
      szTab1 = strchr( p, '\t' );
      if( ! szTab1 )
         continue;
      *szTab1 = '\0';
      szClass = szTab1 + 1;
      szTab2 = strchr( szClass, '\t' );
      if( ! szTab2 )
      {
         /* Legacy two-column format = global. */
         szOwner = NULL;
      }
      else
      {
         *szTab2 = '\0';
         szOwner = szTab2 + 1;
         if( ! *szOwner )
            szOwner = NULL;
      }

      if( ! *szName || ! *szClass )
         continue;

      if( szOwner )
         hb_defMapInsert( s_locals, szOwner, szName, szClass );
      else
         hb_defMapInsert( s_globals, "", szName, szClass );
   }

   fclose( fh );
}


/* Internal: find entry in a given table by (owner, name). */
static PHB_DEFENTRY hb_defMapFindBy( PHB_DEFENTRY * table,
                                      const char * szOwner,
                                      const char * szName )
{
   char *       szKey = hb_defMapKeyJoin( szOwner, szName );
   PHB_DEFENTRY e     = hb_defMapFind( table, szKey );
   hb_xfree( szKey );
   return e;
}

const char * hb_defineMapLookup( const char * szName )
{
   PHB_DEFENTRY e;

   if( ! szName || ! *szName )
      return NULL;
   if( ! s_fLoaded )
      hb_defineMapLoad();

   if( s_szCurrentFile[ 0 ] )
   {
      e = hb_defMapFindBy( s_locals, s_szCurrentFile, szName );
      if( e )
         return e->szClass;
   }
   e = hb_defMapFindBy( s_globals, "", szName );
   return e ? e->szClass : NULL;
}

HB_BOOL hb_defineMapIsLocalOwned( const char * szName )
{
   if( ! szName || ! *szName || ! s_szCurrentFile[ 0 ] )
      return HB_FALSE;
   if( ! s_fLoaded )
      hb_defineMapLoad();
   return hb_defMapFindBy( s_locals, s_szCurrentFile, szName ) != NULL;
}
