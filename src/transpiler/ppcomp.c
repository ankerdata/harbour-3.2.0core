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
#include <string.h>
#include <unistd.h>        /* getpid, unlink for preload scratch files */
#include "hbast.h"

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

     pass through:  #define NAME VALUE   (non-macro)
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
static HB_BOOL hb_compPreloadFilter( const char * szPath,
                                     const char * szFilteredPath )
{
   FILE * fpIn;
   FILE * fpOut;
   char   line[ 4096 ];

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
         fputs( line, fpOut );
         continue;
      }
      if( strncmp( p, "define", 6 ) == 0 &&
          ( p[ 6 ] == ' ' || p[ 6 ] == '\t' ) )
      {
         char * q = p + 6;
         while( *q == ' ' || *q == '\t' )
            q++;
         /* Skip the name to detect macro-shape `NAME(...)`. */
         while( ( *q >= 'A' && *q <= 'Z' ) ||
                ( *q >= 'a' && *q <= 'z' ) ||
                ( *q >= '0' && *q <= '9' ) ||
                *q == '_' )
            q++;
         if( *q == '(' )
            continue;   /* macro define — the preload list forbids these */
         fputs( line, fpOut );
         continue;
      }
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
/* Callback to capture #include/#define directives into AST */
static void hb_pp_directiveCapture( void * cargo, const char * szDirective,
                                    const char * szValue, int iLine )
{
   PHB_COMP pComp = ( PHB_COMP ) cargo;

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
                                  "%s/hbpreload_%u_%u.ch",
                                  P_tmpdir ? P_tmpdir : "/tmp",
                                  ( unsigned ) getpid(),
                                  ++s_iPreloadSeq );
                     if( hb_compPreloadFilter( szResolved, szTmp ) )
                     {
                        hb_pp_readRules( HB_COMP_PARAM->pLex->pPP, szTmp );
                        unlink( szTmp );
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
