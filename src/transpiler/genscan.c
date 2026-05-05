/*
 * Harbour Transpiler - Function-table scan mode (-GF)
 *
 * This is the entry point invoked from hb_compGenOutput when the user
 * passes -GF on the command line. Instead of generating code, it
 * walks the parsed AST and accumulates information about every
 * function/procedure defined in this source file plus every by-ref
 * call site, into HB_REFTAB_PATH.
 *
 * The table is loaded before scanning and saved afterwards so multiple
 * invocations of -GF over different files accumulate into one table.
 * This is the whole-codebase pre-scan step that the C# emitter relies
 * on for correct cross-file by-ref handling and named-argument call
 * rewriting.
 *
 * Copyright 2026 harbour.github.io
 */

#include "hbcomp.h"
#include "hbast.h"
#include "hbreftab.h"
#include "hbvartypes.h"

/* Hungarian-prefix gate. Names are acceptable when:
     - first character is a recognised type prefix (n/c/l/a/o/d/h/b/t/p)
       followed by an uppercase letter — standard Hungarian.
     - first character is `x` / `X` — explicit "USUAL" / variant.
   Anything else is flagged with W0021 so the source can be cleaned up.
   Per Alex 2026-05-05: "anything that doesn't have an explicit Hungarian
   notation should be treated as an error unless it actually starts
   with an X." */
static HB_BOOL hb_csCheckIsHungarianChar( char c )
{
   switch( c )
   {
      case 'n': case 'c': case 'l': case 'a': case 'o':
      case 'd': case 'h': case 'b': case 't': case 'p':
         return HB_TRUE;
   }
   return HB_FALSE;
}

static HB_BOOL hb_csCheckIsHungarian( const char * szName )
{
   if( ! szName || ! *szName )
      return HB_TRUE;
   if( szName[ 0 ] == 'x' || szName[ 0 ] == 'X' )
      return HB_TRUE;
   /* Hungarian: name starts with a recognised type-prefix letter
      and is at least two characters long. Tail can be anything —
      `lx`, `la`, `nCount`, `c3rdParty`, `oFoo` all qualify. */
   if( hb_csCheckIsHungarianChar( szName[ 0 ] ) && szName[ 1 ] != '\0' )
      return HB_TRUE;
   /* easipos two-letter STATIC convention: s<H>... — first `s` flags
      file-scope STATIC, second lowercase letter is the real Hungarian
      prefix (snDrawer = STATIC NUMERIC, saFixed = STATIC ARRAY,
      soWindow = STATIC OBJECT). */
   if( szName[ 0 ] == 's' &&
       hb_csCheckIsHungarianChar( szName[ 1 ] ) &&
       szName[ 2 ] != '\0' )
      return HB_TRUE;
   /* Project-supplied type hints (--var-types). Whitelists short
      conventional names like `i` / `j` / `k` (decimal loop counters)
      that we don't want to rename to Hungarian. */
   if( hb_varTabType( szName ) )
      return HB_TRUE;
   return HB_FALSE;
}

/* True if pInit gives an unambiguous type for the declaration —
   numeric / string / logical / date / array / hash / codeblock
   literal. NIL doesn't count (no type info). Calls hb_astInferType
   with NULL name so the prefix-fallback is skipped; only the
   expression-side inference is consulted. */
static HB_BOOL hb_csCheckInitGivesType( PHB_EXPR pInit )
{
   const char * szType;
   if( ! pInit )
      return HB_FALSE;
   szType = hb_astInferType( NULL, pInit );
   return szType && hb_stricmp( szType, "USUAL" ) != 0;
}

static void hb_csCheckVar( HB_COMP_DECL, const char * szName,
                           PHB_EXPR pInit, int iLine, const char * szKind )
{
   char buffer[ 256 ];
   if( hb_csCheckIsHungarian( szName ) )
      return;
   if( hb_csCheckInitGivesType( pInit ) )
      return;
   hb_snprintf( buffer, sizeof( buffer ),
      "hbtranspiler: %s(%d): warning W0021  %s '%s' lacks Hungarian prefix\n",
      HB_COMP_PARAM->szFile, iLine, szKind, szName ? szName : "" );
   hb_compOutErr( HB_COMP_PARAM, buffer );
}

static void hb_csCheckBlock( HB_COMP_DECL, PHB_AST_NODE pBlock )
{
   PHB_AST_NODE pStmt;
   if( ! pBlock || pBlock->type != HB_AST_BLOCK )
      return;
   for( pStmt = pBlock->value.asBlock.pFirst; pStmt; pStmt = pStmt->pNext )
   {
      const char * szKind = NULL;
      switch( pStmt->type )
      {
         case HB_AST_LOCAL:   szKind = "Local";   break;
         case HB_AST_STATIC:  szKind = "Static";  break;
         case HB_AST_MEMVAR:  szKind = "Memvar";  break;
         case HB_AST_PUBLIC:  szKind = "Public";  break;
         case HB_AST_PRIVATE: szKind = "Private"; break;
         default: break;
      }
      if( szKind )
         hb_csCheckVar( HB_COMP_PARAM,
                        pStmt->value.asVar.szName,
                        pStmt->value.asVar.pInit,
                        pStmt->iLine, szKind );
   }
}

static void hb_csCheckHungarian( HB_COMP_DECL )
{
   PHB_AST_NODE pFunc;
   PHB_AST_NODE pFirst;

   /* Function-level: parameters + LOCAL/STATIC/MEMVAR/PUBLIC/PRIVATE
      declarations in the body. Harbour requires these at the top of
      the function so the body's first-level statement walk catches
      them all without recursing into nested control flow. */
   for( pFunc = HB_COMP_PARAM->ast.pFuncList; pFunc; pFunc = pFunc->pNext )
   {
      PHB_HVAR pVar;
      if( pFunc->type != HB_AST_FUNCTION )
         continue;
      for( pVar = pFunc->value.asFunc.pParams; pVar; pVar = pVar->pNext )
         hb_csCheckVar( HB_COMP_PARAM, pVar->szName, NULL,
                        pFunc->iLine, "Parameter" );
      hb_csCheckBlock( HB_COMP_PARAM, pFunc->value.asFunc.pBody );
   }

   /* Class-level: DATA / VAR members. CLASS nodes live at the top of
      the startup (first) function's body block. */
   pFirst = HB_COMP_PARAM->ast.pFuncList;
   if( pFirst && pFirst->type == HB_AST_FUNCTION &&
       pFirst->value.asFunc.pBody &&
       pFirst->value.asFunc.pBody->type == HB_AST_BLOCK )
   {
      PHB_AST_NODE pStmt = pFirst->value.asFunc.pBody->value.asBlock.pFirst;
      while( pStmt )
      {
         if( pStmt->type == HB_AST_CLASS )
         {
            PHB_AST_NODE pMember = pStmt->value.asClass.pMembers;
            while( pMember )
            {
               if( pMember->type == HB_AST_CLASSDATA )
               {
                  /* Class DATA stores INIT as a raw string; route it
                     through hb_astInferTypeFromInit which understands
                     .T./.F. / "..." / {} / numeric / NIL — the same
                     literal vocabulary the expression-side uses. The
                     check below skips the warning when the init
                     yields a known type. */
                  const char * szName =
                     pMember->value.asClassData.szName;
                  const char * szInit =
                     pMember->value.asClassData.szInit;
                  HB_BOOL fInitTyped = HB_FALSE;
                  if( szInit && *szInit )
                  {
                     const char * szT =
                        hb_astInferTypeFromInit( NULL, szInit );
                     if( szT && hb_stricmp( szT, "USUAL" ) != 0 )
                        fInitTyped = HB_TRUE;
                  }
                  if( ! hb_csCheckIsHungarian( szName ) && ! fInitTyped )
                  {
                     char buffer[ 256 ];
                     hb_snprintf( buffer, sizeof( buffer ),
                        "hbtranspiler: %s(%d): warning W0021  "
                        "Class member '%s' lacks Hungarian prefix\n",
                        HB_COMP_PARAM->szFile, pMember->iLine,
                        szName ? szName : "" );
                     hb_compOutErr( HB_COMP_PARAM, buffer );
                  }
               }
               pMember = pMember->pNext;
            }
         }
         pStmt = pStmt->pNext;
      }
   }
}

void hb_compGenScan( HB_COMP_DECL, PHB_FNAME pFileName )
{
   PHB_REFTAB   pTab;
   const char * szPath = hb_refTabGetPath();

   HB_SYMBOL_UNUSED( pFileName );

   pTab = hb_refTabNew();

   /* Merge with whatever's already on disk so multiple -GF invocations
      accumulate. Failure to load is fine — first run starts empty. */
   hb_refTabLoad( pTab, szPath );

   /* Scan this file's AST and add its functions / by-ref usage. */
   hb_refTabCollect( pTab, HB_COMP_PARAM );

   /* Hungarian-prefix audit. Emits W0021 to stderr for every variable
      declaration whose name doesn't follow Hungarian. scan.sh routes
      stderr through warnings.txt; gen-cs.sh refuses to run while that
      file is non-empty. */
   hb_csCheckHungarian( HB_COMP_PARAM );

   /* Persist. */
   if( ! hb_refTabSave( pTab, szPath ) )
   {
      char buffer[ 256 ];
      hb_snprintf( buffer, sizeof( buffer ),
                   "hbtranspiler: warning: cannot write %s\n", szPath );
      hb_compOutStd( HB_COMP_PARAM, buffer );
   }
   else if( ! HB_COMP_PARAM->fQuiet )
   {
      char buffer[ 256 ];
      hb_snprintf( buffer, sizeof( buffer ),
                   "Scanned '%s' into %s\n",
                   HB_COMP_PARAM->szFile, szPath );
      hb_compOutStd( HB_COMP_PARAM, buffer );
   }

   hb_refTabFree( pTab );
}
