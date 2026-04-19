/*
 * Harbour Transpiler — canonical function-name map from .hbx files
 *
 * See include/hbhbxcanon.h for the API and rationale.
 *
 * Storage: a growable array of { szLower, szCanon } pairs, sorted
 * lazily on first lookup after any load. Lookups use bsearch. The
 * set is small (~2 000 entries for core + a few contribs) and
 * read-dominated once populated, so a flat sorted array is simpler
 * and more cache-friendly than a hash table.
 *
 * Copyright 2026 harbour.github.io
 */

#include "hbcomp.h"
#include "hbhbxcanon.h"
#include <ctype.h>
#include <dirent.h>
#include <sys/stat.h>

typedef struct
{
   char * szLower;   /* owned */
   char * szCanon;   /* owned */
} HB_HBX_ENTRY;

static HB_HBX_ENTRY * s_pEntries = NULL;
static HB_SIZE        s_nEntries = 0;
static HB_SIZE        s_nCap     = 0;
static HB_BOOL        s_fSorted  = HB_TRUE;  /* trivially true when empty */

static char * hb_hbxDupLower( const char * sz )
{
   HB_SIZE n = strlen( sz );
   char *  r = ( char * ) hb_xgrab( n + 1 );
   HB_SIZE i;
   for( i = 0; i < n; i++ )
      r[ i ] = ( char ) HB_TOLOWER( ( HB_UCHAR ) sz[ i ] );
   r[ n ] = '\0';
   return r;
}

static char * hb_hbxDup( const char * sz )
{
   HB_SIZE n = strlen( sz );
   char *  r = ( char * ) hb_xgrab( n + 1 );
   memcpy( r, sz, n + 1 );
   return r;
}

static void hb_hbxAppend( const char * szName )
{
   if( s_nEntries == s_nCap )
   {
      s_nCap = s_nCap ? s_nCap * 2 : 256;
      s_pEntries = ( HB_HBX_ENTRY * ) hb_xrealloc(
         s_pEntries, s_nCap * sizeof( HB_HBX_ENTRY ) );
   }
   s_pEntries[ s_nEntries ].szLower = hb_hbxDupLower( szName );
   s_pEntries[ s_nEntries ].szCanon = hb_hbxDup( szName );
   s_nEntries++;
   s_fSorted = HB_FALSE;
}

static int hb_hbxCmpByLower( const void * a, const void * b )
{
   const HB_HBX_ENTRY * ea = ( const HB_HBX_ENTRY * ) a;
   const HB_HBX_ENTRY * eb = ( const HB_HBX_ENTRY * ) b;
   return strcmp( ea->szLower, eb->szLower );
}

/* After qsort, adjacent rows with the same lowercased key are
   duplicates from the same or different hbx files. Keep the LAST
   occurrence (later loads win — lets a contrib file override core
   if both are loaded). */
static void hb_hbxDedup( void )
{
   HB_SIZE rd, wr = 0;
   if( s_nEntries < 2 )
      return;
   for( rd = 0; rd < s_nEntries; rd++ )
   {
      if( wr > 0 && strcmp( s_pEntries[ wr - 1 ].szLower,
                            s_pEntries[ rd     ].szLower ) == 0 )
      {
         /* Overwrite the prior entry with the later one. */
         hb_xfree( s_pEntries[ wr - 1 ].szLower );
         hb_xfree( s_pEntries[ wr - 1 ].szCanon );
         s_pEntries[ wr - 1 ] = s_pEntries[ rd ];
      }
      else
      {
         if( wr != rd )
            s_pEntries[ wr ] = s_pEntries[ rd ];
         wr++;
      }
   }
   s_nEntries = wr;
}

static void hb_hbxEnsureSorted( void )
{
   if( s_fSorted )
      return;
   if( s_nEntries > 1 )
   {
      qsort( s_pEntries, s_nEntries, sizeof( HB_HBX_ENTRY ),
             hb_hbxCmpByLower );
      hb_hbxDedup();
   }
   s_fSorted = HB_TRUE;
}

/* ---- public API ---- */

HB_BOOL hb_hbxCanonLoad( const char * szPath )
{
   FILE * fh;
   char   szLine[ 1024 ];

   if( ! szPath || ! *szPath )
      return HB_FALSE;
   fh = hb_fopen( szPath, "r" );
   if( ! fh )
      return HB_FALSE;

   while( fgets( szLine, sizeof( szLine ), fh ) )
   {
      char * p = szLine;
      char * szName;
      char * szEnd;

      /* Strip trailing newline(s). */
      szEnd = p + strlen( p );
      while( szEnd > p && ( szEnd[ -1 ] == '\n' || szEnd[ -1 ] == '\r' ) )
         *--szEnd = '\0';

      /* Leading whitespace. */
      while( *p == ' ' || *p == '\t' )
         p++;

      /* Only `DYNAMIC <Name>` lines are interesting. Everything else
         — comments, preprocessor directives, blank lines — ignored. */
      if( strncmp( p, "DYNAMIC", 7 ) != 0 ||
          ( p[ 7 ] != ' ' && p[ 7 ] != '\t' ) )
         continue;

      p += 7;
      while( *p == ' ' || *p == '\t' )
         p++;
      szName = p;

      /* Name ends at first whitespace/punctuation. hbx lines are bare
         identifiers, but guard against trailing-comment junk anyway. */
      while( *p && *p != ' ' && *p != '\t' && *p != ',' && *p != '/' )
         p++;
      *p = '\0';

      if( *szName )
         hb_hbxAppend( szName );
   }
   fclose( fh );
   return HB_TRUE;
}

/* Walk szDir (one level) and load every *.hbx file found. Used both
   for the canonical include/ directory and for the contrib parent
   where each .hbx sits one level down in its package subdir. The
   fRecurse flag turns on the second-level walk so contrib/<lib>/<lib>.hbx
   gets picked up when called with the contrib root. Returns the
   number of files successfully loaded (zero on missing dir is not
   an error — auto-load callers silently accept that). */
HB_SIZE hb_hbxCanonLoadDir( const char * szDir, HB_BOOL fRecurse )
{
   DIR *           dp;
   struct dirent * de;
   HB_SIZE         nLoaded = 0;
   char            szPath[ HB_PATH_MAX ];

   if( ! szDir || ! *szDir )
      return 0;
   dp = opendir( szDir );
   if( ! dp )
      return 0;

   while( ( de = readdir( dp ) ) != NULL )
   {
      struct stat st;
      HB_SIZE     nLen;

      if( de->d_name[ 0 ] == '.' )
         continue;
      hb_snprintf( szPath, sizeof( szPath ), "%s/%s", szDir, de->d_name );
      if( stat( szPath, &st ) != 0 )
         continue;
      if( S_ISDIR( st.st_mode ) )
      {
         if( fRecurse )
            nLoaded += hb_hbxCanonLoadDir( szPath, HB_FALSE );
         continue;
      }
      if( ! S_ISREG( st.st_mode ) )
         continue;
      nLen = strlen( de->d_name );
      if( nLen < 5 ||
          strcmp( de->d_name + nLen - 4, ".hbx" ) != 0 )
         continue;
      if( hb_hbxCanonLoad( szPath ) )
         nLoaded++;
   }
   closedir( dp );
   return nLoaded;
}

/* Auto-load all .hbx files from standard locations. Called once at
   transpiler startup. Searches two layouts:

   1. Install: /opt/harbour/include/harbour/*.hbx (core tags) plus
      /opt/harbour/contrib/<lib>/<lib>.hbx (each contrib package).
   2. Dev / cwd-relative: include/*.hbx plus contrib/<lib>/<lib>.hbx.

   Missing directories are silently skipped — the canonicaliser
   simply has fewer entries. Explicit --hbx=<path> still works for
   non-standard layouts and adds to whatever auto-load found. */
void hb_hbxCanonAutoLoad( void )
{
   /* Install layout (binary lives somewhere like /opt/harbour/bin). */
   hb_hbxCanonLoadDir( "/opt/harbour/include/harbour", HB_FALSE );
   hb_hbxCanonLoadDir( "/opt/harbour/contrib",         HB_TRUE  );

   /* Dev layout: cwd is the harbour-core repo root (scripts run
      from there, and the test suite cds there before invoking). */
   hb_hbxCanonLoadDir( "include",                      HB_FALSE );
   hb_hbxCanonLoadDir( "contrib",                      HB_TRUE  );
}

const char * hb_hbxCanonLookup( const char * szName )
{
   HB_HBX_ENTRY   key;
   HB_HBX_ENTRY * hit;
   char           szLowerBuf[ 128 ];
   HB_SIZE        i;

   if( ! szName || ! *szName || s_nEntries == 0 )
      return NULL;

   /* Stack-buffer lowercase copy avoids allocation on every lookup.
      Names longer than the buffer are rejected rather than truncated
      (silent truncation would cause phantom matches). */
   for( i = 0; szName[ i ] && i < sizeof( szLowerBuf ) - 1; i++ )
      szLowerBuf[ i ] = ( char ) HB_TOLOWER( ( HB_UCHAR ) szName[ i ] );
   if( szName[ i ] )
      return NULL;
   szLowerBuf[ i ] = '\0';

   hb_hbxEnsureSorted();
   key.szLower = szLowerBuf;
   key.szCanon = NULL;
   hit = ( HB_HBX_ENTRY * ) bsearch( &key, s_pEntries, s_nEntries,
                                     sizeof( HB_HBX_ENTRY ),
                                     hb_hbxCmpByLower );
   return hit ? hit->szCanon : NULL;
}

void hb_hbxCanonFree( void )
{
   HB_SIZE i;
   for( i = 0; i < s_nEntries; i++ )
   {
      hb_xfree( s_pEntries[ i ].szLower );
      hb_xfree( s_pEntries[ i ].szCanon );
   }
   if( s_pEntries )
      hb_xfree( s_pEntries );
   s_pEntries = NULL;
   s_nEntries = 0;
   s_nCap     = 0;
   s_fSorted  = HB_TRUE;
}
