/*
 * Harbour Transpiler - C# code emitter
 *
 * Copyright 2026 harbour.github.io
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation; either version 2, or (at your option)
 * any later version.
 */

#include "hbcomp.h"
#include "hbast.h"
#include "hbdate.h"
#include "hbreftab.h"
#include "hbfunctab.h"

/* Forward declarations */
static void hb_csEmitExpr( PHB_EXPR pExpr, FILE * yyc, HB_BOOL fParen );
static void hb_csEmitNode( PHB_AST_NODE pNode, FILE * yyc, int iIndent );
static void hb_csEmitBlock( PHB_AST_NODE pBlock, FILE * yyc, int iIndent );
static void hb_csEmitCallArgs( const char * szFunc, PHB_EXPR pParms, FILE * yyc );

/* Track last source line for blank line preservation */
static int s_iLastLine = 0;
static PHB_AST_NODE s_pClassList = NULL;
static HB_BOOL s_fVoidFunc = HB_FALSE;  /* suppress return expr in void functions */
static PHB_EXPR s_pWithObject = NULL;   /* current WITH OBJECT expression */
static PHB_REFTAB s_pRefTab = NULL;     /* by-ref parameter table for current run */
static char s_szCurrentFunc[ 256 ] = "";  /* keyed name of the function currently
                                             being emitted (e.g. "calculator::adjust"
                                             or "fred"). Used to look up nilable
                                             parameters when wrapping IF/IIF
                                             conditions. Empty when no function. */
static PHB_AST_NODE s_pCurrentFuncNode = NULL;  /* AST node of function currently
                                                   being emitted. Used for
                                                   case-insensitive local lookups
                                                   when resolving identifier
                                                   references the parser wrapped
                                                   as implicit memvar aliases. */

/* File-scope STATIC var registry. Harbour STATIC vars are private to
   their declaring .prg file, but every generated .cs file merges into
   one `public static partial class Program`, so two files each
   declaring `STATIC aHex := {...}` produce a C# member collision.
   We collect the STATIC names once per emit and rewrite both the
   declaration and every reference to `<FileBase>_<VarName>`, which
   is file-unique by construction. Locals with the same name still
   win (handled in hb_csEmitExpr for HB_ET_VARIABLE). */
#define HB_CS_MAX_FILE_STATICS 256
static const char * s_pFileStatics[ HB_CS_MAX_FILE_STATICS ];
static int s_iFileStaticCount = 0;
static char s_szFileBase[ 64 ] = "";

static HB_BOOL hb_csIsFileStatic( const char * szName )
{
   int i;
   if( ! szName || s_iFileStaticCount == 0 )
      return HB_FALSE;
   for( i = 0; i < s_iFileStaticCount; i++ )
      if( hb_stricmp( s_pFileStatics[ i ], szName ) == 0 )
         return HB_TRUE;
   return HB_FALSE;
}

static void hb_csAddFileStatic( const char * szName )
{
   if( ! szName || s_iFileStaticCount >= HB_CS_MAX_FILE_STATICS )
      return;
   /* Dedup: the collection pass walks every function's body block
      for HB_AST_STATIC nodes, and the same name can legitimately
      appear in more than one collection pass during a single file
      emission. Silently drop duplicates rather than growing the
      registry unbounded. */
   if( hb_csIsFileStatic( szName ) )
      return;
   s_pFileStatics[ s_iFileStaticCount++ ] = szName;
}

static void hb_csResetFileStatics( void )
{
   s_iFileStaticCount = 0;
   s_szFileBase[ 0 ] = '\0';
}

/* Walk the current function's parameter+local list for a case-insensitive
   name match. Returns the canonical (declared) name if found, or NULL.
   Harbour's local lookup is case-insensitive at the language level but the
   parser stores only the first-seen casing, so typo'd references like
   `aRetval` against a local declared `aRetVal` get auto-wrapped as implicit
   memvars. This helper lets the C# emitter unwrap that case at emit time. */
static const char * hb_csResolveLocal( const char * szName )
{
   PHB_HVAR pVar;
   if( ! szName || ! s_pCurrentFuncNode )
      return NULL;
   pVar = s_pCurrentFuncNode->value.asFunc.pParams;
   while( pVar )
   {
      if( pVar->szName && hb_stricmp( pVar->szName, szName ) == 0 )
         return pVar->szName;
      pVar = pVar->pNext;
   }
   return NULL;
}

/* Returns HB_TRUE if pExpr is a "naked" boolean condition that needs
   wrapping with `== true` for C# to accept it. The case we care about
   is a single nilable parameter (from the active function) used as a
   condition: in Harbour `IF lFlag` is fine even when lFlag is NIL,
   but in C# `if (lFlag)` doesn't compile if lFlag is `bool?`.

   We unwrap a one-element HB_ET_LIST first because the parser wraps
   conditions in lists. The inner expression must be HB_ET_VARIABLE
   referring to a parameter that hbreftab has marked nilable. */
static HB_BOOL hb_csConditionNeedsBoolUnwrap( PHB_EXPR pExpr )
{
   const char * szName;
   const HB_REFPARAM * pP;
   int i;
   int nParams;

   if( ! pExpr || ! s_szCurrentFunc[ 0 ] || ! s_pRefTab )
      return HB_FALSE;

   /* Peel single-element list wrappers (defensive — the strip pass
      should have left these alone already, but be safe). */
   while( ( pExpr->ExprType == HB_ET_LIST ||
            pExpr->ExprType == HB_ET_ARGLIST ) &&
          pExpr->value.asList.pExprList &&
          ! pExpr->value.asList.pExprList->pNext )
      pExpr = pExpr->value.asList.pExprList;

   if( pExpr->ExprType != HB_ET_VARIABLE )
      return HB_FALSE;

   szName = pExpr->value.asSymbol.name;
   if( ! szName )
      return HB_FALSE;

   /* Look up in the active function's parameter list for a nilable
      slot whose name matches. */
   nParams = hb_refTabParamCount( s_pRefTab, s_szCurrentFunc );
   for( i = 0; i < nParams; i++ )
   {
      pP = hb_refTabParam( s_pRefTab, s_szCurrentFunc, i );
      if( pP && pP->szName && hb_stricmp( pP->szName, szName ) == 0 )
         return pP->fNilable;
   }
   return HB_FALSE;
}

/* Emit the argument list of a function or method call.

   pParms is typically an HB_ET_LIST/HB_ET_ARGLIST whose pExprList holds
   the args. Three jobs:

   1. HB_ET_VARREF items get prefixed with `ref ` to match the
      ref-marked parameter declaration.

   2. Trailing HB_ET_NONE slots (Harbour's empty-arg sentinel) are
      dropped entirely — the C# default values on the target function
      fill them in.

   3. Middle HB_ET_NONE slots — the Fred(x, , z) case — force a
      switch to C# named-argument syntax from that point onwards, so
      we emit Fred(x, c: z). We look up parameter names from the
      signature table via szFunc. If szFunc is NULL or the function
      isn't in the table, we fall back to emitting `default` for the
      missing slot (safe but less pretty).
*/
static void hb_csEmitCallArgs( const char * szFunc, PHB_EXPR pParms, FILE * yyc )
{
   PHB_EXPR pHead;
   PHB_EXPR pItem;
   int      nLastReal = -1;
   int      iPos;
   HB_BOOL  fFirst = HB_TRUE;
   HB_BOOL  fNamed = HB_FALSE;

   if( ! pParms )
      return;

   if( pParms->ExprType == HB_ET_LIST ||
       pParms->ExprType == HB_ET_ARGLIST ||
       pParms->ExprType == HB_ET_MACROARGLIST )
      pHead = pParms->value.asList.pExprList;
   else
      pHead = pParms;

   /* Pass 1: find the last index that holds a real (non-HB_ET_NONE)
      argument. Everything past that is a trailing default and gets
      dropped. */
   for( pItem = pHead, iPos = 0; pItem; pItem = pItem->pNext, iPos++ )
   {
      if( pItem->ExprType != HB_ET_NONE )
         nLastReal = iPos;
   }

   if( nLastReal < 0 )
      return;   /* empty arg list or all HB_ET_NONE */

   /* Pass 2: walk [0 .. nLastReal] and emit each slot. On the first
      HB_ET_NONE we hit before nLastReal, flip into named-arg mode for
      every subsequent non-NONE slot. */
   for( pItem = pHead, iPos = 0; pItem && iPos <= nLastReal;
        pItem = pItem->pNext, iPos++ )
   {
      if( pItem->ExprType == HB_ET_NONE )
      {
         /* A gap. The next real slot will emit in named form. */
         fNamed = HB_TRUE;
         continue;
      }

      if( ! fFirst )
         fprintf( yyc, ", " );
      fFirst = HB_FALSE;

      if( fNamed )
      {
         const HB_REFPARAM * pP = szFunc
            ? hb_refTabParam( s_pRefTab, szFunc, iPos )
            : NULL;
         if( pP && pP->szName && pP->szName[ 0 ] )
            fprintf( yyc, "%s: ", pP->szName );
         /* If we can't find the name, the emission falls back to
            positional — which is only correct if there are no more
            gaps after this one. We accept the risk: unknown functions
            aren't in the table. */
      }

      if( pItem->ExprType == HB_ET_VARREF )
         fprintf( yyc, "ref " );
      hb_csEmitExpr( pItem, yyc, HB_FALSE );
   }
}

/* ---- Type mapping ---- */

static const char * hb_csTypeMap( const char * szHbType )
{
   if( ! szHbType || hb_stricmp( szHbType, "USUAL" ) == 0 )
      return "dynamic";
   if( hb_stricmp( szHbType, "NUMERIC" ) == 0 )
      return "decimal";
   if( hb_stricmp( szHbType, "STRING" ) == 0 )
      return "string";
   if( hb_stricmp( szHbType, "LOGICAL" ) == 0 )
      return "bool";
   if( hb_stricmp( szHbType, "DATE" ) == 0 )
      return "DateOnly";
   if( hb_stricmp( szHbType, "TIMESTAMP" ) == 0 )
      return "DateTime";
   if( hb_stricmp( szHbType, "OBJECT" ) == 0 )
      return "object";
   if( hb_stricmp( szHbType, "ARRAY" ) == 0 )
      return "dynamic[]";
   if( hb_stricmp( szHbType, "HASH" ) == 0 )
      return "Dictionary<dynamic, dynamic>";
   if( hb_stricmp( szHbType, "BLOCK" ) == 0 )
      return "dynamic";
   /* Class name or unknown — pass through as-is */
   return szHbType;
}

static const char * hb_csScopeStr( int iScope )
{
   switch( iScope )
   {
      case HB_AST_SCOPE_PROTECTED: return "protected";
      case HB_AST_SCOPE_HIDDEN:    return "private";
      default:                      return "public";
   }
}

/* Translate a Harbour INIT value string to C# syntax.
   Returns a static buffer — use immediately or copy. */
static const char * hb_csTranslateInit( const char * szVal )
{
   static char s_szBuf[ 512 ];
   HB_SIZE nLen;

   if( ! szVal )
      return szVal;

   /* Strip trailing READONLY */
   nLen = strlen( szVal );
   if( nLen > 9 && hb_stricmp( szVal + nLen - 8, "READONLY" ) == 0 )
   {
      nLen -= 8;
      while( nLen > 0 && szVal[ nLen - 1 ] == ' ' )
         nLen--;
      if( nLen < sizeof( s_szBuf ) )
      {
         memcpy( s_szBuf, szVal, nLen );
         s_szBuf[ nLen ] = '\0';
         szVal = s_szBuf;
      }
   }

   /* .T. / .F. → true / false */
   if( hb_stricmp( szVal, ".T." ) == 0 )
      return "true";
   if( hb_stricmp( szVal, ".F." ) == 0 )
      return "false";

   /* NIL → null */
   if( hb_stricmp( szVal, "NIL" ) == 0 )
      return "null";

   /* {} → empty array */
   if( strcmp( szVal, "{}" ) == 0 )
      return "Array.Empty<dynamic>()";

   /* Numeric literal with decimal point → append m suffix */
   if( strchr( szVal, '.' ) && szVal[ 0 ] != '.' &&
       ( ( szVal[ 0 ] >= '0' && szVal[ 0 ] <= '9' ) || szVal[ 0 ] == '-' || szVal[ 0 ] == '+' ) )
   {
      nLen = strlen( szVal );
      if( nLen < sizeof( s_szBuf ) - 1 )
      {
         memcpy( s_szBuf, szVal, nLen );
         s_szBuf[ nLen ] = 'm';
         s_szBuf[ nLen + 1 ] = '\0';
         return s_szBuf;
      }
   }

   return szVal;
}

/* ---- Helpers ---- */

static void hb_csEmitBlankLines( FILE * yyc, int iLine )
{
   if( iLine > 0 && s_iLastLine > 0 && iLine > s_iLastLine + 1 )
      fprintf( yyc, "\n" );
   if( iLine > 0 )
      s_iLastLine = iLine;
}

static void hb_csEmitIndent( FILE * yyc, int iIndent )
{
   int i;
   for( i = 0; i < iIndent; i++ )
      fprintf( yyc, "    " );
}

/* C# operator string */
static const char * hb_csOperatorStr( HB_EXPRTYPE type )
{
   switch( type )
   {
      case HB_EO_PLUS:    return " + ";
      case HB_EO_MINUS:   return " - ";
      case HB_EO_MULT:    return " * ";
      case HB_EO_DIV:     return " / ";
      case HB_EO_MOD:     return " % ";
      case HB_EO_ASSIGN:  return " = ";
      case HB_EO_PLUSEQ:  return " += ";
      case HB_EO_MINUSEQ: return " -= ";
      case HB_EO_MULTEQ:  return " *= ";
      case HB_EO_DIVEQ:   return " /= ";
      case HB_EO_MODEQ:   return " %= ";
      case HB_EO_EXPEQ:   return " ^= ";  /* TODO: Math.Pow */
      case HB_EO_EQUAL:   return " == ";
      case HB_EO_EQ:      return " == ";
      case HB_EO_NE:      return " != ";
      case HB_EO_LT:      return " < ";
      case HB_EO_GT:      return " > ";
      case HB_EO_LE:      return " <= ";
      case HB_EO_GE:      return " >= ";
      case HB_EO_AND:     return " && ";
      case HB_EO_OR:      return " || ";
      default:            return " ??? ";
   }
}

/* Map a function name to its remapped form, e.g. STR → HbRuntime.STR.
   The prefix is looked up in hbfuncs.tab (see include/hbfunctab.h).
   Functions with no prefix entry are returned unchanged.

   When a prefix is found, the function name is uppercased. Harbour is
   case-insensitive for identifiers, so real-world code uses every
   variation (`INT()`, `int()`, `Int()`). C# is case-sensitive, and
   HbRuntime.cs exposes its methods as UPPERCASE to match the Harbour
   convention. Without the uppercasing, `int()` would emit `HbRuntime.int`
   which is both syntactically broken (`int` is reserved) and semantically
   wrong (no such method). Non-remapped functions (user code, prefix="-")
   keep their source case so IDE navigation stays useful. */
static const char * hb_csFuncMap( const char * szName )
{
   static char s_szBuf[ 128 ];
   const char * szPrefix = hb_funcTabPrefix( szName );
   if( szPrefix )
   {
      char szUpper[ 128 ];
      HB_SIZE i;
      for( i = 0; szName[ i ] && i < sizeof( szUpper ) - 1; i++ )
         szUpper[ i ] = ( char ) HB_TOUPPER( ( HB_UCHAR ) szName[ i ] );
      szUpper[ i ] = '\0';
      hb_snprintf( s_szBuf, sizeof( s_szBuf ), "%s.%s", szPrefix, szUpper );
      return s_szBuf;
   }
   return szName;
}

/* Check if a name matches a known class. We check two sources:
     1. s_pClassList — classes defined in the file currently being
        emitted (the local AST class list)
     2. s_pRefTab    — classes recorded by the cross-file scan
                       (-GF), so ClassName():New() patterns work even
                       when ClassName is defined in another file */
static HB_BOOL hb_csIsClassName( const char * szName )
{
   PHB_AST_NODE pStmt = s_pClassList;
   while( pStmt )
   {
      if( pStmt->type == HB_AST_CLASS &&
          hb_stricmp( pStmt->value.asClass.szName, szName ) == 0 )
         return HB_TRUE;
      pStmt = pStmt->pNext;
   }
   if( hb_refTabIsClass( s_pRefTab, szName ) )
      return HB_TRUE;
   return HB_FALSE;
}

/* Check if expression is the ClassName():New() constructor pattern.
   Returns the class name if matched, NULL otherwise. */
/* Check if expression is a ClassName():Method() constructor pattern.
   Returns the class name if the object part is a FUNCALL to a known class, NULL otherwise. */
static const char * hb_csIsConstructor( PHB_EXPR pExpr )
{
   const char * szClassName;

   if( pExpr->ExprType != HB_ET_SEND )
      return NULL;

   /* Must have a message (method name) */
   if( ! pExpr->value.asMessage.szMessage )
      return NULL;

   /* Object must be a FUNCALL to a known class name */
   if( ! pExpr->value.asMessage.pObject ||
       pExpr->value.asMessage.pObject->ExprType != HB_ET_FUNCALL )
      return NULL;

   {
      PHB_EXPR pCall = pExpr->value.asMessage.pObject;
      if( ! pCall->value.asFunCall.pFunName ||
          pCall->value.asFunCall.pFunName->ExprType != HB_ET_FUNNAME )
         return NULL;

      szClassName = pCall->value.asFunCall.pFunName->value.asSymbol.name;
      if( hb_csIsClassName( szClassName ) )
         return szClassName;
   }

   return NULL;
}

/* ---- Expression emitter ---- */

static void hb_csEmitExpr( PHB_EXPR pExpr, FILE * yyc, HB_BOOL fParen )
{
   if( ! pExpr )
      return;

   switch( pExpr->ExprType )
   {
      case HB_ET_NONE:
         break;

      case HB_ET_NIL:
         fprintf( yyc, "null" );
         break;

      case HB_ET_NUMERIC:
         if( pExpr->value.asNum.NumType == HB_ET_LONG )
            fprintf( yyc, "%" HB_PFS "d", pExpr->value.asNum.val.l );
         else
            fprintf( yyc, "%.*fm", pExpr->value.asNum.bDec,
                     pExpr->value.asNum.val.d );
         break;

      case HB_ET_STRING:
         {
            const char * s = pExpr->value.asString.string;
            HB_SIZE nLen = pExpr->nLength;
            HB_SIZE n;

            fputc( '"', yyc );
            for( n = 0; n < nLen; n++ )
            {
               switch( s[ n ] )
               {
                  case '"':  fprintf( yyc, "\\\"" ); break;
                  case '\\': fprintf( yyc, "\\\\" ); break;
                  case '\n': fprintf( yyc, "\\n" ); break;
                  case '\r': fprintf( yyc, "\\r" ); break;
                  case '\t': fprintf( yyc, "\\t" ); break;
                  default:   fputc( s[ n ], yyc ); break;
               }
            }
            fputc( '"', yyc );
         }
         break;

      case HB_ET_LOGICAL:
         fprintf( yyc, "%s", pExpr->value.asLogical ? "true" : "false" );
         break;

      case HB_ET_SELF:
         fprintf( yyc, "this" );
         break;

      case HB_ET_VARIABLE:
      case HB_ET_VARREF:
         {
            const char * szVarName = pExpr->value.asSymbol.name;
            if( hb_stricmp( szVarName, "Self" ) == 0 )
               fprintf( yyc, "this" );
            else if( hb_csResolveLocal( szVarName ) == NULL &&
                     hb_csIsFileStatic( szVarName ) )
            {
               /* File-scope STATIC reference — rewrite to the mangled
                  class field name to dodge cross-file collisions.
                  A local with the same name in the current scope
                  shadows the static, matching Harbour's rule. */
               fprintf( yyc, "%s_%s", s_szFileBase, szVarName );
            }
            else
               fprintf( yyc, "%s", szVarName );
         }
         break;

      case HB_ET_FUNREF:
         fprintf( yyc, "%s", pExpr->value.asSymbol.name );
         break;

      case HB_ET_REFERENCE:
         fprintf( yyc, "ref " );
         hb_csEmitExpr( pExpr->value.asReference, yyc, HB_TRUE );
         break;

      case HB_ET_FUNCALL:
         {
            const char * szName = NULL;
            if( pExpr->value.asFunCall.pFunName &&
                pExpr->value.asFunCall.pFunName->ExprType == HB_ET_FUNNAME )
               szName = pExpr->value.asFunCall.pFunName->value.asSymbol.name;

            /* hb_AParams() inside a spread-receiving function body is
               literally the `hbva` array we already bound at the top
               of the C# function. Short-circuit to that instead of
               routing through HbRuntime, which has no such array to
               return. */
            if( szName && hb_stricmp( szName, "HB_APARAMS" ) == 0 )
            {
               fprintf( yyc, "hbva" );
               break;
            }

            if( szName )
               fprintf( yyc, "%s", hb_csFuncMap( szName ) );
            else
               hb_csEmitExpr( pExpr->value.asFunCall.pFunName, yyc, HB_FALSE );
            fprintf( yyc, "(" );
            hb_csEmitCallArgs( szName, pExpr->value.asFunCall.pParms, yyc );
            fprintf( yyc, ")" );
         }
         break;

      case HB_ET_FUNNAME:
         fprintf( yyc, "%s", pExpr->value.asSymbol.name );
         break;

      case HB_ET_SEND:
         {
            /* Check for ClassName():New() → new ClassName() */
            const char * szCtor = hb_csIsConstructor( pExpr );
            if( szCtor )
            {
               /* ClassName():New() → new ClassName()
                  ClassName():New(args) / ClassName():Init(args) → new ClassName().Method(args) */
               {
                  /* Check if the method call has actual arguments */
                  PHB_EXPR pArgs = pExpr->value.asMessage.pParms;
                  HB_BOOL fHasArgs = HB_FALSE;
                  if( pArgs )
                  {
                     /* An ARGLIST/LIST with a non-NONE first element has real args */
                     if( pArgs->ExprType == HB_ET_ARGLIST ||
                         pArgs->ExprType == HB_ET_LIST )
                     {
                        if( pArgs->value.asList.pExprList &&
                            pArgs->value.asList.pExprList->ExprType != HB_ET_NONE )
                           fHasArgs = HB_TRUE;
                     }
                     else if( pArgs->ExprType != HB_ET_NONE )
                        fHasArgs = HB_TRUE;
                  }

                  if( fHasArgs && pExpr->value.asMessage.szMessage )
                  {
                     /* Cast needed: New()/Init() returns object */
                     fprintf( yyc, "(%s)new %s().%s(", szCtor, szCtor,
                              pExpr->value.asMessage.szMessage );
                     hb_csEmitCallArgs( pExpr->value.asMessage.szMessage,
                                        pArgs, yyc );
                     fprintf( yyc, ")" );
                  }
                  else
                     fprintf( yyc, "new %s()", szCtor );
               }
               break;
            }
         }
         if( pExpr->value.asMessage.pObject )
         {
            /* Self:member → this.member */
            if( pExpr->value.asMessage.pObject->ExprType == HB_ET_VARIABLE &&
                hb_stricmp( pExpr->value.asMessage.pObject->value.asSymbol.name, "Self" ) == 0 )
               fprintf( yyc, "this" );
            else
               hb_csEmitExpr( pExpr->value.asMessage.pObject, yyc, HB_TRUE );
         }
         else if( s_pWithObject )
         {
            /* No explicit object — use WITH OBJECT expression directly */
            hb_csEmitExpr( s_pWithObject, yyc, HB_TRUE );
         }
         if( pExpr->value.asMessage.szMessage )
            fprintf( yyc, ".%s", pExpr->value.asMessage.szMessage );
         else if( pExpr->value.asMessage.pMessage )
         {
            fprintf( yyc, "." );
            hb_csEmitExpr( pExpr->value.asMessage.pMessage, yyc, HB_FALSE );
         }
         if( pExpr->value.asMessage.pParms )
         {
            fprintf( yyc, "(" );
            hb_csEmitCallArgs( pExpr->value.asMessage.szMessage,
                               pExpr->value.asMessage.pParms, yyc );
            fprintf( yyc, ")" );
         }
         break;

      case HB_ET_ARRAYAT:
         hb_csEmitExpr( pExpr->value.asList.pExprList, yyc, HB_TRUE );
         /* String keys (hashes) — no index adjustment */
         if( pExpr->value.asList.pIndex &&
             pExpr->value.asList.pIndex->ExprType == HB_ET_STRING )
         {
            fprintf( yyc, "[" );
            hb_csEmitExpr( pExpr->value.asList.pIndex, yyc, HB_FALSE );
            fprintf( yyc, "]" );
         }
         /* Integer literal index — emit decremented value directly */
         else if( pExpr->value.asList.pIndex &&
                  pExpr->value.asList.pIndex->ExprType == HB_ET_NUMERIC &&
                  pExpr->value.asList.pIndex->value.asNum.NumType == HB_ET_LONG )
         {
            fprintf( yyc, "[%" HB_PFS "d]",
                     pExpr->value.asList.pIndex->value.asNum.val.l - 1 );
         }
         /* Variable or expression index — cast and subtract at runtime */
         else
         {
            fprintf( yyc, "[(int)(" );
            hb_csEmitExpr( pExpr->value.asList.pIndex, yyc, HB_FALSE );
            fprintf( yyc, ") - 1]" );
         }
         break;

      case HB_ET_ARRAY:
         {
            PHB_EXPR pItem;
            fprintf( yyc, "new dynamic[] { " );
            pItem = pExpr->value.asList.pExprList;
            while( pItem )
            {
               hb_csEmitExpr( pItem, yyc, HB_FALSE );
               pItem = pItem->pNext;
               if( pItem )
                  fprintf( yyc, ", " );
            }
            fprintf( yyc, " }" );
         }
         break;

      case HB_ET_HASH:
         {
            PHB_EXPR pItem;
            fprintf( yyc, "new Dictionary<dynamic, dynamic> { " );
            pItem = pExpr->value.asList.pExprList;
            while( pItem )
            {
               fprintf( yyc, "{ " );
               hb_csEmitExpr( pItem, yyc, HB_FALSE );
               pItem = pItem->pNext;
               if( pItem )
               {
                  fprintf( yyc, ", " );
                  hb_csEmitExpr( pItem, yyc, HB_FALSE );
                  pItem = pItem->pNext;
               }
               fprintf( yyc, " }" );
               if( pItem )
                  fprintf( yyc, ", " );
            }
            fprintf( yyc, " }" );
         }
         break;

      case HB_ET_IIF:
         /* IIF(cond, true, false) → (cond ? true : false) */
         if( pExpr->value.asList.pExprList )
         {
            PHB_EXPR pCond = pExpr->value.asList.pExprList;
            HB_BOOL fWrap = hb_csConditionNeedsBoolUnwrap( pCond );
            fprintf( yyc, "(" );
            hb_csEmitExpr( pCond, yyc, HB_FALSE );
            if( fWrap )
               fprintf( yyc, " == true" );
            if( pCond->pNext )
            {
               fprintf( yyc, " ? " );
               hb_csEmitExpr( pCond->pNext, yyc, HB_FALSE );
               if( pCond->pNext->pNext )
               {
                  fprintf( yyc, " : " );
                  hb_csEmitExpr( pCond->pNext->pNext, yyc, HB_FALSE );
               }
            }
            fprintf( yyc, ")" );
         }
         break;

      case HB_ET_LIST:
      case HB_ET_ARGLIST:
      case HB_ET_MACROARGLIST:
         {
            PHB_EXPR pItem = pExpr->value.asList.pExprList;
            /* `...` call argument and `{ ... }` array-spread literal
               both arrive as an HB_ET_ARGLIST with `reference=TRUE`
               and an empty child list (see hb_compExprNewArgRef).
               Emit `hbva` — the lambda's vararg array — which lines
               up with the name used by the HB_ET_CODEBLOCK VPARAMS
               emission above and by the vararg preamble in
               hb_csEmitFunc. Callee must accept `params dynamic[]`;
               the scanner flags the callee (fCalledVarargs) so its
               signature widens to match. */
            if( pExpr->ExprType == HB_ET_ARGLIST &&
                pExpr->value.asList.reference && ! pItem )
            {
               fprintf( yyc, "hbva" );
               break;
            }
            while( pItem )
            {
               hb_csEmitExpr( pItem, yyc, HB_FALSE );
               pItem = pItem->pNext;
               if( pItem )
                  fprintf( yyc, ", " );
            }
         }
         break;

      case HB_ET_MACRO:
         /* Macros can't be transpiled — emit as comment */
         if( pExpr->value.asMacro.szMacro )
            fprintf( yyc, "/* MACRO: &%s */", pExpr->value.asMacro.szMacro );
         else
            fprintf( yyc, "/* MACRO */" );
         break;

      case HB_ET_ALIASVAR:
         /* Harbour's parser auto-wraps identifiers it can't resolve
            against the current scope as implicit memvar aliases —
            producing HB_ET_ALIASVAR with a bare HB_ET_ALIAS keyword
            on the alias side. This happens in two situations real
            codebases hit all the time:

              1) Case-typo'd locals. Harbour is case-insensitive but
                 the parser stores the first-seen casing as the local's
                 canonical name, so a later reference with different
                 case (`aRetval` vs `aRetVal`) fails to resolve and gets
                 wrapped. We catch these by doing a case-insensitive
                 lookup against the current function's locals list and
                 emitting the canonical name unwrapped.

              2) Real unqualified memvars (PRIVATE/PUBLIC). These can't
                 be resolved at emit time and get emitted as bare
                 identifiers — C# will complain, but with a clean
                 "undefined identifier" rather than malformed syntax.

            Named-alias references (`WORKAREA->field`) keep the comment
            fallback, which stays syntactically broken but is at least
            recognizable for triage. */
         if( pExpr->value.asAlias.pAlias &&
             pExpr->value.asAlias.pAlias->ExprType == HB_ET_ALIAS &&
             pExpr->value.asAlias.pVar &&
             pExpr->value.asAlias.pVar->ExprType == HB_ET_VARIABLE )
         {
            const char * szVarName = pExpr->value.asAlias.pVar->value.asSymbol.name;
            const char * szLocal = hb_csResolveLocal( szVarName );
            fprintf( yyc, "%s", szLocal ? szLocal : ( szVarName ? szVarName : "" ) );
         }
         else
         {
            fprintf( yyc, "/* ALIAS: " );
            hb_csEmitExpr( pExpr->value.asAlias.pAlias, yyc, HB_FALSE );
            fprintf( yyc, "->" );
            hb_csEmitExpr( pExpr->value.asAlias.pVar, yyc, HB_FALSE );
            fprintf( yyc, " */" );
         }
         break;

      case HB_ET_ALIASEXPR:
         fprintf( yyc, "/* ALIAS: " );
         hb_csEmitExpr( pExpr->value.asAlias.pAlias, yyc, HB_FALSE );
         fprintf( yyc, "->(" );
         hb_csEmitExpr( pExpr->value.asAlias.pExpList, yyc, HB_FALSE );
         fprintf( yyc, ") */" );
         break;

      case HB_ET_ALIAS:
         /* Bare alias keyword (FIELD, MEMVAR, or the implicit wrapper
            the parser inserts for unresolved identifiers). When it
            appears outside an ALIASVAR wrapper — which shouldn't happen
            in well-formed code but occasionally does in the tail of an
            ALIASVAR's alias side that escaped the HB_ET_ALIASVAR handler
            above — emit the keyword name as a comment so downstream
            context is preserved instead of producing `unknown expr type 26`. */
         fprintf( yyc, "/* %s */",
                  pExpr->value.asSymbol.name ? pExpr->value.asSymbol.name : "ALIAS" );
         break;

      case HB_ET_CODEBLOCK:
         {
            PHB_CBVAR pVar = pExpr->value.asCodeblock.pLocals;
            HB_BOOL fVParams =
               ( pExpr->value.asCodeblock.flags & HB_BLOCK_VPARAMS ) != 0;
            if( fVParams )
            {
               /* `{|...| body}` — emit as a Func<dynamic[], dynamic>
                  so the lambda has a uniform shape that HbRuntime.EVAL
                  (and downstream callers that treat the block as
                  dynamic) can invoke by packing args into an array.
                  Named params listed before `...` bind to the leading
                  slots of `hbva`.

                  The body is emitted as a statement (expression + ';')
                  followed by `return null` rather than `return expr`,
                  because `...` codeblocks overwhelmingly forward to a
                  procedure (`Echo(...)`) whose call can't legally sit
                  on the right of `return` in C# (void has no value).
                  Harbour blocks then naturally evaluate to NIL, which
                  matches `return null`. */
               fprintf( yyc, "((Func<dynamic[], dynamic>)((dynamic[] hbva) => { " );
               {
                  int iLocal = 0;
                  while( pVar )
                  {
                     fprintf( yyc, "dynamic %s = hbva.Length > %d ? hbva[%d] : null; ",
                              pVar->szName, iLocal, iLocal );
                     pVar = pVar->pNext;
                     iLocal++;
                  }
               }
               if( pExpr->value.asCodeblock.pExprList )
               {
                  hb_csEmitExpr( pExpr->value.asCodeblock.pExprList, yyc, HB_FALSE );
                  fprintf( yyc, "; " );
               }
               fprintf( yyc, "return null; }))" );
            }
            else
            {
               fprintf( yyc, "(" );
               if( pVar )
               {
                  fprintf( yyc, "(" );
                  while( pVar )
                  {
                     fprintf( yyc, "%s", pVar->szName );
                     pVar = pVar->pNext;
                     if( pVar )
                        fprintf( yyc, ", " );
                  }
                  fprintf( yyc, ")" );
               }
               else
                  fprintf( yyc, "()" );
               fprintf( yyc, " => " );
               if( pExpr->value.asCodeblock.pExprList )
                  hb_csEmitExpr( pExpr->value.asCodeblock.pExprList, yyc, HB_FALSE );
               fprintf( yyc, ")" );
            }
         }
         break;

      case HB_ET_DATE:
      {
         int iYear, iMonth, iDay;
         hb_dateDecode( pExpr->value.asDate.lDate, &iYear, &iMonth, &iDay );
         if( iYear == 0 && iMonth == 0 && iDay == 0 )
            fprintf( yyc, "default(DateOnly)" );
         else
            fprintf( yyc, "new DateOnly(%d, %d, %d)", iYear, iMonth, iDay );
         break;
      }

      case HB_ET_TIMESTAMP:
      {
         int iYear, iMonth, iDay, iHour, iMin, iSec, iMSec;
         hb_dateDecode( pExpr->value.asDate.lDate, &iYear, &iMonth, &iDay );
         hb_timeDecode( pExpr->value.asDate.lTime, &iHour, &iMin, &iSec, &iMSec );
         if( iYear == 0 && iMonth == 0 && iDay == 0 &&
             iHour == 0 && iMin == 0 && iSec == 0 && iMSec == 0 )
            fprintf( yyc, "default(DateTime)" );
         else
            fprintf( yyc, "new DateTime(%d, %d, %d, %d, %d, %d, %d)",
                     iYear, iMonth, iDay, iHour, iMin, iSec, iMSec );
         break;
      }

      case HB_ET_RTVAR:
         if( pExpr->value.asRTVar.szName )
            fprintf( yyc, "%s", pExpr->value.asRTVar.szName );
         else if( pExpr->value.asRTVar.pMacro )
            hb_csEmitExpr( pExpr->value.asRTVar.pMacro, yyc, HB_FALSE );
         break;

      case HB_ET_SETGET:
         hb_csEmitExpr( pExpr->value.asSetGet.pVar, yyc, HB_FALSE );
         break;

      case HB_EO_NEGATE:
         fprintf( yyc, "-" );
         hb_csEmitExpr( pExpr->value.asOperator.pLeft, yyc, HB_TRUE );
         break;

      case HB_EO_NOT:
         fprintf( yyc, "!" );
         hb_csEmitExpr( pExpr->value.asOperator.pLeft, yyc, HB_TRUE );
         break;

      case HB_EO_PREINC:
         fprintf( yyc, "++" );
         hb_csEmitExpr( pExpr->value.asOperator.pLeft, yyc, HB_TRUE );
         break;

      case HB_EO_PREDEC:
         fprintf( yyc, "--" );
         hb_csEmitExpr( pExpr->value.asOperator.pLeft, yyc, HB_TRUE );
         break;

      case HB_EO_POSTINC:
         hb_csEmitExpr( pExpr->value.asOperator.pLeft, yyc, HB_TRUE );
         fprintf( yyc, "++" );
         break;

      case HB_EO_POSTDEC:
         hb_csEmitExpr( pExpr->value.asOperator.pLeft, yyc, HB_TRUE );
         fprintf( yyc, "--" );
         break;

      default:
         /* Binary operators */
         if( pExpr->ExprType >= HB_EO_ASSIGN && pExpr->ExprType <= HB_EO_PREDEC )
         {
            /* Handle special operators */
            if( pExpr->ExprType == HB_EO_POWER )
            {
               /* a ^ b → Math.Pow(a, b) */
               fprintf( yyc, "Math.Pow(" );
               hb_csEmitExpr( pExpr->value.asOperator.pLeft, yyc, HB_FALSE );
               fprintf( yyc, ", " );
               hb_csEmitExpr( pExpr->value.asOperator.pRight, yyc, HB_FALSE );
               fprintf( yyc, ")" );
            }
            else if( pExpr->ExprType == HB_EO_IN )
            {
               /* a $ b → b.Contains(a) */
               hb_csEmitExpr( pExpr->value.asOperator.pRight, yyc, HB_TRUE );
               fprintf( yyc, ".Contains(" );
               hb_csEmitExpr( pExpr->value.asOperator.pLeft, yyc, HB_FALSE );
               fprintf( yyc, ")" );
            }
            else
            {
               HB_BOOL fNeedParen = HB_FALSE;
               if( fParen )
               {
                  PHB_EXPR pLeft = pExpr->value.asOperator.pLeft;
                  PHB_EXPR pRight = pExpr->value.asOperator.pRight;
                  if( ( pLeft && pLeft->ExprType >= HB_EO_ASSIGN &&
                        pLeft->ExprType < pExpr->ExprType ) ||
                      ( pRight && pRight->ExprType >= HB_EO_ASSIGN &&
                        pRight->ExprType < pExpr->ExprType ) )
                     fNeedParen = HB_TRUE;
               }
               if( fNeedParen )
                  fprintf( yyc, "(" );
               hb_csEmitExpr( pExpr->value.asOperator.pLeft, yyc, HB_TRUE );
               fprintf( yyc, "%s", hb_csOperatorStr( pExpr->ExprType ) );
               hb_csEmitExpr( pExpr->value.asOperator.pRight, yyc, HB_TRUE );
               if( fNeedParen )
                  fprintf( yyc, ")" );
            }
         }
         else
         {
            fprintf( yyc, "/* unknown expr type %d */", pExpr->ExprType );
         }
         break;
   }
}

/* Check if a block ends with EXIT/BREAK/RETURN (maps to break/throw/return in C#) */
static HB_BOOL hb_csBlockEndsWithBreak( PHB_AST_NODE pBlock )
{
   PHB_AST_NODE pLast = NULL;
   if( ! pBlock )
      return HB_FALSE;
   if( pBlock->type == HB_AST_BLOCK )
      pLast = pBlock->value.asBlock.pLast;
   else
      pLast = pBlock;
   if( ! pLast )
      return HB_FALSE;
   return pLast->type == HB_AST_EXIT || pLast->type == HB_AST_BREAK ||
          pLast->type == HB_AST_RETURN;
}

/* ---- Statement emitter ---- */

static void hb_csEmitNode( PHB_AST_NODE pNode, FILE * yyc, int iIndent )
{
   if( ! pNode )
      return;

   /* Blank line preservation */
   switch( pNode->type )
   {
      case HB_AST_STATIC:
      case HB_AST_MEMVAR:
         /* Skipped nodes — don't update line tracking so they
            don't create spurious blank lines in the output */
         break;

      case HB_AST_EXPRSTMT:
      case HB_AST_LOCAL:
      case HB_AST_PUBLIC:
      case HB_AST_PRIVATE:
      case HB_AST_RETURN:
      case HB_AST_EXIT:
      case HB_AST_LOOP:
      case HB_AST_BREAK:
      case HB_AST_COMMENT:
         hb_csEmitBlankLines( yyc, pNode->iLine );
         break;
      default:
         if( pNode->iLine > 0 && s_iLastLine > 0 && pNode->iLine > s_iLastLine + 1 )
            fprintf( yyc, "\n" );
         s_iLastLine = 0;
         break;
   }

   switch( pNode->type )
   {
      case HB_AST_EXPRSTMT:
         hb_csEmitIndent( yyc, iIndent );
         hb_csEmitExpr( pNode->value.asExprStmt.pExpr, yyc, HB_FALSE );
         fprintf( yyc, ";\n" );
         break;

      case HB_AST_RETURN:
         hb_csEmitIndent( yyc, iIndent );
         if( pNode->value.asReturn.pExpr && ! s_fVoidFunc )
         {
            fprintf( yyc, "return " );
            hb_csEmitExpr( pNode->value.asReturn.pExpr, yyc, HB_FALSE );
            fprintf( yyc, ";\n" );
         }
         else
            fprintf( yyc, "return;\n" );
         break;

      case HB_AST_QOUT:
         hb_csEmitIndent( yyc, iIndent );
         fprintf( yyc, "HbRuntime.QOut(" );
         if( pNode->value.asQOut.pExprList )
            hb_csEmitExpr( pNode->value.asQOut.pExprList, yyc, HB_FALSE );
         fprintf( yyc, ");\n" );
         break;

      case HB_AST_QQOUT:
         hb_csEmitIndent( yyc, iIndent );
         fprintf( yyc, "HbRuntime.QQOut(" );
         if( pNode->value.asQOut.pExprList )
            hb_csEmitExpr( pNode->value.asQOut.pExprList, yyc, HB_FALSE );
         fprintf( yyc, ");\n" );
         break;

      case HB_AST_LOCAL:
         {
            const char * szType = NULL;

            /* Use propagated type if available, otherwise infer */
            if( pNode->value.asVar.szAlias )
               szType = pNode->value.asVar.szAlias;
            else
               szType = hb_astInferType( pNode->value.asVar.szName,
                                          pNode->value.asVar.pInit );

            if( pNode->value.asVar.pInit )
            {
               /* Check if init is ClassName():New() constructor pattern */
               const char * szCtor = hb_csIsConstructor( pNode->value.asVar.pInit );
               if( szCtor )
                  szType = szCtor;

               /* Type name = value; */
               hb_csEmitIndent( yyc, iIndent );
               /* Codeblock initializers need explicit Func<> type */
               if( pNode->value.asVar.pInit->ExprType == HB_ET_CODEBLOCK )
               {
                  HB_BOOL fVParamsBlock =
                     ( pNode->value.asVar.pInit->value.asCodeblock.flags
                       & HB_BLOCK_VPARAMS ) != 0;
                  if( fVParamsBlock )
                  {
                     /* `{|...| body}` — uniform dynamic[] shape,
                        matches the cast emitted in HB_ET_CODEBLOCK. */
                     fprintf( yyc, "Func<dynamic[], dynamic> %s = ",
                              pNode->value.asVar.szName );
                  }
                  else
                  {
                     PHB_CBVAR pCBVar = pNode->value.asVar.pInit->value.asCodeblock.pLocals;
                     int nParams = 0;
                     int j;
                     while( pCBVar ) { nParams++; pCBVar = pCBVar->pNext; }
                     fprintf( yyc, "Func<" );
                     for( j = 0; j < nParams; j++ )
                        fprintf( yyc, "dynamic, " );
                     fprintf( yyc, "dynamic> %s = ", pNode->value.asVar.szName );
                  }
               }
               else
                  fprintf( yyc, "%s %s = ", hb_csTypeMap( szType ),
                           pNode->value.asVar.szName );
               hb_csEmitExpr( pNode->value.asVar.pInit, yyc, HB_FALSE );
               fprintf( yyc, ";\n" );
            }
            else
            {
               /* No initializer — check if this variable is used as a
                  foreach iterator or catch variable, in which case skip
                  the declaration (it will be declared by foreach/catch) */
               HB_BOOL fSkip = HB_FALSE;
               HB_BOOL fUsedInFor = HB_FALSE;
               const char * szVarName = pNode->value.asVar.szName;
               PHB_AST_NODE pSibling = pNode->pNext;

               /* First check if variable is used as a FOR loop counter */
               while( pSibling )
               {
                  if( pSibling->type == HB_AST_FOR &&
                      hb_stricmp( pSibling->value.asFor.szVar, szVarName ) == 0 )
                  {
                     fUsedInFor = HB_TRUE;
                     break;
                  }
                  pSibling = pSibling->pNext;
               }

               /* Only suppress if used in foreach/catch and NOT in a FOR loop */
               pSibling = pNode->pNext;
               while( pSibling && ! fSkip && ! fUsedInFor )
               {
                  if( pSibling->type == HB_AST_FOREACH &&
                      pSibling->value.asForEach.pVar )
                  {
                     PHB_EXPR pFEVar = pSibling->value.asForEach.pVar;
                     const char * szFEName = NULL;
                     /* Unwrap ARGLIST/LIST wrapper if present */
                     if( ( pFEVar->ExprType == HB_ET_ARGLIST ||
                           pFEVar->ExprType == HB_ET_LIST ) &&
                         pFEVar->value.asList.pExprList )
                        pFEVar = pFEVar->value.asList.pExprList;
                     if( pFEVar->ExprType == HB_ET_VARIABLE ||
                         pFEVar->ExprType == HB_ET_VARREF )
                        szFEName = pFEVar->value.asSymbol.name;
                     if( szFEName &&
                         hb_stricmp( szFEName, szVarName ) == 0 )
                        fSkip = HB_TRUE;
                  }
                  if( pSibling->type == HB_AST_BEGINSEQ &&
                      pSibling->value.asSeq.szRecoverVar &&
                      hb_stricmp( pSibling->value.asSeq.szRecoverVar,
                                  szVarName ) == 0 )
                     fSkip = HB_TRUE;
                  pSibling = pSibling->pNext;
               }
               if( ! fSkip )
               {
                  hb_csEmitIndent( yyc, iIndent );
                  fprintf( yyc, "%s %s = default;\n", hb_csTypeMap( szType ),
                           pNode->value.asVar.szName );
               }
            }
         }
         break;

      case HB_AST_STATIC:
         /* STATIC is emitted as a static class field — skip in method body. */
         break;

      case HB_AST_MEMVAR:
         /* MEMVAR is a declaration hint — no C# equivalent needed. */
         break;

      case HB_AST_PUBLIC:
      case HB_AST_PRIVATE:
         {
            const char * szType = NULL;
            hb_csEmitIndent( yyc, iIndent );

            if( pNode->value.asVar.szAlias )
               szType = pNode->value.asVar.szAlias;
            else
               szType = hb_astInferType( pNode->value.asVar.szName,
                                          pNode->value.asVar.pInit );

            /* PUBLIC/PRIVATE → regular local in C# */
            fprintf( yyc, "%s %s", hb_csTypeMap( szType ),
                     pNode->value.asVar.szName );
            if( pNode->value.asVar.pInit )
            {
               fprintf( yyc, " = " );
               hb_csEmitExpr( pNode->value.asVar.pInit, yyc, HB_FALSE );
            }
            fprintf( yyc, ";\n" );
         }
         break;

      case HB_AST_IF:
         hb_csEmitIndent( yyc, iIndent );
         fprintf( yyc, "if (" );
         {
            HB_BOOL fWrap = hb_csConditionNeedsBoolUnwrap(
               pNode->value.asIf.pCondition );
            hb_csEmitExpr( pNode->value.asIf.pCondition, yyc, HB_FALSE );
            if( fWrap )
               fprintf( yyc, " == true" );
         }
         fprintf( yyc, ")\n" );
         hb_csEmitIndent( yyc, iIndent );
         fprintf( yyc, "{\n" );
         if( pNode->value.asIf.pThen )
            hb_csEmitBlock( pNode->value.asIf.pThen, yyc, iIndent + 1 );
         hb_csEmitIndent( yyc, iIndent );
         fprintf( yyc, "}\n" );

         /* ELSEIF chain */
         {
            PHB_AST_NODE pElseIf = pNode->value.asIf.pElseIfs;
            while( pElseIf )
            {
               HB_BOOL fWrap = hb_csConditionNeedsBoolUnwrap(
                  pElseIf->value.asElseIf.pCondition );
               hb_csEmitIndent( yyc, iIndent );
               fprintf( yyc, "else if (" );
               hb_csEmitExpr( pElseIf->value.asElseIf.pCondition, yyc, HB_FALSE );
               if( fWrap )
                  fprintf( yyc, " == true" );
               fprintf( yyc, ")\n" );
               hb_csEmitIndent( yyc, iIndent );
               fprintf( yyc, "{\n" );
               s_iLastLine = 0;
               if( pElseIf->value.asElseIf.pBody )
                  hb_csEmitBlock( pElseIf->value.asElseIf.pBody, yyc, iIndent + 1 );
               hb_csEmitIndent( yyc, iIndent );
               fprintf( yyc, "}\n" );
               pElseIf = pElseIf->pNext;
            }
         }

         if( pNode->value.asIf.pElse )
         {
            hb_csEmitIndent( yyc, iIndent );
            fprintf( yyc, "else\n" );
            hb_csEmitIndent( yyc, iIndent );
            fprintf( yyc, "{\n" );
            s_iLastLine = 0;
            hb_csEmitBlock( pNode->value.asIf.pElse, yyc, iIndent + 1 );
            hb_csEmitIndent( yyc, iIndent );
            fprintf( yyc, "}\n" );
         }
         break;

      case HB_AST_DOWHILE:
         hb_csEmitIndent( yyc, iIndent );
         fprintf( yyc, "while (" );
         hb_csEmitExpr( pNode->value.asWhile.pCondition, yyc, HB_FALSE );
         fprintf( yyc, ")\n" );
         hb_csEmitIndent( yyc, iIndent );
         fprintf( yyc, "{\n" );
         if( pNode->value.asWhile.pBody )
            hb_csEmitBlock( pNode->value.asWhile.pBody, yyc, iIndent + 1 );
         hb_csEmitIndent( yyc, iIndent );
         fprintf( yyc, "}\n" );
         break;

      case HB_AST_FOR:
         {
            HB_BOOL fDescend = HB_FALSE;
            hb_csEmitIndent( yyc, iIndent );

            /* Detect descending: negative step literal */
            if( pNode->value.asFor.pStep &&
                pNode->value.asFor.pStep->ExprType == HB_ET_NUMERIC &&
                pNode->value.asFor.pStep->value.asNum.NumType == HB_ET_LONG &&
                pNode->value.asFor.pStep->value.asNum.val.l < 0 )
               fDescend = HB_TRUE;

            fprintf( yyc, "for (%s = ", pNode->value.asFor.szVar );
            hb_csEmitExpr( pNode->value.asFor.pStart, yyc, HB_FALSE );
            fprintf( yyc, "; %s %s ", pNode->value.asFor.szVar,
                     fDescend ? ">=" : "<=" );
            hb_csEmitExpr( pNode->value.asFor.pEnd, yyc, HB_FALSE );
            fprintf( yyc, "; %s", pNode->value.asFor.szVar );
            if( pNode->value.asFor.pStep )
            {
               fprintf( yyc, " += " );
               hb_csEmitExpr( pNode->value.asFor.pStep, yyc, HB_FALSE );
            }
            else
               fprintf( yyc, "++" );
            fprintf( yyc, ")\n" );
            hb_csEmitIndent( yyc, iIndent );
            fprintf( yyc, "{\n" );
            if( pNode->value.asFor.pBody )
               hb_csEmitBlock( pNode->value.asFor.pBody, yyc, iIndent + 1 );
            hb_csEmitIndent( yyc, iIndent );
            fprintf( yyc, "}\n" );
         }
         break;

      case HB_AST_FOREACH:
         hb_csEmitIndent( yyc, iIndent );
         fprintf( yyc, "foreach (dynamic " );
         hb_csEmitExpr( pNode->value.asForEach.pVar, yyc, HB_FALSE );
         fprintf( yyc, " in " );
         hb_csEmitExpr( pNode->value.asForEach.pEnum, yyc, HB_FALSE );
         if( pNode->value.asForEach.iDir < 0 )
            fprintf( yyc, ".Reverse()" );
         fprintf( yyc, ")\n" );
         hb_csEmitIndent( yyc, iIndent );
         fprintf( yyc, "{\n" );
         if( pNode->value.asForEach.pBody )
            hb_csEmitBlock( pNode->value.asForEach.pBody, yyc, iIndent + 1 );
         hb_csEmitIndent( yyc, iIndent );
         fprintf( yyc, "}\n" );
         break;

      case HB_AST_EXIT:
         hb_csEmitIndent( yyc, iIndent );
         fprintf( yyc, "break;\n" );
         break;

      case HB_AST_LOOP:
         hb_csEmitIndent( yyc, iIndent );
         fprintf( yyc, "continue;\n" );
         break;

      case HB_AST_BREAK:
         hb_csEmitIndent( yyc, iIndent );
         fprintf( yyc, "throw new Exception(" );
         if( pNode->value.asBreak.pExpr )
            hb_csEmitExpr( pNode->value.asBreak.pExpr, yyc, HB_FALSE );
         fprintf( yyc, ");\n" );
         break;

      case HB_AST_DOCASE:
         /* DO CASE → if/else if chain */
         {
            PHB_AST_NODE pCase = pNode->value.asDoCase.pCases;
            HB_BOOL fFirst = HB_TRUE;
            while( pCase )
            {
               hb_csEmitIndent( yyc, iIndent );
               if( fFirst )
               {
                  fprintf( yyc, "if (" );
                  fFirst = HB_FALSE;
               }
               else
                  fprintf( yyc, "else if (" );
               hb_csEmitExpr( pCase->value.asCase.pCondition, yyc, HB_FALSE );
               fprintf( yyc, ")\n" );
               hb_csEmitIndent( yyc, iIndent );
               fprintf( yyc, "{\n" );
               s_iLastLine = 0;
               if( pCase->value.asCase.pBody )
                  hb_csEmitBlock( pCase->value.asCase.pBody, yyc, iIndent + 1 );
               hb_csEmitIndent( yyc, iIndent );
               fprintf( yyc, "}\n" );
               pCase = pCase->pNext;
            }
         }
         if( pNode->value.asDoCase.pOtherwise )
         {
            hb_csEmitIndent( yyc, iIndent );
            fprintf( yyc, "else\n" );
            hb_csEmitIndent( yyc, iIndent );
            fprintf( yyc, "{\n" );
            s_iLastLine = 0;
            hb_csEmitBlock( pNode->value.asDoCase.pOtherwise, yyc, iIndent + 1 );
            hb_csEmitIndent( yyc, iIndent );
            fprintf( yyc, "}\n" );
         }
         break;

      case HB_AST_SWITCH:
         hb_csEmitIndent( yyc, iIndent );
         fprintf( yyc, "switch (" );
         hb_csEmitExpr( pNode->value.asSwitch.pSwitch, yyc, HB_FALSE );
         fprintf( yyc, ")\n" );
         hb_csEmitIndent( yyc, iIndent );
         fprintf( yyc, "{\n" );
         {
            PHB_AST_NODE pCase = pNode->value.asSwitch.pCases;
            while( pCase )
            {
               /* Skip default case (NULL condition) — handled below */
               if( ! pCase->value.asCase.pCondition )
               {
                  pCase = pCase->pNext;
                  continue;
               }
               hb_csEmitIndent( yyc, iIndent + 1 );
               fprintf( yyc, "case " );
               hb_csEmitExpr( pCase->value.asCase.pCondition, yyc, HB_FALSE );
               fprintf( yyc, ":\n" );
               s_iLastLine = 0;
               if( pCase->value.asCase.pBody )
               {
                  hb_csEmitBlock( pCase->value.asCase.pBody, yyc, iIndent + 2 );
                  if( ! hb_csBlockEndsWithBreak( pCase->value.asCase.pBody ) )
                  {
                     hb_csEmitIndent( yyc, iIndent + 2 );
                     fprintf( yyc, "break;\n" );
                  }
               }
               else
               {
                  hb_csEmitIndent( yyc, iIndent + 2 );
                  fprintf( yyc, "break;\n" );
               }
               pCase = pCase->pNext;
            }
         }
         if( pNode->value.asSwitch.pDefault )
         {
            hb_csEmitIndent( yyc, iIndent + 1 );
            fprintf( yyc, "default:\n" );
            s_iLastLine = 0;
            hb_csEmitBlock( pNode->value.asSwitch.pDefault, yyc, iIndent + 2 );
            if( ! hb_csBlockEndsWithBreak( pNode->value.asSwitch.pDefault ) )
            {
               hb_csEmitIndent( yyc, iIndent + 2 );
               fprintf( yyc, "break;\n" );
            }
         }
         hb_csEmitIndent( yyc, iIndent );
         fprintf( yyc, "}\n" );
         break;

      case HB_AST_BEGINSEQ:
         /* BEGIN SEQUENCE → try/catch/finally */
         hb_csEmitIndent( yyc, iIndent );
         fprintf( yyc, "try\n" );
         hb_csEmitIndent( yyc, iIndent );
         fprintf( yyc, "{\n" );
         s_iLastLine = 0;
         if( pNode->value.asSeq.pBody )
            hb_csEmitBlock( pNode->value.asSeq.pBody, yyc, iIndent + 1 );
         hb_csEmitIndent( yyc, iIndent );
         fprintf( yyc, "}\n" );
         if( pNode->value.asSeq.pRecover )
         {
            hb_csEmitIndent( yyc, iIndent );
            if( pNode->value.asSeq.szRecoverVar )
               fprintf( yyc, "catch (Exception %s)\n",
                        pNode->value.asSeq.szRecoverVar );
            else
               fprintf( yyc, "catch\n" );
            hb_csEmitIndent( yyc, iIndent );
            fprintf( yyc, "{\n" );
            s_iLastLine = 0;
            hb_csEmitBlock( pNode->value.asSeq.pRecover, yyc, iIndent + 1 );
            hb_csEmitIndent( yyc, iIndent );
            fprintf( yyc, "}\n" );
         }
         if( pNode->value.asSeq.pAlways )
         {
            hb_csEmitIndent( yyc, iIndent );
            fprintf( yyc, "finally\n" );
            hb_csEmitIndent( yyc, iIndent );
            fprintf( yyc, "{\n" );
            s_iLastLine = 0;
            hb_csEmitBlock( pNode->value.asSeq.pAlways, yyc, iIndent + 1 );
            hb_csEmitIndent( yyc, iIndent );
            fprintf( yyc, "}\n" );
         }
         break;

      case HB_AST_WITHOBJECT:
         {
            /* WITH OBJECT — emit body with implicit object reference */
            PHB_EXPR pSavedWith = s_pWithObject;
            s_pWithObject = pNode->value.asWithObj.pObject;
            s_iLastLine = 0;
            if( pNode->value.asWithObj.pBody )
               hb_csEmitBlock( pNode->value.asWithObj.pBody, yyc, iIndent );
            s_pWithObject = pSavedWith;
         }
         break;

      case HB_AST_COMMENT:
         hb_csEmitIndent( yyc, iIndent );
         {
            const char * szText = pNode->value.asComment.szText;
            /* Convert Harbour-specific comment styles to C# // */
            if( szText[ 0 ] == '*' && szText[ 1 ] == ' ' )
               fprintf( yyc, "//%s\n", szText + 1 );
            else if( szText[ 0 ] == '*' )
               fprintf( yyc, "// %s\n", szText + 1 );
            else if( hb_strnicmp( szText, "NOTE ", 5 ) == 0 )
               fprintf( yyc, "// %s\n", szText + 5 );
            else if( hb_strnicmp( szText, "NOTE\t", 5 ) == 0 )
               fprintf( yyc, "// %s\n", szText + 5 );
            else if( szText[ 0 ] == '&' && szText[ 1 ] == '&' )
               fprintf( yyc, "//%s\n", szText + 2 );
            else
               fprintf( yyc, "%s\n", szText );
         }
         break;

      case HB_AST_INCLUDE:
         /* #include → // #include for reference */
         hb_csEmitIndent( yyc, iIndent );
         fprintf( yyc, "// #include \"%s\"\n", pNode->value.asInclude.szFile );
         break;

      case HB_AST_PPDEFINE:
         {
            /* Try to emit as const if it's a simple name-value define */
            const char * sz = pNode->value.asDefine.szDefine;
            const char * p = sz;
            char szName[ 256 ];
            HB_SIZE n = 0;

            /* Extract name (identifier characters) */
            while( *p && *p != ' ' && *p != '\t' && *p != '(' && n < sizeof( szName ) - 1 )
               szName[ n++ ] = *p++;
            szName[ n ] = '\0';

            /* Skip whitespace between name and value */
            while( *p == ' ' || *p == '\t' )
               p++;

            if( *p == '(' || *p == '\0' )
            {
               /* Macro with parameters or no value — emit as comment */
               hb_csEmitIndent( yyc, iIndent );
               fprintf( yyc, "// #define %s\n", sz );
            }
            else if( *p == '"' || *p == '\'' )
            {
               /* String constant. Harbour string literals have no escape
                  processing (`"\"` is a one-byte backslash), so a C#
                  verbatim string (`@"..."`) is a perfect semantic match:
                  backslash stays literal, only `"` needs doubling. Using
                  a regular string instead would require escaping every
                  `\` and every `"`, which the previous pass-through
                  implementation didn't do at all — producing broken
                  literals for any source-level backslash. */
               char cQuote = *p;
               const char * pInner = p + 1;
               HB_SIZE nInner;
               HB_SIZE i;

               nInner = strlen( pInner );
               if( nInner > 0 && pInner[ nInner - 1 ] == cQuote )
                  nInner--;

               hb_csEmitIndent( yyc, iIndent );
               fprintf( yyc, "const string %s = @\"", szName );
               for( i = 0; i < nInner; i++ )
               {
                  if( pInner[ i ] == '"' )
                     fprintf( yyc, "\"\"" );
                  else
                     fputc( pInner[ i ], yyc );
               }
               fprintf( yyc, "\";\n" );
            }
            else if( ( *p >= '0' && *p <= '9' ) || *p == '-' || *p == '+' )
            {
               /* Numeric constant. Parse just the literal and split off any
                  trailing comment before appending the `m` suffix and `;`
                  — otherwise a `#define X 2 // comment` would round-trip as
                  `const decimal X = 2 // commentm;` where both the suffix
                  and the terminator get swallowed by the line comment. */
               const char * pNumStart = p;
               const char * pNumEnd = p;
               const char * pTrail;
               HB_BOOL fHasDot = HB_FALSE;

               if( *pNumEnd == '+' || *pNumEnd == '-' )
                  pNumEnd++;
               while( *pNumEnd >= '0' && *pNumEnd <= '9' )
                  pNumEnd++;
               if( *pNumEnd == '.' )
               {
                  fHasDot = HB_TRUE;
                  pNumEnd++;
                  while( *pNumEnd >= '0' && *pNumEnd <= '9' )
                     pNumEnd++;
               }

               /* Locate the first non-whitespace after the literal — if it
                  starts a comment, emit it after the statement terminator. */
               pTrail = pNumEnd;
               while( *pTrail == ' ' || *pTrail == '\t' )
                  pTrail++;
               if( ! ( pTrail[ 0 ] == '/' &&
                       ( pTrail[ 1 ] == '/' || pTrail[ 1 ] == '*' ) ) )
                  pTrail = NULL;

               hb_csEmitIndent( yyc, iIndent );
               fprintf( yyc, "const decimal %s = %.*s%s;",
                        szName,
                        ( int )( pNumEnd - pNumStart ), pNumStart,
                        fHasDot ? "m" : "" );
               if( pTrail )
                  fprintf( yyc, " %s\n", pTrail );
               else
                  fprintf( yyc, "\n" );
            }
            else if( hb_stricmp( p, ".T." ) == 0 || hb_stricmp( p, ".F." ) == 0 )
            {
               hb_csEmitIndent( yyc, iIndent );
               fprintf( yyc, "const bool %s = %s;\n", szName,
                        hb_stricmp( p, ".T." ) == 0 ? "true" : "false" );
            }
            else
            {
               /* Complex expression — emit as comment */
               hb_csEmitIndent( yyc, iIndent );
               fprintf( yyc, "// #define %s\n", sz );
            }
         }
         break;

      case HB_AST_CLASSMETHOD:
         /* Method marker node — skip in C# (handled by class emitter) */
         break;

      case HB_AST_BLOCK:
         hb_csEmitBlock( pNode, yyc, iIndent );
         break;

      default:
         hb_csEmitIndent( yyc, iIndent );
         fprintf( yyc, "/* unhandled AST node type %d */\n", pNode->type );
         break;
   }
}

/* Emit all statements in a block */
static void hb_csEmitBlock( PHB_AST_NODE pBlock, FILE * yyc, int iIndent )
{
   PHB_AST_NODE pStmt;

   if( ! pBlock )
      return;

   if( pBlock->type == HB_AST_BLOCK )
   {
      pStmt = pBlock->value.asBlock.pFirst;
      while( pStmt )
      {
         hb_csEmitNode( pStmt, yyc, iIndent );
         pStmt = pStmt->pNext;
      }
   }
   else
      hb_csEmitNode( pBlock, yyc, iIndent );
}

/* ---- Class and method collection structures ---- */

typedef struct _HB_CS_METHOD
{
   PHB_AST_NODE         pFunc;
   PHB_HFUNC            pCompFunc;
   struct _HB_CS_METHOD * pNext;
} HB_CS_METHOD;

typedef struct _HB_CS_CLASS
{
   const char *          szName;
   PHB_AST_NODE          pClassNode;
   HB_CS_METHOD *        pMethods;
   HB_CS_METHOD *        pMethodsLast;
   struct _HB_CS_CLASS * pNext;
} HB_CS_CLASS;

/* Find class entry by name (case-insensitive) */
static HB_CS_CLASS * hb_csFindClass( HB_CS_CLASS * pList, const char * szName )
{
   while( pList )
   {
      if( hb_stricmp( pList->szName, szName ) == 0 )
         return pList;
      pList = pList->pNext;
   }
   return NULL;
}

/* Add a method to a class entry */
static void hb_csAddMethod( HB_CS_CLASS * pClass, PHB_AST_NODE pFunc,
                             PHB_HFUNC pCompFunc )
{
   HB_CS_METHOD * pMethod = ( HB_CS_METHOD * ) hb_xgrab( sizeof( HB_CS_METHOD ) );
   pMethod->pFunc = pFunc;
   pMethod->pCompFunc = pCompFunc;
   pMethod->pNext = NULL;
   if( pClass->pMethodsLast )
   {
      pClass->pMethodsLast->pNext = pMethod;
      pClass->pMethodsLast = pMethod;
   }
   else
      pClass->pMethods = pClass->pMethodsLast = pMethod;
}

/* Free class/method collection */
static void hb_csFreeClasses( HB_CS_CLASS * pList )
{
   while( pList )
   {
      HB_CS_CLASS * pNext = pList->pNext;
      HB_CS_METHOD * pMethod = pList->pMethods;
      while( pMethod )
      {
         HB_CS_METHOD * pMNext = pMethod->pNext;
         hb_xfree( pMethod );
         pMethod = pMNext;
      }
      hb_xfree( pList );
      pList = pNext;
   }
}

/* Emit a C# class method body */
static void hb_csEmitMethodBody( PHB_AST_NODE pFunc, PHB_HFUNC pCompFunc,
                                  FILE * yyc, int iIndent )
{
   PHB_HVAR pVar;
   HB_USHORT nParam = 0;
   const char * szRetType = NULL;
   PHB_AST_NODE pFirstStmt = NULL;
   HB_BOOL fProcedure = HB_FALSE;

   /* Run type propagation */
   if( pFunc->value.asFunc.pBody )
      szRetType = hb_astPropagate( pFunc->value.asFunc.pBody, s_pClassList, s_pRefTab );

   /* Get CLASSMETHOD marker */
   if( pFunc->value.asFunc.pBody &&
       pFunc->value.asFunc.pBody->type == HB_AST_BLOCK )
      pFirstStmt = pFunc->value.asFunc.pBody->value.asBlock.pFirst;

   if( pFirstStmt && pFirstStmt->type == HB_AST_CLASSMETHOD )
      fProcedure = pFirstStmt->value.asClassMethod.fProcedure;

   /* The AST function's szName is mangled as `<Class>__<Method>` by
      hb_compMethodParse so class methods don't collide with same-name
      free functions in the compiler's function table. The emitters
      want the real method name from the CLASSMETHOD marker; fall
      back to the mangled name only if (unexpectedly) no marker is
      present. */
   {
      const char * szMethodName =
         ( pFirstStmt && pFirstStmt->type == HB_AST_CLASSMETHOD &&
           pFirstStmt->value.asClassMethod.szName )
            ? pFirstStmt->value.asClassMethod.szName
            : pFunc->value.asFunc.szName;
      const char * szClass =
         ( pFirstStmt && pFirstStmt->type == HB_AST_CLASSMETHOD )
            ? pFirstStmt->value.asClassMethod.szClass
            : NULL;
      const char * szKey = hb_refTabMethodKey( szClass, szMethodName );
      hb_strncpy( s_szCurrentFunc, szKey, sizeof( s_szCurrentFunc ) - 1 );
   }
   s_pCurrentFuncNode = pFunc;

   /* Emit method signature */
   hb_csEmitIndent( yyc, iIndent );
   fprintf( yyc, "public " );
   if( fProcedure )
   {
      fprintf( yyc, "void" );
      s_fVoidFunc = HB_TRUE;
   }
   else
   {
      if( szRetType )
         fprintf( yyc, "%s", hb_csTypeMap( szRetType ) );
      else
         fprintf( yyc, "dynamic" );
      s_fVoidFunc = HB_FALSE;
   }
   fprintf( yyc, " %s(",
            ( pFirstStmt && pFirstStmt->type == HB_AST_CLASSMETHOD &&
              pFirstStmt->value.asClassMethod.szName )
               ? pFirstStmt->value.asClassMethod.szName
               : pFunc->value.asFunc.szName );

   /* Parameters */
   pVar = pFunc->value.asFunc.pParams;
   {
      /* Method entries in the table are keyed Class::Method, so build
         that key from the CLASSMETHOD marker before any lookups. */
      const char * szClassName =
         ( pFirstStmt && pFirstStmt->type == HB_AST_CLASSMETHOD )
            ? pFirstStmt->value.asClassMethod.szClass
            : NULL;
      char szKeyBuf[ 256 ];
      const char * szRealName =
         ( pFirstStmt && pFirstStmt->type == HB_AST_CLASSMETHOD &&
           pFirstStmt->value.asClassMethod.szName )
            ? pFirstStmt->value.asClassMethod.szName
            : pFunc->value.asFunc.szName;
      const char * szMethName = hb_refTabMethodKey( szClassName, szRealName );
      hb_strncpy( szKeyBuf, szMethName, sizeof( szKeyBuf ) - 1 );
      szMethName = szKeyBuf;

      {
         int iPos = 0;
         int iLastRef = -1;
         int k;

         for( k = 0; k < ( int ) pCompFunc->wParamCount; k++ )
            if( hb_refTabIsRef( s_pRefTab, szMethName, k ) )
               iLastRef = k;

         while( pVar && nParam < pCompFunc->wParamCount )
         {
            HB_BOOL fThisRef     = hb_refTabIsRef( s_pRefTab, szMethName, iPos );
            HB_BOOL fThisNilable = hb_refTabIsNilable( s_pRefTab, szMethName, iPos );
            const HB_REFPARAM * pP =
               hb_refTabParam( s_pRefTab, szMethName, iPos );
            const char * szSlotType = NULL;
            if( pP && pP->szType && hb_stricmp( pP->szType, "USUAL" ) != 0 )
               szSlotType = pP->szType;
            if( ! szSlotType )
               szSlotType = hb_astInferType( pVar->szName, NULL );

            if( nParam > 0 )
               fprintf( yyc, ", " );
            if( fThisRef )
               fprintf( yyc, "ref " );
            fprintf( yyc, "%s%s %s",
                     hb_csTypeMap( szSlotType ),
                     fThisNilable ? "?" : "",
                     pVar->szName );
            if( ! fThisRef && iPos > iLastRef )
               fprintf( yyc, fThisNilable ? " = null" : " = default" );
            nParam++;
            iPos++;
            pVar = pVar->pNext;
         }
      }
   }
   fprintf( yyc, ")\n" );

   /* Method body */
   hb_csEmitIndent( yyc, iIndent );
   fprintf( yyc, "{\n" );
   s_iLastLine = 0;
   if( pFunc->value.asFunc.pBody )
   {
      if( pFirstStmt && pFirstStmt->type == HB_AST_CLASSMETHOD &&
          pFunc->value.asFunc.pBody->type == HB_AST_BLOCK )
      {
         /* Skip CLASSMETHOD marker */
         PHB_AST_NODE pStmt = pFirstStmt->pNext;
         while( pStmt )
         {
            hb_csEmitNode( pStmt, yyc, iIndent + 1 );
            pStmt = pStmt->pNext;
         }
      }
      else
         hb_csEmitBlock( pFunc->value.asFunc.pBody, yyc, iIndent + 1 );
   }
   hb_csEmitIndent( yyc, iIndent );
   fprintf( yyc, "}\n" );
   s_szCurrentFunc[ 0 ] = '\0';
   s_pCurrentFuncNode = NULL;
}

/* Emit a complete C# class definition */
static void hb_csEmitClass( HB_CS_CLASS * pClass, FILE * yyc )
{
   PHB_AST_NODE pClassNode = pClass->pClassNode;
   PHB_AST_NODE pMember;
   HB_CS_METHOD * pMethod;

   fprintf( yyc, "public class %s", pClassNode->value.asClass.szName );
   if( pClassNode->value.asClass.szParent )
      fprintf( yyc, " : %s", pClassNode->value.asClass.szParent );
   fprintf( yyc, "\n{\n" );

   /* Emit DATA members as properties or fields */
   pMember = pClassNode->value.asClass.pMembers;
   while( pMember )
   {
      if( pMember->type == HB_AST_CLASSDATA )
      {
         const char * szType = NULL;
         const char * szScope = hb_csScopeStr( pMember->value.asClassData.iScope );

         if( pMember->value.asClassData.szType )
            szType = pMember->value.asClassData.szType;
         else if( pMember->value.asClassData.iKind != HB_AST_DATA_ACCESS &&
                  pMember->value.asClassData.iKind != HB_AST_DATA_ASSIGN )
            szType = hb_astInferTypeFromInit( pMember->value.asClassData.szName,
                                               pMember->value.asClassData.szInit );

         hb_csEmitIndent( yyc, 1 );

         switch( pMember->value.asClassData.iKind )
         {
            case HB_AST_DATA_CLASS:
               fprintf( yyc, "%s static %s %s",
                        szScope,
                        hb_csTypeMap( szType ),
                        pMember->value.asClassData.szName );
               break;

            case HB_AST_DATA_ACCESS:
               {
                  /* Check if there's a matching ASSIGN for this name */
                  PHB_AST_NODE pScan = pMember->pNext;
                  HB_BOOL fHasAssign = HB_FALSE;
                  while( pScan )
                  {
                     if( pScan->type == HB_AST_CLASSDATA &&
                         pScan->value.asClassData.iKind == HB_AST_DATA_ASSIGN &&
                         hb_stricmp( pScan->value.asClassData.szName,
                                     pMember->value.asClassData.szName ) == 0 )
                     {
                        fHasAssign = HB_TRUE;
                        break;
                     }
                     pScan = pScan->pNext;
                  }
                  fprintf( yyc, "%s %s %s { get;%s }",
                           szScope,
                           hb_csTypeMap( szType ? szType : "USUAL" ),
                           pMember->value.asClassData.szName,
                           fHasAssign ? " set;" : "" );
               }
               break;

            case HB_AST_DATA_ASSIGN:
               {
                  /* Skip if there's a matching ACCESS (already emitted with get;set) */
                  PHB_AST_NODE pScan = pClassNode->value.asClass.pMembers;
                  HB_BOOL fHasAccess = HB_FALSE;
                  while( pScan )
                  {
                     if( pScan->type == HB_AST_CLASSDATA &&
                         pScan->value.asClassData.iKind == HB_AST_DATA_ACCESS &&
                         hb_stricmp( pScan->value.asClassData.szName,
                                     pMember->value.asClassData.szName ) == 0 )
                     {
                        fHasAccess = HB_TRUE;
                        break;
                     }
                     pScan = pScan->pNext;
                  }
                  if( fHasAccess )
                  {
                     pMember = pMember->pNext;
                     continue;
                  }
                  fprintf( yyc, "%s %s %s { get; set; }",
                           szScope,
                           hb_csTypeMap( szType ? szType : "USUAL" ),
                           pMember->value.asClassData.szName );
               }
               break;

            default:
               /* Instance DATA → auto property */
               if( pMember->value.asClassData.fReadOnly )
                  fprintf( yyc, "%s %s %s { get; }",
                           szScope,
                           hb_csTypeMap( szType ),
                           pMember->value.asClassData.szName );
               else
                  fprintf( yyc, "%s %s %s { get; set; }",
                           szScope,
                           hb_csTypeMap( szType ),
                           pMember->value.asClassData.szName );
               break;
         }

         /* INIT value → default */
         if( pMember->value.asClassData.szInit &&
             pMember->value.asClassData.iKind != HB_AST_DATA_ACCESS &&
             pMember->value.asClassData.iKind != HB_AST_DATA_ASSIGN )
            fprintf( yyc, " = %s;",
                     hb_csTranslateInit( pMember->value.asClassData.szInit ) );

         fprintf( yyc, "\n" );
      }
      pMember = pMember->pNext;
   }

   /* Blank line between properties and methods */
   fprintf( yyc, "\n" );

   /* Emit method bodies, skipping ACCESS/ASSIGN implementations */
   pMethod = pClass->pMethods;
   while( pMethod )
   {
      /* The function's szName is mangled `<Class>__<Method>` — read
         the original method name off the CLASSMETHOD marker for the
         ACCESS/ASSIGN collision check. */
      const char * szMethName = pMethod->pFunc->value.asFunc.szName;
      {
         PHB_AST_NODE pFirst =
            ( pMethod->pFunc->value.asFunc.pBody &&
              pMethod->pFunc->value.asFunc.pBody->type == HB_AST_BLOCK )
               ? pMethod->pFunc->value.asFunc.pBody->value.asBlock.pFirst
               : NULL;
         if( pFirst && pFirst->type == HB_AST_CLASSMETHOD &&
             pFirst->value.asClassMethod.szName )
            szMethName = pFirst->value.asClassMethod.szName;
      }
      HB_BOOL fSkipMethod = HB_FALSE;

      /* Check if this method name matches an ACCESS or ASSIGN property */
      pMember = pClassNode->value.asClass.pMembers;
      while( pMember )
      {
         if( pMember->type == HB_AST_CLASSDATA &&
             ( pMember->value.asClassData.iKind == HB_AST_DATA_ACCESS ||
               pMember->value.asClassData.iKind == HB_AST_DATA_ASSIGN ) )
         {
            /* Skip method if name matches ACCESS/ASSIGN property name */
            if( hb_stricmp( szMethName, pMember->value.asClassData.szName ) == 0 )
               fSkipMethod = HB_TRUE;
            /* Skip _Name method (ASSIGN implementation) */
            if( szMethName[ 0 ] == '_' &&
                hb_stricmp( szMethName + 1, pMember->value.asClassData.szName ) == 0 )
               fSkipMethod = HB_TRUE;
         }
         if( fSkipMethod )
            break;
         pMember = pMember->pNext;
      }

      if( ! fSkipMethod )
      {
         hb_csEmitMethodBody( pMethod->pFunc, pMethod->pCompFunc, yyc, 1 );
         if( pMethod->pNext )
            fprintf( yyc, "\n" );
      }
      pMethod = pMethod->pNext;
   }

   fprintf( yyc, "}\n" );
}

/* Emit a standalone function as a static method */
static void hb_csEmitFunc( PHB_AST_NODE pFunc, PHB_HFUNC pCompFunc,
                            FILE * yyc, int iIndent )
{
   PHB_HVAR pVar;
   HB_USHORT nParam = 0;
   const char * szRetType = NULL;
   HB_BOOL fIsMain = HB_FALSE;

   /* Run type propagation */
   if( pFunc->value.asFunc.pBody )
      szRetType = hb_astPropagate( pFunc->value.asFunc.pBody, s_pClassList, s_pRefTab );

   /* Detect Main entry point */
   if( hb_stricmp( pFunc->value.asFunc.szName, "Main" ) == 0 )
      fIsMain = HB_TRUE;

   /* Track current function for nilable-parameter lookups in IF/IIF
      condition emission. Free functions get the bare name. */
   hb_strncpy( s_szCurrentFunc, pFunc->value.asFunc.szName,
               sizeof( s_szCurrentFunc ) - 1 );
   s_pCurrentFuncNode = pFunc;

   /* Emit blank line if gap */
   if( pFunc->iLine > 0 && s_iLastLine > 0 && pFunc->iLine > s_iLastLine + 1 )
      fprintf( yyc, "\n" );
   s_iLastLine = pFunc->iLine;

   hb_csEmitIndent( yyc, iIndent );
   fprintf( yyc, "public static " );

   if( pFunc->value.asFunc.fProcedure || fIsMain )
   {
      fprintf( yyc, "void" );
      s_fVoidFunc = HB_TRUE;
   }
   else
   {
      if( szRetType )
         fprintf( yyc, "%s", hb_csTypeMap( szRetType ) );
      else
         fprintf( yyc, "dynamic" );
      s_fVoidFunc = HB_FALSE;
   }

   fprintf( yyc, " %s(", fIsMain ? "Main" : pFunc->value.asFunc.szName );

   /* Parameters */
   if( fIsMain )
   {
      /* Main gets string[] args if no params declared */
      if( ! pFunc->value.asFunc.pParams || pCompFunc->wParamCount == 0 )
         fprintf( yyc, "string[] args" );
   }

   pVar = pFunc->value.asFunc.pParams;
   {
      /* Main is an entry point — no defaults. For everything else,
         all parameters after the last ref-marked one get `= default`,
         so callers can omit trailing args. Ref params can't carry
         defaults (C# rule), which is why we use "after the last ref"
         as the cutoff for the optional tail. */
      const char * szFnName = fIsMain ? "Main" : pFunc->value.asFunc.szName;
      int iPos = 0;
      int iLastRef = -1;
      HB_BOOL fWantDefaults = ! fIsMain;
      /* A callee reached via `...` spread from a codeblock gets its
         signature widened to `params dynamic[] hbva`. The original
         param names are re-bound from the array inside the body so
         existing body references keep working. Skipped when any slot
         is by-ref (C# `params` can't combine with `ref`) — those calls
         fall back to the reflection path in HbRuntime.EVAL. */
      HB_BOOL fSpread = ! fIsMain &&
                        hb_refTabIsCalledVarargs( s_pRefTab, szFnName );

      if( fWantDefaults )
      {
         int k;
         for( k = 0; k < ( int ) pCompFunc->wParamCount; k++ )
            if( hb_refTabIsRef( s_pRefTab, szFnName, k ) )
               iLastRef = k;
      }

      if( fSpread && iLastRef >= 0 )
         fSpread = HB_FALSE;  /* ref-taking callee: keep typed signature */

      if( fSpread )
      {
         if( fIsMain )
            fprintf( yyc, ", " );
         fprintf( yyc, "params dynamic[] hbva" );
         nParam = pCompFunc->wParamCount;  /* skip normal param loop */
         pVar = NULL;
      }

      while( pVar && nParam < pCompFunc->wParamCount )
      {
         HB_BOOL fThisRef     = hb_refTabIsRef( s_pRefTab, szFnName, iPos );
         HB_BOOL fThisNilable = hb_refTabIsNilable( s_pRefTab, szFnName, iPos );
         /* Prefer the table's per-slot type (which may have been
            refined from call sites in other files); only fall back to
            Hungarian inference if the table has nothing useful. */
         const HB_REFPARAM * pP =
            hb_refTabParam( s_pRefTab, szFnName, iPos );
         const char * szSlotType = NULL;
         if( pP && pP->szType && hb_stricmp( pP->szType, "USUAL" ) != 0 )
            szSlotType = pP->szType;
         if( ! szSlotType )
            szSlotType = hb_astInferType( pVar->szName, NULL );

         if( nParam > 0 || fIsMain )
            fprintf( yyc, ", " );
         if( fThisRef )
            fprintf( yyc, "ref " );
         fprintf( yyc, "%s%s %s",
                  hb_csTypeMap( szSlotType ),
                  fThisNilable ? "?" : "",
                  pVar->szName );
         if( fWantDefaults && ! fThisRef && iPos > iLastRef )
         {
            /* Nilable params default to null (preserves Harbour NIL
               semantics); non-nilable strongly-typed params get the
               value-type zero via `default`. */
            fprintf( yyc, fThisNilable ? " = null" : " = default" );
         }
         nParam++;
         iPos++;
         pVar = pVar->pNext;
      }
   }
   fprintf( yyc, ")\n" );

   hb_csEmitIndent( yyc, iIndent );
   fprintf( yyc, "{\n" );
   s_iLastLine = 0;
   /* If the signature was widened to `params dynamic[] hbva` above,
      re-bind the original named params from the array so references
      in the body keep working. */
   if( ! fIsMain &&
       hb_refTabIsCalledVarargs( s_pRefTab, pFunc->value.asFunc.szName ) )
   {
      PHB_HVAR pSlot;
      int k = 0;
      int iLastRef = -1;
      int j;
      for( j = 0; j < ( int ) pCompFunc->wParamCount; j++ )
         if( hb_refTabIsRef( s_pRefTab, pFunc->value.asFunc.szName, j ) )
            iLastRef = j;
      if( iLastRef < 0 )  /* skip unpack when ref params forced typed sig */
      {
         pSlot = pFunc->value.asFunc.pParams;
         while( pSlot && k < ( int ) pCompFunc->wParamCount )
         {
            hb_csEmitIndent( yyc, iIndent + 1 );
            fprintf( yyc, "dynamic %s = hbva.Length > %d ? hbva[%d] : null;\n",
                     pSlot->szName, k, k );
            pSlot = pSlot->pNext;
            k++;
         }
      }
   }
   if( pFunc->value.asFunc.pBody )
      hb_csEmitBlock( pFunc->value.asFunc.pBody, yyc, iIndent + 1 );
   hb_csEmitIndent( yyc, iIndent );
   fprintf( yyc, "}\n" );
   s_szCurrentFunc[ 0 ] = '\0';
   s_pCurrentFuncNode = NULL;
}

/* ---- Main entry point ---- */

void hb_compGenCSharp( HB_COMP_DECL, PHB_FNAME pFileName )
{
   char szFileName[ HB_PATH_MAX ];
   PHB_AST_NODE pFunc;
   FILE * yyc;
   HB_CS_CLASS * pClassList = NULL;
   HB_CS_CLASS * pClassLast = NULL;
   HB_BOOL fHasStandalone = HB_FALSE;

   HB_SYMBOL_UNUSED( HB_COMP_PARAM );

   /* Build output filename with .cs extension, and capture the base
      name (without path or extension) for use as a STATIC-var name
      prefix. See hb_csIsFileStatic for the collision rationale. */
   hb_csResetFileStatics();
   {
      PHB_FNAME pOut = hb_fsFNameSplit( HB_COMP_PARAM->szFile );
      pFileName->szExtension = ".cs";
      if( HB_COMP_PARAM->pOutPath && HB_COMP_PARAM->pOutPath->szPath )
         pFileName->szPath = HB_COMP_PARAM->pOutPath->szPath;
      else if( pOut->szPath )
         pFileName->szPath = pOut->szPath;
      hb_fsFNameMerge( szFileName, pFileName );
      if( pOut->szName )
         hb_strncpy( s_szFileBase, pOut->szName, sizeof( s_szFileBase ) - 1 );
      hb_xfree( pOut );
   }

   yyc = hb_fopen( szFileName, "w" );
   if( ! yyc )
   {
      hb_compGenError( HB_COMP_PARAM, hb_comp_szErrors, 'E',
                       HB_COMP_ERR_CREATE_OUTPUT, szFileName, NULL );
      return;
   }

   s_iLastLine = 0;

   /* Build the user-function signature table. We first merge in any
      pre-scanned table from disk (the result of `hbtranspiler -GF`
      run over the whole codebase) so that cross-file call-site
      information is visible. Then we scan this file's own AST to
      pick up anything new, and any stale-on-disk entries for
      functions defined in this file get overwritten. */
   s_pRefTab = hb_refTabNew();
   hb_refTabLoad( s_pRefTab, hb_refTabGetPath() );
   hb_refTabCollect( s_pRefTab, HB_COMP_PARAM );

   /* Collect CLASS nodes from startup function */
   s_pClassList = NULL;
   {
      PHB_AST_NODE pFirst = HB_COMP_PARAM->ast.pFuncList;
      if( pFirst && pFirst->type == HB_AST_FUNCTION &&
          pFirst->value.asFunc.pBody &&
          pFirst->value.asFunc.pBody->type == HB_AST_BLOCK )
      {
         PHB_AST_NODE pStmt = pFirst->value.asFunc.pBody->value.asBlock.pFirst;
         s_pClassList = pStmt;
         while( pStmt )
         {
            if( pStmt->type == HB_AST_CLASS )
            {
               HB_CS_CLASS * pClass = ( HB_CS_CLASS * ) hb_xgrab( sizeof( HB_CS_CLASS ) );
               pClass->szName = pStmt->value.asClass.szName;
               pClass->pClassNode = pStmt;
               pClass->pMethods = NULL;
               pClass->pMethodsLast = NULL;
               pClass->pNext = NULL;
               if( pClassLast )
               {
                  pClassLast->pNext = pClass;
                  pClassLast = pClass;
               }
               else
                  pClassList = pClassLast = pClass;
            }
            pStmt = pStmt->pNext;
         }
      }
   }

   /* Walk function list and collect methods into their classes */
   {
      PHB_HFUNC pCompFunc = HB_COMP_PARAM->functions.pFirst;
      pFunc = HB_COMP_PARAM->ast.pFuncList;
      while( pFunc )
      {
         if( pFunc->type == HB_AST_FUNCTION )
         {
            while( pCompFunc && ( pCompFunc->funFlags & HB_FUNF_FILE_DECL ) )
               pCompFunc = pCompFunc->pNext;

            if( pCompFunc && ! ( pCompFunc->funFlags & HB_FUNF_FILE_FIRST ) )
            {
               /* Check if this is a method implementation */
               PHB_AST_NODE pFirstStmt = NULL;
               const char * szClassName = NULL;

               if( pFunc->value.asFunc.pBody &&
                   pFunc->value.asFunc.pBody->type == HB_AST_BLOCK )
                  pFirstStmt = pFunc->value.asFunc.pBody->value.asBlock.pFirst;

               if( pFirstStmt && pFirstStmt->type == HB_AST_CLASSMETHOD &&
                   pFirstStmt->value.asClassMethod.szClass )
                  szClassName = pFirstStmt->value.asClassMethod.szClass;

               if( szClassName )
               {
                  HB_CS_CLASS * pClass = hb_csFindClass( pClassList, szClassName );
                  if( pClass )
                     hb_csAddMethod( pClass, pFunc, pCompFunc );
               }
               else if( pFunc->value.asFunc.pBody &&
                        pFunc->value.asFunc.pBody->value.asBlock.pFirst )
                  fHasStandalone = HB_TRUE;
            }
            if( pCompFunc )
               pCompFunc = pCompFunc->pNext;
         }
         pFunc = pFunc->pNext;
      }
   }

   /* ---- Emit C# output ---- */

   fprintf( yyc, "using System;\n" );
   if( pClassList && pClassList->pNext )
      fprintf( yyc, "using System.Collections.Generic;\n" );
   fprintf( yyc, "\n" );

   /* HbRuntime.cs must be present in the output directory.
      The build script copies it from src/transpiler/HbRuntime.cs */

   /* Emit comments and #include/#define from startup function */
   {
      PHB_AST_NODE pFirst = HB_COMP_PARAM->ast.pFuncList;
      if( pFirst && pFirst->type == HB_AST_FUNCTION &&
          pFirst->value.asFunc.pBody &&
          pFirst->value.asFunc.pBody->type == HB_AST_BLOCK )
      {
         PHB_AST_NODE pStmt = pFirst->value.asFunc.pBody->value.asBlock.pFirst;
         while( pStmt )
         {
            if( pStmt->type == HB_AST_INCLUDE ||
                pStmt->type == HB_AST_COMMENT )
               hb_csEmitNode( pStmt, yyc, 0 );
            pStmt = pStmt->pNext;
         }
      }
   }

   /* Emit class definitions with their methods */
   {
      HB_CS_CLASS * pClass = pClassList;
      while( pClass )
      {
         hb_csEmitClass( pClass, yyc );
         fprintf( yyc, "\n" );
         pClass = pClass->pNext;
      }
   }

   /* Emit standalone functions in a static class */
   if( fHasStandalone )
   {
      PHB_HFUNC pCompFunc = HB_COMP_PARAM->functions.pFirst;

      /* Marked `partial` so multi-file projects (e.g. test19a + test19b)
         can have their separate Program definitions merged into one
         class at the C# build step. Single-file projects are unaffected. */
      fprintf( yyc, "public static partial class Program\n{\n" );
      s_iLastLine = 0;

      /* Emit #define constants as static const members */
      {
         PHB_AST_NODE pFirst = HB_COMP_PARAM->ast.pFuncList;
         if( pFirst && pFirst->type == HB_AST_FUNCTION &&
             pFirst->value.asFunc.pBody &&
             pFirst->value.asFunc.pBody->type == HB_AST_BLOCK )
         {
            PHB_AST_NODE pStmt = pFirst->value.asFunc.pBody->value.asBlock.pFirst;
            while( pStmt )
            {
               if( pStmt->type == HB_AST_PPDEFINE )
                  hb_csEmitNode( pStmt, yyc, 1 );
               pStmt = pStmt->pNext;
            }
         }
      }

      /* Emit STATIC declarations as static class fields. Names are
         mangled with the file base name so two files declaring the
         same STATIC don't collide under the merged `partial class
         Program`. References to these names in function bodies are
         rewritten identically — see hb_csEmitExpr for HB_ET_VARIABLE. */
      {
         PHB_AST_NODE pF = HB_COMP_PARAM->ast.pFuncList;
         while( pF )
         {
            if( pF->type == HB_AST_FUNCTION && pF->value.asFunc.pBody &&
                pF->value.asFunc.pBody->type == HB_AST_BLOCK )
            {
               PHB_AST_NODE pStmt = pF->value.asFunc.pBody->value.asBlock.pFirst;
               while( pStmt )
               {
                  if( pStmt->type == HB_AST_STATIC )
                  {
                     const char * szType = pStmt->value.asVar.szAlias ?
                        pStmt->value.asVar.szAlias :
                        hb_astInferType( pStmt->value.asVar.szName,
                                          pStmt->value.asVar.pInit );
                     hb_csAddFileStatic( pStmt->value.asVar.szName );
                     hb_csEmitIndent( yyc, 1 );
                     fprintf( yyc, "static %s %s_%s",
                              hb_csTypeMap( szType ),
                              s_szFileBase,
                              pStmt->value.asVar.szName );
                     if( pStmt->value.asVar.pInit )
                     {
                        fprintf( yyc, " = " );
                        hb_csEmitExpr( pStmt->value.asVar.pInit, yyc, HB_FALSE );
                     }
                     fprintf( yyc, ";\n" );
                  }
                  pStmt = pStmt->pNext;
               }
            }
            pF = pF->pNext;
         }
      }

      pFunc = HB_COMP_PARAM->ast.pFuncList;
      while( pFunc )
      {
         if( pFunc->type == HB_AST_FUNCTION )
         {
            while( pCompFunc && ( pCompFunc->funFlags & HB_FUNF_FILE_DECL ) )
               pCompFunc = pCompFunc->pNext;

            if( pCompFunc && ! ( pCompFunc->funFlags & HB_FUNF_FILE_FIRST ) )
            {
               /* Check if NOT a method */
               PHB_AST_NODE pFirstStmt = NULL;
               if( pFunc->value.asFunc.pBody &&
                   pFunc->value.asFunc.pBody->type == HB_AST_BLOCK )
                  pFirstStmt = pFunc->value.asFunc.pBody->value.asBlock.pFirst;

               if( ! ( pFirstStmt && pFirstStmt->type == HB_AST_CLASSMETHOD &&
                       pFirstStmt->value.asClassMethod.szClass ) )
               {
                  if( pFunc->value.asFunc.pBody &&
                      pFunc->value.asFunc.pBody->value.asBlock.pFirst )
                  {
                     hb_csEmitFunc( pFunc, pCompFunc, yyc, 1 );
                  }
               }
            }
            if( pCompFunc )
               pCompFunc = pCompFunc->pNext;
         }
         pFunc = pFunc->pNext;
      }

      fprintf( yyc, "}\n" );
   }

   /* Cleanup */
   hb_csFreeClasses( pClassList );
   hb_refTabFree( s_pRefTab );
   s_pRefTab = NULL;
   hb_csResetFileStatics();
   fclose( yyc );

   if( ! HB_COMP_PARAM->fQuiet )
   {
      char buffer[ HB_PATH_MAX + 64 ];
      hb_snprintf( buffer, sizeof( buffer ),
                   "Generating C# output to '%s'... Done.\n", szFileName );
      hb_compOutStd( HB_COMP_PARAM, buffer );
   }
}
