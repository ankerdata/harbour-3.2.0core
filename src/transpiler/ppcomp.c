/*
 * Compiler C source with real code generation
 *
 * Copyright 2006 Przemyslaw Czerpak < druzus /at/ priv.onet.pl >
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation; either version 2, or (at your option)
 * any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program; see the file LICENSE.txt.  If not, write to
 * the Free Software Foundation, Inc., 51 Franklin Street, Fifth Floor,
 * Boston, MA 02110-1301 USA (or visit https://www.gnu.org/licenses/).
 *
 * As a special exception, the Harbour Project gives permission for
 * additional uses of the text contained in its release of Harbour.
 *
 * The exception is that, if you link the Harbour libraries with other
 * files to produce an executable, this does not by itself cause the
 * resulting executable to be covered by the GNU General Public License.
 * Your use of that executable is in no way restricted on account of
 * linking the Harbour library code into it.
 *
 * This exception does not however invalidate any other reasons why
 * the executable file might be covered by the GNU General Public License.
 *
 * This exception applies only to the code released by the Harbour
 * Project under the name Harbour.  If you copy code from other
 * Harbour Project or Free Software Foundation releases into a copy of
 * Harbour, as the General Public License permits, the exception does
 * not apply to the code that you add in this way.  To avoid misleading
 * anyone as to the status of such modified files, you must delete
 * this exception notice from them.
 *
 * If you write modifications of your own for Harbour, it is your choice
 * whether to permit this exception to apply to your modifications.
 * If you do not wish that, delete this exception notice.
 *
 */

#define _HB_PP_INTERNAL
#include "hbcomp.h"
#ifdef HB_TRANSPILER
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#if defined( HB_OS_WIN )
#  include <process.h>   /* _getpid for preload scratch files */
#else
#  include <unistd.h>    /* getpid for preload scratch files */
#endif
#include "hbast.h"
#include "hbdefinemap.h"

#if defined( _MSC_VER )
#  define HB_PRELOAD_GETPID()  _getpid()
#else
#  define HB_PRELOAD_GETPID()  getpid()
#endif

/* Directory for the preload scratch files. On Windows P_tmpdir is
   "\\" — the root of the current drive, which is normally not
   writable — so honour TEMP/TMP there instead. */
static const char * hb_compPreloadTmpDir( void )
{
#if defined( HB_OS_WIN )
   const char * pszDir = getenv( "TEMP" );

   if( ! pszDir || ! *pszDir )
      pszDir = getenv( "TMP" );
   return ( pszDir && *pszDir ) ? pszDir : ".";
#else
   return P_tmpdir ? P_tmpdir : "/tmp";
#endif
}

/* Optional path to a project-specific preload list. Set via
   --preload-list=<path> on the command line. The file names one
   header per line; each is fed to hb_pp_readRules after the baked-in
   std.ch + common.ch defaults, extending the standard rule set
   without patching the transpiler. Lines starting with '#' and
   blank lines are ignored. Missing files inside the list warn but
   don't abort the compile — "soft" is the point.

   While s_fPreloadSoftErrors is set, hb_pp_ErrorGen rewrites any
   non-warning errors raised by the preprocessor into warnings so a
   missing header file or a malformed preload doesn't take the whole
   compile down. Cleared immediately after the preload loop. */
static char    s_szPreloadListPath[ HB_PATH_MAX ] = { 0 };
static HB_BOOL s_fPreloadSoftErrors = HB_FALSE;

void hb_compPreloadListSetPath( const char * szPath )
{
   if( ! szPath || ! *szPath )
   {
      s_szPreloadListPath[ 0 ] = '\0';
      return;
   }
   hb_strncpy( s_szPreloadListPath, szPath,
               sizeof( s_szPreloadListPath ) - 1 );
}

const char * hb_compPreloadListGetPath( void )
{
   return s_szPreloadListPath[ 0 ] ? s_szPreloadListPath : NULL;
}

/* Find `szHeader` along pState's include path (or absolute). Writes
   the resolved path into szBuf. Returns HB_TRUE on hit. Used by the
   preload loader, which consumes headers directly rather than through
   hb_pp_readRules so it can filter to `#define` lines only. */
static HB_BOOL hb_compPreloadResolve( PHB_PP_STATE pState,
                                      const char * szHeader,
                                      char * szBuf, size_t nBufSize )
{
   FILE * fp;
   HB_PATHNAMES * pPath;
   PHB_FNAME pFileName;
   char szMerged[ HB_PATH_MAX ];

   if( ! szHeader || ! *szHeader )
      return HB_FALSE;

   /* Try the name as given — handles absolute and cwd-relative paths. */
   fp = hb_fopen( szHeader, "r" );
   if( fp )
   {
      fclose( fp );
      hb_strncpy( szBuf, szHeader, nBufSize - 1 );
      return HB_TRUE;
   }

   /* Fall through to the PP include-path walk. Default .ch extension
      matches hb_pp_readRules' behaviour — listing `hbcom` resolves
      the same as listing `hbcom.ch`. */
   pFileName = hb_fsFNameSplit( szHeader );
   if( ! pFileName->szExtension )
      pFileName->szExtension = ".ch";

   for( pPath = pState->pIncludePath; pPath; pPath = pPath->pNext )
   {
      pFileName->szPath = pPath->szPath;
      hb_fsFNameMerge( szMerged, pFileName );
      fp = hb_fopen( szMerged, "r" );
      if( fp )
      {
         fclose( fp );
         hb_strncpy( szBuf, szMerged, nBufSize - 1 );
         hb_xfree( pFileName );
         return HB_TRUE;
      }
   }

   hb_xfree( pFileName );
   return HB_FALSE;
}

/* Read szPath and write a filtered copy to szFilteredPath containing
   only the directives that make sense for a preload context:

     pass through:  #define NAME VALUE   (non-macro, and not a name the
                                          defines map knows: that one
                                          stays a name for the emitter)
                    #ifdef / #ifndef     (flow control)
                    #else / #elif / #endif
     dropped:       #xcommand / #xtranslate / #command / #translate
                    (rewrite rules — the whole reason the preload list
                     exists is to keep these out. A caller that wants
                     rule expansion should `#include` the header from
                     source normally; the transpiler's setNoInclude
                     means that's a no-op for emitted output anyway.)
                    #include   (chained preloads would pull in rewrite
                                rules by the back door)
                    #pragma
                    #define NAME(x) ...  (macro shape — becomes a
                                          rewrite rule in the PP)
                    body lines           (headers shouldn't have any)

   The PP itself evaluates our pass-through flow-control against its
   own define table, so we don't need to track `#ifdef` state here —
   just preserve the directives. Returns HB_FALSE if the source can't
   be opened. */
/* The identifier at p (after blanks) into szBuf, empty when there is none */
static void hb_compPreloadName( const char * p, char * szBuf, size_t nBufSize )
{
   size_t n = 0;
   while( *p == ' ' || *p == '\t' )
      p++;
   while( n + 1 < nBufSize &&
          ( ( *p >= 'A' && *p <= 'Z' ) || ( *p >= 'a' && *p <= 'z' ) ||
            ( *p >= '0' && *p <= '9' ) || *p == '_' ) )
      szBuf[ n++ ] = *p++;
   szBuf[ n ] = '\0';
}

#define HB_PRELOAD_MAXDEPTH 64

static HB_BOOL hb_compPreloadFilter( const char * szPath,
                                     const char * szFilteredPath )
{
   FILE * fpIn;
   FILE * fpOut;
   char   line[ 4096 ];
   /* The conditionals open around the current line, each an include
      guard (`#ifndef X` whose next directive is `#define X`) or not. A
      define the defines map knows is left to the map only where every
      open conditional is a guard: inside a real `#ifdef` the map holds
      whichever branch gendefines met first, and the preprocessor here
      picks the right one. */
   HB_BOOL afGuard[ HB_PRELOAD_MAXDEPTH ];
   int     iDepth = 0;
   char    szPendingGuard[ 128 ];   /* the last #ifndef's name, until the next directive */

   szPendingGuard[ 0 ] = '\0';
   fpIn = hb_fopen( szPath, "r" );
   if( ! fpIn )
      return HB_FALSE;
   fpOut = hb_fopen( szFilteredPath, "w" );
   if( ! fpOut )
   {
      fclose( fpIn );
      return HB_FALSE;
   }

   while( fgets( line, sizeof( line ), fpIn ) )
   {
      char * p = line;

      /* Non-directive lines are always dropped from a preload source:
         a .ch intended for include-expansion can have PROCEDURE bodies
         or free-floating expressions, but in a preload context those
         would reach hb_pp_tokenGet as top-level tokens and either
         produce junk rules or trip the tokenizer. Only directives
         survive the filter. */
      while( *p == ' ' || *p == '\t' )
         p++;
      if( *p != '#' )
         continue;
      p++;
      while( *p == ' ' || *p == '\t' )
         p++;

      /* Pass through flow-control and #define (non-macro); drop
         everything else. `#elif` isn't emitted by the Harbour PP for
         .ch rule files but is cheap to allow for forward-compat. */
      if( strncmp( p, "ifdef",  5 ) == 0 ||
          strncmp( p, "ifndef", 6 ) == 0 ||
          strncmp( p, "else",   4 ) == 0 ||
          strncmp( p, "elif",   4 ) == 0 ||
          strncmp( p, "endif",  5 ) == 0 )
      {
         if( strncmp( p, "endif", 5 ) == 0 )
         {
            if( iDepth > 0 )
               iDepth--;
         }
         else if( strncmp( p, "else", 4 ) == 0 || strncmp( p, "elif", 4 ) == 0 )
         {
            if( iDepth > 0 && iDepth <= HB_PRELOAD_MAXDEPTH )
               afGuard[ iDepth - 1 ] = HB_FALSE;
         }
         else
         {
            iDepth++;
            if( iDepth <= HB_PRELOAD_MAXDEPTH )
               afGuard[ iDepth - 1 ] = HB_FALSE;
         }
         szPendingGuard[ 0 ] = '\0';
         if( strncmp( p, "ifndef", 6 ) == 0 )
            hb_compPreloadName( p + 6, szPendingGuard, sizeof( szPendingGuard ) );
         fputs( line, fpOut );
         continue;
      }
      /* `#if <expr>` is not passed on, but opens a conditional all the same */
      if( strncmp( p, "if", 2 ) == 0 && ( p[ 2 ] == ' ' || p[ 2 ] == '\t' || p[ 2 ] == '(' ) )
      {
         iDepth++;
         if( iDepth <= HB_PRELOAD_MAXDEPTH )
            afGuard[ iDepth - 1 ] = HB_FALSE;
         szPendingGuard[ 0 ] = '\0';
         continue;
      }
      if( strncmp( p, "define", 6 ) == 0 &&
          ( p[ 6 ] == ' ' || p[ 6 ] == '\t' ) )
      {
         char * q = p + 6;
         char * szName;
         char   szDefName[ 128 ];
         while( *q == ' ' || *q == '\t' )
            q++;
         /* Skip the name to detect macro-shape `NAME(...)`. */
         szName = q;
         while( ( *q >= 'A' && *q <= 'Z' ) ||
                ( *q >= 'a' && *q <= 'z' ) ||
                ( *q >= '0' && *q <= '9' ) ||
                *q == '_' )
            q++;
         if( *q == '(' )
            continue;   /* macro define — the preload list forbids these */
         /* A constant the defines map knows (gendefines --header), defined
            outside any conditional but the include guard, stays a name,
            which the emitter writes as its const class's member
            (`ErrorConst.EG_OPEN`) and the inference types from the map;
            expanded here it would be a bare number. */
         if( q > szName && ( HB_SIZE ) ( q - szName ) < sizeof( szDefName ) )
         {
            HB_BOOL fGuarded = HB_TRUE;
            int i;
            memcpy( szDefName, szName, q - szName );
            szDefName[ q - szName ] = '\0';
            if( szPendingGuard[ 0 ] && iDepth > 0 && iDepth <= HB_PRELOAD_MAXDEPTH &&
                strcmp( szDefName, szPendingGuard ) == 0 )
               afGuard[ iDepth - 1 ] = HB_TRUE;
            szPendingGuard[ 0 ] = '\0';
            for( i = 0; i < iDepth; i++ )
               if( i >= HB_PRELOAD_MAXDEPTH || ! afGuard[ i ] )
                  fGuarded = HB_FALSE;
            if( fGuarded && hb_defineMapLookup( szDefName ) )
               continue;
         }
         fputs( line, fpOut );
         continue;
      }
      szPendingGuard[ 0 ] = '\0';
      /* Everything else dropped: #xcommand, #xtranslate, #command,
         #translate, #include, #pragma, #undef, ... */
   }

   fclose( fpIn );
   fclose( fpOut );
   return HB_TRUE;
}
#endif

static void hb_pp_ErrorGen( void * cargo,
                            const char * const szMsgTable[],
                            char cPrefix, int iErrorCode,
                            const char * szParam1, const char * szParam2 )
{
   HB_COMP_DECL = ( PHB_COMP ) cargo;
   int iCurrLine = HB_COMP_PARAM->currLine;
   const char * currModule = HB_COMP_PARAM->currModule;

   HB_COMP_PARAM->currLine = hb_pp_line( HB_COMP_PARAM->pLex->pPP );
   HB_COMP_PARAM->currModule = hb_pp_fileName( HB_COMP_PARAM->pLex->pPP );
#ifdef HB_TRANSPILER
   /* Soft mode (preload list): a missing header file or a malformed
      rule inside one should not abort the compile. Print a W0019
      summary directly so the user sees *which* preload entry failed,
      then swallow the error — don't call hb_compGenError, so
      iErrorCount stays at zero and the compile continues. The
      compiler warning channel isn't used here because PP error
      messages have no leading level digit (unlike PP warnings), and
      hb_compGenWarning would misread that as a malformed warning. */
   if( s_fPreloadSoftErrors && cPrefix != 'W' )
   {
      fprintf( stderr,
               "hbtranspiler: warning W0019  "
               "Preload rule load failed: %s\n",
               szParam1 ? szParam1 : "(unknown)" );
      HB_COMP_PARAM->fError = HB_FALSE;
      HB_COMP_PARAM->currLine = iCurrLine;
      HB_COMP_PARAM->currModule = currModule;
      return;
   }
#endif
   if( cPrefix == 'W' )
      hb_compGenWarning( HB_COMP_PARAM, szMsgTable, cPrefix, iErrorCode, szParam1, szParam2 );
   else
      hb_compGenError( HB_COMP_PARAM, szMsgTable, cPrefix, iErrorCode, szParam1, szParam2 );
   HB_COMP_PARAM->fError = HB_FALSE;
   HB_COMP_PARAM->currLine = iCurrLine;
   HB_COMP_PARAM->currModule = currModule;
}

static void hb_pp_Disp( void * cargo, const char * szMessage )
{
   HB_COMP_DECL = ( PHB_COMP ) cargo;

   hb_compOutStd( HB_COMP_PARAM, szMessage );
}

static void hb_pp_PragmaDump( void * cargo, char * pBuffer, HB_SIZE nSize,
                              int iLine )
{
   PHB_HINLINE pInline;

   pInline = hb_compInlineAdd( ( PHB_COMP ) cargo, NULL, iLine );
   pInline->pCode = ( HB_BYTE * ) hb_xgrab( nSize + 1 );
   memcpy( pInline->pCode, pBuffer, nSize );
   pInline->pCode[ nSize ] = '\0';
   pInline->nPCodeSize = nSize;
}

/* `#pragma BEGINCSHARP` … `#pragma ENDCSHARP`: the block's text becomes
   an HB_AST_CSHARP node. Where it goes depends on what it holds.

   C# allows only type declarations at namespace level, and none inside
   a method, so the text itself says which kind of block it is — which
   matters because position cannot: Harbour has no end-of-routine
   marker, so a block after a routine's last statement is "inside" it
   until the next routine begins. A block that opens with a type
   declaration (`public partial class Program { … }`) is FILE scope:
   appended beside the CLASS nodes, written out verbatim at namespace
   level. So is a block with no code at all, only comments (test105
   keeps one inside Main() on purpose). Any other block inside a routine
   is STATEMENTS: appended to the block the parser is filling, at the
   point it stands, and emitted there in the method body — the way to
   replace a guarded stretch of Harbour with its C# while 9.0 keeps the
   Harbour. See gencsharp.c. */
extern void hb_pp_setDumpCsFunc( PHB_PP_DUMP_FUNC pFunc );

/* Is the C# text a file-scope block? True when it opens with a type
   declaration — past whitespace, comments, attributes and modifiers, at
   `class`, `struct`, `interface`, `enum`, `record`, `delegate` or
   `namespace` — or when it holds nothing but comments. */
static HB_BOOL hb_pp_CSharpIsFileScope( const char * p )
{
   static const char * const s_szMods[] = {
      "public", "private", "protected", "internal", "static", "partial",
      "sealed", "abstract", "readonly", "unsafe", "file", "new", "ref", NULL };
   static const char * const s_szKinds[] = {
      "class", "struct", "interface", "enum", "record", "delegate",
      "namespace", NULL };

   for( ;; )
   {
      while( *p == ' ' || *p == '\t' || *p == '\r' || *p == '\n' )
         p++;
      if( ! *p )
         return HB_TRUE;                        /* comments only */
      if( p[ 0 ] == '/' && p[ 1 ] == '/' )
      {
         while( *p && *p != '\n' )
            p++;
      }
      else if( p[ 0 ] == '/' && p[ 1 ] == '*' )
      {
         const char * pEnd = strstr( p + 2, "*/" );
         p = pEnd ? pEnd + 2 : p + strlen( p );
      }
      else if( *p == '[' )                      /* [Attribute] */
      {
         while( *p && *p != ']' )
            p++;
         if( *p )
            p++;
      }
      else
      {
         const char * pWord = p;
         HB_SIZE nWord;
         int i;
         HB_BOOL fMod = HB_FALSE;

         while( ( *p >= 'a' && *p <= 'z' ) || ( *p >= 'A' && *p <= 'Z' ) )
            p++;
         nWord = ( HB_SIZE ) ( p - pWord );
         if( nWord == 0 )
            return HB_FALSE;
         for( i = 0; s_szKinds[ i ]; i++ )
            if( strlen( s_szKinds[ i ] ) == nWord &&
                strncmp( pWord, s_szKinds[ i ], nWord ) == 0 )
               return HB_TRUE;
         for( i = 0; s_szMods[ i ]; i++ )
            if( strlen( s_szMods[ i ] ) == nWord &&
                strncmp( pWord, s_szMods[ i ], nWord ) == 0 )
               fMod = HB_TRUE;
         if( ! fMod )
            return HB_FALSE;
      }
   }
}

static void hb_pp_PragmaCSharp( void * cargo, char * pBuffer, HB_SIZE nSize,
                                int iLine )
{
   HB_COMP_DECL = ( PHB_COMP ) cargo;
   PHB_AST_NODE pNode;
   char * szText;

   /* the state that answers conditionals (hb_compCondDefined) has no
      compile behind it: it reads directives, not code */
   if( ! HB_COMP_PARAM )
      return;
   pNode = hb_astNew( HB_AST_CSHARP, iLine );
   szText = ( char * ) hb_xgrab( nSize + 1 );

   memcpy( szText, pBuffer, nSize );
   szText[ nSize ] = '\0';
   pNode->value.asCSharp.szText =
      hb_compIdentifierNew( HB_COMP_PARAM, szText, HB_IDENT_COPY );
   if( HB_COMP_PARAM->ast.pCurrFunc != NULL && ! hb_pp_CSharpIsFileScope( szText ) )
   {
      pNode->value.asCSharp.fStatement = HB_TRUE;
      hb_astAppend( HB_COMP_PARAM, pNode );
   }
   else
      hb_astAppendToStartup( HB_COMP_PARAM, pNode );
   hb_xfree( szText );
}

static void hb_pp_hb_inLine( void * cargo, char * szFunc,
                             char * pBuffer, HB_SIZE nSize, int iLine )
{
   HB_COMP_DECL = ( PHB_COMP ) cargo;

   if( HB_COMP_PARAM->iLanguage != HB_LANG_C )
   {
      int iCurrLine = HB_COMP_PARAM->currLine;
      HB_COMP_PARAM->currLine = iLine;
      hb_compGenError( HB_COMP_PARAM, hb_comp_szErrors, 'F', HB_COMP_ERR_REQUIRES_C, NULL, NULL );
      HB_COMP_PARAM->fError = HB_FALSE;
      HB_COMP_PARAM->currLine = iCurrLine;
   }
   else
   {
      PHB_HINLINE pInline = hb_compInlineAdd( HB_COMP_PARAM,
         hb_compIdentifierNew( HB_COMP_PARAM, szFunc, HB_IDENT_COPY ), iLine );
      pInline->pCode = ( HB_BYTE * ) hb_xgrab( nSize + 1 );
      memcpy( pInline->pCode, pBuffer, nSize );
      pInline->pCode[ nSize ] = '\0';
      pInline->nPCodeSize = nSize;
   }
}

static HB_BOOL hb_pp_CompilerSwitch( void * cargo, const char * szSwitch,
                                     int * piValue, HB_BOOL fSet )
{
   HB_COMP_DECL = ( PHB_COMP ) cargo;
   HB_BOOL fError = HB_FALSE;
   int iValue, i;

   iValue = *piValue;

   i = ( int ) strlen( szSwitch );
   if( i > 1 && ( ( int ) ( szSwitch[ i - 1 ] - '0' ) ) == iValue )
      --i;

   if( i == 1 )
   {
      switch( szSwitch[ 0 ] )
      {
         case 'a':
         case 'A':
            if( fSet )
               HB_COMP_PARAM->fAutoMemvarAssume = iValue != 0;
            else
               iValue = HB_COMP_PARAM->fAutoMemvarAssume ? 1 : 0;
            break;

         case 'b':
         case 'B':
            if( fSet )
               HB_COMP_PARAM->fDebugInfo = iValue != 0;
            else
               iValue = HB_COMP_PARAM->fDebugInfo ? 1 : 0;
            break;

         case 'j':
         case 'J':
            if( fSet )
               HB_COMP_PARAM->fI18n = iValue != 0;
            else
               iValue = HB_COMP_PARAM->fI18n ? 1 : 0;
            break;

         case 'l':
         case 'L':
            if( fSet )
               HB_COMP_PARAM->fLineNumbers = iValue != 0;
            else
               iValue = HB_COMP_PARAM->fLineNumbers ? 1 : 0;
            break;

         case 'n':
         case 'N':
            if( fSet )
               fError = HB_TRUE;
            else
               iValue = HB_COMP_PARAM->iStartProc;
            break;

         case 'p':
         case 'P':
            if( fSet )
               HB_COMP_PARAM->fPPO = iValue != 0;
            else
               iValue = HB_COMP_PARAM->fPPO ? 1 : 0;
            break;

         case 'q':
         case 'Q':
            if( fSet )
               HB_COMP_PARAM->fQuiet = iValue != 0;
            else
               iValue = HB_COMP_PARAM->fQuiet ? 1 : 0;
            break;

         case 'v':
         case 'V':
            if( fSet )
               HB_COMP_PARAM->fForceMemvars = iValue != 0;
            else
               iValue = HB_COMP_PARAM->fForceMemvars ? 1 : 0;
            break;

         case 'w':
         case 'W':
            if( fSet )
            {
               if( iValue >= 0 && iValue <= 3 )
                  HB_COMP_PARAM->iWarnings = iValue;
               else
                  fError = HB_TRUE;
            }
            else
               iValue = HB_COMP_PARAM->iWarnings;
            break;

         case 'z':
         case 'Z':
            if( fSet )
            {
               if( iValue )
                  HB_COMP_PARAM->supported &= ~HB_COMPFLAG_SHORTCUTS;
               else
                  HB_COMP_PARAM->supported |= HB_COMPFLAG_SHORTCUTS;
            }
            else
               iValue = ( HB_COMP_PARAM->supported & HB_COMPFLAG_SHORTCUTS ) ? 0 : 1;
            break;

         default:
            fError = HB_TRUE;
      }
   }
   else if( i == 2 )
   {
      if( szSwitch[ 0 ] == 'k' || szSwitch[ 0 ] == 'K' )
      {
         int iFlag = 0;
         /* -k? parameters are case sensitive */
         switch( szSwitch[ 1 ] )
         {
            case '?':
               if( fSet )
                  HB_COMP_PARAM->supported = iValue;
               else
                  iValue = HB_COMP_PARAM->supported;
               break;
            case 'c':
            case 'C':
               if( fSet )
               {
                  /* clear all flags - minimal set of features */
                  HB_COMP_PARAM->supported &= HB_COMPFLAG_SHORTCUTS;
                  HB_COMP_PARAM->supported |= HB_COMPFLAG_OPTJUMP |
                                              HB_COMPFLAG_MACROTEXT;
               }
               else
               {
                  iValue = ( HB_COMP_PARAM->supported & ~HB_COMPFLAG_SHORTCUTS ) ==
                           ( HB_COMPFLAG_OPTJUMP | HB_COMPFLAG_MACROTEXT ) ? 1 : 0;
               }
               break;
            case 'h':
            case 'H':
               iFlag = HB_COMPFLAG_HARBOUR;
               break;
            case 'o':
            case 'O':
               iFlag = HB_COMPFLAG_EXTOPT;
               break;
            case 'i':
            case 'I':
               iFlag = HB_COMPFLAG_HB_INLINE;
               break;
            case 'r':
            case 'R':
               iFlag = HB_COMPFLAG_RT_MACRO;
               break;
            case 'x':
            case 'X':
               iFlag = HB_COMPFLAG_XBASE;
               break;
            case 'j':
            case 'J':
               iFlag = HB_COMPFLAG_OPTJUMP;
               iValue = ! iValue;
               break;
            case 'm':
            case 'M':
               iFlag = HB_COMPFLAG_MACROTEXT;
               iValue = ! iValue;
               break;
            case 'd':
            case 'D':
               iFlag = HB_COMPFLAG_MACRODECL;
               break;
            case 's':
            case 'S':
               iFlag = HB_COMPFLAG_ARRSTR;
               break;
            default:
               fError = HB_TRUE;
         }
         if( ! fError && iFlag )
         {
            if( fSet )
            {
               if( iValue )
                  HB_COMP_PARAM->supported |= iFlag;
               else
                  HB_COMP_PARAM->supported &= ~iFlag;
            }
            else
            {
               if( iValue )
                  iValue = ( HB_COMP_PARAM->supported & iFlag ) ? 0 : 1;
               else
                  iValue = ( HB_COMP_PARAM->supported & iFlag ) ? 1 : 0;
            }
         }
      }
      else if( hb_strnicmp( szSwitch, "gc", 2 ) == 0 )
      {
         if( fSet )
         {
            if( iValue == HB_COMPGENC_REALCODE ||
                iValue == HB_COMPGENC_VERBOSE ||
                iValue == HB_COMPGENC_NORMAL ||
                iValue == HB_COMPGENC_COMPACT )
               HB_COMP_PARAM->iGenCOutput = iValue;
         }
         else
            iValue = HB_COMP_PARAM->iGenCOutput;
      }
      else if( hb_strnicmp( szSwitch, "es", 2 ) == 0 )
      {
         if( fSet )
         {
            if( iValue == HB_EXITLEVEL_DEFAULT ||
                iValue == HB_EXITLEVEL_SETEXIT ||
                iValue == HB_EXITLEVEL_DELTARGET )
               HB_COMP_PARAM->iExitLevel = iValue;
         }
         else
            iValue = HB_COMP_PARAM->iExitLevel;
      }
      else if( hb_stricmp( szSwitch, "p+" ) == 0 )
      {
         if( fSet )
            HB_COMP_PARAM->fPPT = iValue != 0;
         else
            iValue = HB_COMP_PARAM->fPPT ? 1 : 0;
      }
      else
         fError = HB_TRUE;
   }
   /* xHarbour extension */
   else if( i >= 4 && hb_strnicmp( szSwitch, "TEXTHIDDEN", i ) == 0 )
   {
      if( fSet )
      {
         if( iValue >= 0 && iValue <= 1 )
            HB_COMP_PARAM->iHidden = iValue;
      }
      else
         iValue = HB_COMP_PARAM->iHidden;
   }
   else
      fError = HB_TRUE;

   *piValue = iValue;

   return fError;
}

static void hb_pp_fileIncluded( void * cargo, const char * szFileName )
{
   HB_COMP_DECL = ( PHB_COMP ) cargo;
   PHB_INCLST pIncFile, * pIncFilePtr;
   int iLen;

   pIncFilePtr = &HB_COMP_PARAM->incfiles;
   while( *pIncFilePtr )
   {
#if defined( HB_OS_UNIX )
      if( strcmp( ( *pIncFilePtr )->szFileName, szFileName ) == 0 )
         return;
#else
      if( hb_stricmp( ( *pIncFilePtr )->szFileName, szFileName ) == 0 )
         return;
#endif
      pIncFilePtr = &( *pIncFilePtr )->pNext;
   }

   iLen = ( int ) strlen( szFileName );
   pIncFile = ( PHB_INCLST ) hb_xgrab( sizeof( HB_INCLST ) + iLen );
   pIncFile->pNext = NULL;
   memcpy( pIncFile->szFileName, szFileName, iLen + 1 );
   *pIncFilePtr = pIncFile;
}

#ifdef HB_TRANSPILER
/* `#ifdef`, `#ifndef` and `defined()` as Harbour answers them.

   The compile's preprocessor registers none of the file's own #defines
   (hb_pp_directiveCapture() keeps them for the emitter, which writes the
   names) and opens none of its #includes, so on its own it would take
   every name they define for undefined, where Harbour knows them. It
   hands such a name to hb_compCondDefined(), which answers from a second
   preprocessor state that does open includes: the file's captured
   #define, #undef and #include lines are logged in order, and replayed
   into that state when a question comes, so it reads each header as
   Harbour does, its own conditionals included, and the answer is
   Harbour's. A question that state cannot settle because a header it
   was given could not be read is W0042; the header is not on the
   transpiler's include path. The log is per file (hb_compCondReset()),
   and a file that asks nothing never builds the state. */
typedef HB_BOOL ( * PHB_PP_CONDDEF_FUNC )( PHB_PP_STATE pState, const char * szName );
extern void    hb_pp_setCondDefFunc( PHB_PP_CONDDEF_FUNC pFunc );
extern void    hb_pp_readRulesBuffer( PHB_PP_STATE pState, const char * szFileName,
                                      const char * pBuffer, HB_SIZE nLen );
extern HB_BOOL hb_pp_isDefinedName( PHB_PP_STATE pState, const char * szName );
extern void    hb_pp_copySearchPath( PHB_PP_STATE pDst, PHB_PP_STATE pSrc );

static PHB_PP_STATE s_pCondMain   = NULL;   /* the compile's preprocessor */
static PHB_PP_STATE s_pCondHeader = NULL;   /* the one that opens includes */
static char *  s_pCondLog       = NULL;     /* the captured lines, in order */
static HB_SIZE s_nCondLog       = 0;
static HB_SIZE s_nCondLogAlloc  = 0;
static HB_SIZE s_nCondReplayed  = 0;
static char    s_szCondUnread[ 256 ];       /* a header it could not read */

static void hb_compCondLog( const char * szLine1, const char * szLine2,
                            const char * szLine3 )
{
   HB_SIZE n1 = strlen( szLine1 ), n2 = strlen( szLine2 ), n3 = strlen( szLine3 );

   if( s_nCondLog + n1 + n2 + n3 + 2 > s_nCondLogAlloc )
   {
      s_nCondLogAlloc = ( s_nCondLog + n1 + n2 + n3 + 2 ) * 2 + 256;
      s_pCondLog = ( char * ) hb_xrealloc( s_pCondLog, s_nCondLogAlloc );
   }
   memcpy( s_pCondLog + s_nCondLog, szLine1, n1 );
   memcpy( s_pCondLog + s_nCondLog + n1, szLine2, n2 );
   memcpy( s_pCondLog + s_nCondLog + n1 + n2, szLine3, n3 );
   s_nCondLog += n1 + n2 + n3;
   s_pCondLog[ s_nCondLog++ ] = '\n';
   s_pCondLog[ s_nCondLog ] = '\0';
}

/* The header state's errors are not the compile's: a header it cannot
   open is noted for W0042, anything else is the compile's own to report
   when it reaches the code. */
static void hb_compCondError( void * cargo, const char * const szMsgTable[],
                              char cPrefix, int iErrorCode,
                              const char * szParam1, const char * szParam2 )
{
   HB_SYMBOL_UNUSED( cargo );
   HB_SYMBOL_UNUSED( szMsgTable );
   HB_SYMBOL_UNUSED( iErrorCode );
   HB_SYMBOL_UNUSED( szParam2 );
   if( cPrefix == 'F' && szParam1 && ! s_szCondUnread[ 0 ] )
      hb_strncpy( s_szCondUnread, szParam1, sizeof( s_szCondUnread ) - 1 );
}

static void hb_compCondDisp( void * cargo, const char * szMessage )
{
   HB_SYMBOL_UNUSED( cargo );
   HB_SYMBOL_UNUSED( szMessage );
}

/* What a header could define, read from its text once per process (a
   header does not change while it runs): the name of every #define in
   it, in any branch, and every #include. Most questions are about names
   nothing defines (debug switches), and replaying a file's headers into
   the header state for each would double the scan; a name that neither
   the file nor any header it includes could define is undefined without
   it. Any other question still goes to the header state, which reads the
   conditionals. */
typedef struct _HB_CONDHDR
{
   char *  szPath;                /* as resolved */
   char *  szNames;               /* "\nNAME\nNAME\n": every #define */
   char *  szIncludes;            /* "\nname\nname\n": every #include */
   struct _HB_CONDHDR * pNext;
} HB_CONDHDR, * PHB_CONDHDR;

static PHB_CONDHDR s_pCondHeaders = NULL;

#define HB_COND_MAXSEEN  256

/* p at a directive's word (after `#` and blanks), or NULL */
static const char * hb_compCondDirective( const char * p, const char * szWord )
{
   HB_SIZE nLen = strlen( szWord );

   while( *p == ' ' || *p == '\t' )
      p++;
   if( *p++ != '#' )
      return NULL;
   while( *p == ' ' || *p == '\t' )
      p++;
   if( hb_strnicmp( p, szWord, nLen ) != 0 ||
       ( p[ nLen ] != ' ' && p[ nLen ] != '\t' && p[ nLen ] != '"' && p[ nLen ] != '<' ) )
      return NULL;
   p += nLen;
   while( *p == ' ' || *p == '\t' )
      p++;
   return p;
}

/* "\n" + the word at p + "\n" appended to *pszList */
static void hb_compCondListAdd( char ** pszList, const char * p, HB_SIZE nLen )
{
   HB_SIZE nOld = *pszList ? strlen( *pszList ) : 0;

   *pszList = ( char * ) hb_xrealloc( *pszList, nOld + nLen + 3 );
   if( nOld == 0 )
      ( *pszList )[ nOld++ ] = '\n';
   memcpy( *pszList + nOld, p, nLen );
   ( *pszList )[ nOld + nLen ] = '\n';
   ( *pszList )[ nOld + nLen + 1 ] = '\0';
}

static HB_BOOL hb_compCondListHas( const char * szList, const char * szWord )
{
   HB_SIZE nLen = strlen( szWord );
   const char * p = szList;

   while( p && ( p = strstr( p, szWord ) ) != NULL )
   {
      if( p > szList && p[ -1 ] == '\n' && p[ nLen ] == '\n' )
         return HB_TRUE;
      p++;
   }
   return HB_FALSE;
}

/* An #include's file, where the preprocessor looks for it: a quoted one
   beside the source file first, then along the search path */
static HB_BOOL hb_compCondResolve( const char * szName, HB_BOOL fQuoted,
                                   char * szPath, HB_SIZE nSize )
{
   HB_PATHNAMES * pPath;

   if( fQuoted && s_pCondMain->pFile && s_pCondMain->pFile->szFileName )
   {
      PHB_FNAME pSource = hb_fsFNameSplit( s_pCondMain->pFile->szFileName );
      hb_snprintf( szPath, nSize, "%s%s", pSource->szPath ? pSource->szPath : "", szName );
      hb_xfree( pSource );
      if( hb_fsFileExists( szPath ) )
         return HB_TRUE;
   }
   for( pPath = s_pCondMain->pIncludePath; pPath; pPath = pPath->pNext )
   {
      HB_SIZE nDir = strlen( pPath->szPath );
      hb_snprintf( szPath, nSize, "%s%s%s", pPath->szPath,
                   nDir && strchr( HB_OS_PATH_DELIM_CHR_LIST, pPath->szPath[ nDir - 1 ] ) ?
                   "" : HB_OS_PATH_DELIM_CHR_STRING, szName );
      if( hb_fsFileExists( szPath ) )
         return HB_TRUE;
   }
   return HB_FALSE;
}

/* The header at szPath, read once */
static PHB_CONDHDR hb_compCondHeader( const char * szPath )
{
   PHB_CONDHDR pHdr;
   FILE * fp;
   char szLine[ 4096 ];

   for( pHdr = s_pCondHeaders; pHdr; pHdr = pHdr->pNext )
      if( strcmp( pHdr->szPath, szPath ) == 0 )
         return pHdr;

   pHdr = ( PHB_CONDHDR ) hb_xgrabz( sizeof( HB_CONDHDR ) );
   pHdr->szPath = hb_strdup( szPath );
   pHdr->pNext = s_pCondHeaders;
   s_pCondHeaders = pHdr;

   fp = hb_fopen( szPath, "r" );
   if( fp )
   {
      while( fgets( szLine, sizeof( szLine ), fp ) )
      {
         const char * p;
         if( ( p = hb_compCondDirective( szLine, "define" ) ) != NULL )
         {
            const char * q = p;
            while( HB_ISNEXTIDCHAR( *q ) )
               q++;
            if( q > p )
               hb_compCondListAdd( &pHdr->szNames, p, q - p );
         }
         else if( ( p = hb_compCondDirective( szLine, "include" ) ) != NULL &&
                  ( *p == '"' || *p == '<' ) )
         {
            /* the quote kept in front, so its search is the right one */
            const char * q = strchr( p + 1, *p == '"' ? '"' : '>' );
            if( q && q > p + 1 )
               hb_compCondListAdd( &pHdr->szIncludes, p, q - p );
         }
      }
      fclose( fp );
   }
   return pHdr;
}

/* Where each #include resolved, once per process: the file probes are what
   a question costs otherwise. Keyed by the include as written (its quote
   or < in front) and, for a quoted one, the source file's directory. */
typedef struct _HB_CONDRES
{
   char *      szKey;
   PHB_CONDHDR pHdr;              /* NULL: not found */
   struct _HB_CONDRES * pNext;
} HB_CONDRES, * PHB_CONDRES;

static PHB_CONDRES s_pCondResolved = NULL;

static PHB_CONDHDR hb_compCondInclude( const char * szInclude, HB_SIZE nLen )
{
   char szKey[ HB_PATH_MAX * 2 ];
   char szName[ HB_PATH_MAX ];
   char szPath[ HB_PATH_MAX ];
   PHB_CONDRES pRes;
   HB_BOOL fQuoted = szInclude[ 0 ] == '"';
   PHB_FNAME pSource = NULL;

   if( nLen < 2 || nLen >= sizeof( szName ) )
      return NULL;
   memcpy( szName, szInclude + 1, nLen - 1 );
   szName[ nLen - 1 ] = '\0';
   if( fQuoted && s_pCondMain->pFile && s_pCondMain->pFile->szFileName )
      pSource = hb_fsFNameSplit( s_pCondMain->pFile->szFileName );
   hb_snprintf( szKey, sizeof( szKey ), "%c%s|%s", szInclude[ 0 ], szName,
                pSource && pSource->szPath ? pSource->szPath : "" );
   if( pSource )
      hb_xfree( pSource );
   for( pRes = s_pCondResolved; pRes; pRes = pRes->pNext )
      if( strcmp( pRes->szKey, szKey ) == 0 )
         return pRes->pHdr;

   pRes = ( PHB_CONDRES ) hb_xgrab( sizeof( HB_CONDRES ) );
   pRes->szKey = hb_strdup( szKey );
   pRes->pHdr = hb_compCondResolve( szName, fQuoted, szPath, sizeof( szPath ) ) ?
                hb_compCondHeader( szPath ) : NULL;
   pRes->pNext = s_pCondResolved;
   s_pCondResolved = pRes;
   return pRes->pHdr;
}

/* The headers the file's #includes reach, nested ones too, worked out
   again only when the log has grown */
static PHB_CONDHDR s_aCondReach[ HB_COND_MAXSEEN ];
static int         s_iCondReach = 0;
static HB_BOOL     s_fCondReachLost = HB_FALSE;  /* one not found, or too many */
static HB_SIZE     s_nCondReachLog = ( HB_SIZE ) -1;

/* add the headers szIncludes (one per line, its quote or < in front)
   names, and the ones they include */
static void hb_compCondReachAdd( const char * szIncludes )
{
   const char * p = szIncludes;

   while( p && *p )
   {
      const char * q;
      PHB_CONDHDR pHdr;
      int i;

      if( *p == '\n' )
      {
         p++;
         continue;
      }
      q = strchr( p, '\n' );
      if( ! q )
         break;
      pHdr = hb_compCondInclude( p, q - p );
      p = q;
      if( ! pHdr )
      {
         s_fCondReachLost = HB_TRUE;
         continue;
      }
      for( i = 0; i < s_iCondReach && s_aCondReach[ i ] != pHdr; i++ )
         ;
      if( i < s_iCondReach )
         continue;
      if( s_iCondReach >= HB_COND_MAXSEEN )
      {
         s_fCondReachLost = HB_TRUE;
         return;
      }
      s_aCondReach[ s_iCondReach++ ] = pHdr;
      hb_compCondReachAdd( pHdr->szIncludes );
   }
}

/* HB_TRUE when neither the file (its logged #defines) nor any header it
   has included could define szName, so it is surely undefined */
static HB_BOOL hb_compCondSurelyUndefined( const char * szName )
{
   const char * p;
   HB_SIZE n = strlen( szName );
   int i;

   if( s_nCondReachLog != s_nCondLog )
   {
      char * szIncludes = NULL;

      s_iCondReach = 0;
      s_fCondReachLost = HB_FALSE;
      for( p = s_pCondLog; p && *p; )
      {
         const char * q = strchr( p, '\n' );
         const char * r;
         if( ! q )
            break;
         if( ( r = hb_compCondDirective( p, "include" ) ) != NULL && *r == '"' )
         {
            const char * e = strchr( r + 1, '"' );
            if( e && e < q )
               hb_compCondListAdd( &szIncludes, r, e - r );
         }
         p = q + 1;
      }
      if( szIncludes )
      {
         hb_compCondReachAdd( szIncludes );
         hb_xfree( szIncludes );
      }
      s_nCondReachLog = s_nCondLog;
   }
   if( s_fCondReachLost )
      return HB_FALSE;

   for( p = s_pCondLog; p && *p; )
   {
      const char * q = strchr( p, '\n' );
      const char * r;
      if( ! q )
         break;
      if( ( r = hb_compCondDirective( p, "define" ) ) != NULL &&
          strncmp( r, szName, n ) == 0 && ! HB_ISNEXTIDCHAR( r[ n ] ) )
         return HB_FALSE;
      p = q + 1;
   }
   for( i = 0; i < s_iCondReach; i++ )
      if( hb_compCondListHas( s_aCondReach[ i ]->szNames, szName ) )
         return HB_FALSE;
   return HB_TRUE;
}

static HB_BOOL hb_compCondDefined( PHB_PP_STATE pState, const char * szName )
{
   /* a header's own question: the -D flags, std.ch and the rest the
      compile's preprocessor knows */
   if( pState == s_pCondHeader )
      return s_pCondMain && hb_pp_isDefinedName( s_pCondMain, szName );
   if( pState != s_pCondMain )
      return HB_FALSE;

   if( ! s_szCondUnread[ 0 ] && hb_compCondSurelyUndefined( szName ) )
      return HB_FALSE;

   if( s_nCondReplayed < s_nCondLog )
   {
      if( ! s_pCondHeader )
      {
         s_pCondHeader = hb_pp_new();
         hb_pp_init( s_pCondHeader, HB_TRUE, HB_FALSE, 0, NULL, NULL, NULL,
                     hb_compCondError, hb_compCondDisp, NULL, NULL, NULL );
         hb_pp_copySearchPath( s_pCondHeader, s_pCondMain );
      }
      hb_pp_readRulesBuffer( s_pCondHeader,
                             s_pCondMain->pFile ? s_pCondMain->pFile->szFileName : NULL,
                             s_pCondLog + s_nCondReplayed, s_nCondLog - s_nCondReplayed );
      s_nCondReplayed = s_nCondLog;
   }
   if( s_pCondHeader && hb_pp_isDefinedName( s_pCondHeader, szName ) )
      return HB_TRUE;

   if( s_szCondUnread[ 0 ] )
   {
      const char * szFile = s_pCondMain->pFile && s_pCondMain->pFile->szFileName ?
                            s_pCondMain->pFile->szFileName : "?";
      int iLine = s_pCondMain->pFile ? s_pCondMain->pFile->iCurrentLine : 0;
      fprintf( stderr, "hbtranspiler: %s(%d): warning W0042  a conditional asks "
               "whether '%s' is defined, and the transpiler could not read "
               "'%s' to know; it takes it as undefined\n",
               hb_strCollapsePath( szFile ), iLine, szName, s_szCondUnread );
   }
   return HB_FALSE;
}

/* A new file: nothing of the last one is defined. */
void hb_compCondReset( void )
{
   if( s_pCondHeader )
   {
      hb_pp_free( s_pCondHeader );
      s_pCondHeader = NULL;
   }
   s_nCondLog = s_nCondReplayed = 0;
   if( s_pCondLog )
      s_pCondLog[ 0 ] = '\0';
   s_szCondUnread[ 0 ] = '\0';
   s_nCondReachLog = ( HB_SIZE ) -1;
}

/* Callback to capture #include/#define directives into AST */
static void hb_pp_directiveCapture( void * cargo, const char * szDirective,
                                    const char * szValue, int iLine )
{
   PHB_COMP pComp = ( PHB_COMP ) cargo;

   /* the log hb_compCondDefined() replays */
   if( strcmp( szDirective, "INCLUDE" ) == 0 )
   {
      /* only `#include "x"` keeps its name to here (`<x>` arrives as "<") */
      if( strcmp( szValue, "<" ) == 0 )
      {
         if( ! s_szCondUnread[ 0 ] )
            hb_strncpy( s_szCondUnread, "#include <...>", sizeof( s_szCondUnread ) - 1 );
      }
      else
         hb_compCondLog( "#include \"", szValue, "\"" );
   }
   else if( strcmp( szDirective, "DEFINE" ) == 0 )
      hb_compCondLog( "#define ", szValue, "" );
   else if( strcmp( szDirective, "UNDEF" ) == 0 )
   {
      hb_compCondLog( "#undef ", szValue, "" );
      return;
   }

   /* #include and #define are file-scope in Harbour regardless of where
      they textually appear. Use hb_astAppendToStartup so they land in the
      synthetic startup function's body (file scope), not in whichever user
      function happens to be mid-parse when the preprocessor fires. Without
      this, a #define between two functions gets attached to the first
      function's body and both the -GT and -GS emitters drop it in the
      wrong place (e.g. a `const` decl after `return`, invisible to sibling
      functions). Mirrors how CLASS nodes are handled. */
   if( strcmp( szDirective, "INCLUDE" ) == 0 )
   {
      PHB_AST_NODE pNode = hb_astNew( HB_AST_INCLUDE, iLine );
      pNode->value.asInclude.szFile = hb_compIdentifierNew( pComp, szValue, HB_IDENT_COPY );
      hb_astAppendToStartup( pComp, pNode );
   }
   else if( strcmp( szDirective, "DEFINE" ) == 0 )
   {
      PHB_AST_NODE pNode = hb_astNew( HB_AST_PPDEFINE, iLine );
      pNode->value.asDefine.szDefine = hb_compIdentifierNew( pComp, szValue, HB_IDENT_COPY );
      hb_astAppendToStartup( pComp, pNode );
   }
}
#endif

void hb_compInitPP( HB_COMP_DECL, PHB_PP_OPEN_FUNC pOpenFunc )
{
   HB_TRACE( HB_TR_DEBUG, ( "hb_compInitPP()" ) );

   if( HB_COMP_PARAM->pLex->pPP )
   {
      hb_pp_init( HB_COMP_PARAM->pLex->pPP,
                  HB_COMP_PARAM->fQuiet, HB_COMP_PARAM->fGauge,
                  HB_COMP_PARAM->iMaxTransCycles,
                  HB_COMP_PARAM, pOpenFunc, NULL,
                  hb_pp_ErrorGen, hb_pp_Disp, hb_pp_PragmaDump,
                  HB_COMP_ISSUPPORTED( HB_COMPFLAG_HB_INLINE ) ?
                  hb_pp_hb_inLine : NULL, hb_pp_CompilerSwitch );
      hb_pp_setDumpCsFunc( hb_pp_PragmaCSharp );

#ifdef HB_TRANSPILER
      /* Transpiler mode: source #include directives are captured as
         AST nodes (for round-tripping in .hb output) but the included
         files themselves are NOT expanded into the source — we don't
         want their content flowing into the parser.

         To still see the standard syntax sugar (DEFAULT, IIF, etc.)
         we preload std.ch and common.ch via hb_pp_readRules. That
         call consumes a file in "rules-only" mode: any #xcommand /
         #define / #xtranslate directives are registered globally,
         but every other token is discarded.

         IMPORTANT: we deliberately do NOT load hbclass.ch. The
         transpiler has its own dedicated CLASS / METHOD / DATA
         parser (hbclsparse.c) which expects the canonical syntax.
         Loading hbclass.ch's rules would expand class syntax into
         its underlying runtime-call machinery, and the dedicated
         class parser would never see the original form. */
      hb_pp_setNoInclude( HB_COMP_PARAM->pLex->pPP, HB_TRUE );
      hb_pp_setComments( HB_COMP_PARAM->pLex->pPP, HB_TRUE );
      hb_pp_readRules( HB_COMP_PARAM->pLex->pPP, "std.ch" );
      hb_pp_readRules( HB_COMP_PARAM->pLex->pPP, "common.ch" );

      /* Expose a transpiler-mode marker to source .prg files so they
         can `#ifndef __HB_TRANSPILER__ ... #endif` around constructs
         the transpiler can't parse (typical offender: Clipper/GUI DSL
         like `INIT WINDOW ... CONTEXT MENU ...`). Unset under normal
         hbmk2 builds, so hand-wrapped blocks still compile there. */
      hb_pp_addDefine( HB_COMP_PARAM->pLex->pPP, "__HB_TRANSPILER__", NULL );

      /* Add the built-in dynamic defines and any -D / -undef flags
         BEFORE running the preload filter, so preload headers'
         `#ifdef ECR` / `#ifndef XYZ` see the command-line -D state.
         These calls normally live just before setStdBase, but the
         preload loop needs their effect; moving them up costs
         nothing — the preload and setStdBase both sit in the same
         window between rule registration and the first source. */
      hb_pp_initDynDefines( HB_COMP_PARAM->pLex->pPP, ! HB_COMP_PARAM->fNoArchDefs );
      hb_compChkSetDefines( HB_COMP_PARAM );

      /* Project-supplied preload list (soft extension of the baked-in
         std.ch + common.ch set). Each line names another .ch whose
         `#define` lines should register globally — `#xcommand`,
         `#xtranslate`, and friends are deliberately ignored. The
         list is meant for header *constants* that the transpiler
         would otherwise miss (setNoInclude stops header text flowing
         into the parser); letting arbitrary rewrite rules in would
         reintroduce the surprise-expansion problem hbclass.ch was
         excluded to avoid.
         Soft: a missing file or a bad define on a line warns (W0019)
         but doesn't fail the transpile. */
      {
         const char * szPath = hb_compPreloadListGetPath();
         if( szPath && *szPath )
         {
            FILE * fp = hb_fopen( szPath, "r" );
            if( fp )
            {
               char line[ 512 ];
               s_fPreloadSoftErrors = HB_TRUE;
               while( fgets( line, sizeof( line ), fp ) )
               {
                  char * p = line;
                  char * q;
                  char szResolved[ HB_PATH_MAX ];
                  while( *p == ' ' || *p == '\t' )
                     p++;
                  if( *p == '\0' || *p == '#' ||
                      *p == '\n' || *p == '\r' )
                     continue;
                  for( q = p; *q; q++ )
                  {
                     if( *q == '\n' || *q == '\r' || *q == '#' )
                     {
                        *q = '\0';
                        break;
                     }
                  }
                  q = p + strlen( p );
                  while( q > p && ( q[ -1 ] == ' ' || q[ -1 ] == '\t' ) )
                     *--q = '\0';
                  if( *p == '\0' )
                     continue;
                  if( ! hb_compPreloadResolve( HB_COMP_PARAM->pLex->pPP,
                                               p, szResolved,
                                               sizeof( szResolved ) ) )
                  {
                     fprintf( stderr,
                              "hbtranspiler: warning W0019  "
                              "Preload header '%s' not found in include path\n",
                              p );
                     continue;
                  }
                  /* Filter the header to a scratch file containing only
                     define + flow-control directives, then hand that
                     to hb_pp_readRules. Skipping the direct
                     hb_pp_addDefine path avoids a tokenizer-state bug
                     where injecting a define bare (outside a file
                     context) left later call sites confused. The
                     scratch file lives in the system temp dir and is
                     unlinked immediately after consumption. */
                  {
                     char szTmp[ HB_PATH_MAX ];
                     static unsigned s_iPreloadSeq = 0;
                     hb_snprintf( szTmp, sizeof( szTmp ),
                                  "%s%chbpreload_%u_%u.ch",
                                  hb_compPreloadTmpDir(),
                                  HB_OS_PATH_DELIM_CHR,
                                  ( unsigned ) HB_PRELOAD_GETPID(),
                                  ++s_iPreloadSeq );
                     if( hb_compPreloadFilter( szResolved, szTmp ) )
                     {
                        hb_pp_readRules( HB_COMP_PARAM->pLex->pPP, szTmp );
                        remove( szTmp );
                     }
                     else
                     {
                        fprintf( stderr,
                                 "hbtranspiler: warning W0019  "
                                 "Preload header '%s' could not be filtered\n",
                                 p );
                     }
                  }
               }
               s_fPreloadSoftErrors = HB_FALSE;
               fclose( fp );
            }
            else
            {
               fprintf( stderr,
                        "hbtranspiler: warning W0019  "
                        "Preload list '%s' could not be opened\n", szPath );
            }
         }
      }

      /* setStdBase marks the current rule list as the "standard"
         (persistent) set; per-file hb_pp_reset() then strips
         everything NOT in that set. So the dynamic defines, -D
         flags, and preload-list defines all need to be registered
         above this line — otherwise they get wiped on the first
         source reset and `#ifdef ECR` / `#ifdef __HARBOUR__` comes
         back false. */
      hb_pp_setStdBase( HB_COMP_PARAM->pLex->pPP );

      /* Set callback to capture #include/#define directives into AST */
      {
         HB_PP_STATE * pPP = ( HB_PP_STATE * ) HB_COMP_PARAM->pLex->pPP;
         pPP->pDirectiveCargo = ( void * ) HB_COMP_PARAM;
         pPP->pDirectiveFunc = hb_pp_directiveCapture;
         /* and `#ifdef` on what that keeps out of the preprocessor */
         s_pCondMain = pPP;
         hb_pp_setCondDefFunc( hb_compCondDefined );
      }
#else
      if( HB_COMP_PARAM->iTraceInclude )
         hb_pp_setIncFunc( HB_COMP_PARAM->pLex->pPP, hb_pp_fileIncluded );

      if( ! HB_COMP_PARAM->szStdCh )
         hb_pp_setStdRules( HB_COMP_PARAM->pLex->pPP );
      else if( HB_COMP_PARAM->szStdCh[ 0 ] > ' ' )
         hb_pp_readRules( HB_COMP_PARAM->pLex->pPP, HB_COMP_PARAM->szStdCh );
      else if( ! HB_COMP_PARAM->fQuiet )
         hb_compOutStd( HB_COMP_PARAM, "Standard command definitions excluded.\n" );

      hb_pp_initDynDefines( HB_COMP_PARAM->pLex->pPP, ! HB_COMP_PARAM->fNoArchDefs );

      /* Add /D and /undef: command-line or envvar defines */
      hb_compChkSetDefines( HB_COMP_PARAM );

      /* add extended definitions files (-u+<file>) */
      if( HB_COMP_PARAM->iStdChExt > 0 )
      {
         int i = 0;

         while( i < HB_COMP_PARAM->iStdChExt )
            hb_pp_readRules( HB_COMP_PARAM->pLex->pPP,
                             HB_COMP_PARAM->szStdChExt[ i++ ] );
      }

      /* mark current rules as standard ones */
      hb_pp_setStdBase( HB_COMP_PARAM->pLex->pPP );
#endif
   }
}
