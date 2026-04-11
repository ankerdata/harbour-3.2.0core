/*
 * Harbour Transpiler - Source code emitter
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
#include "hbreftab.h"

/* Forward declarations */
static void hb_astEmitExpr( PHB_EXPR pExpr, FILE * yyc, HB_BOOL fParen );
static void hb_astEmitNode( PHB_AST_NODE pNode, FILE * yyc, int iIndent );
static void hb_astEmitBlock( PHB_AST_NODE pBlock, FILE * yyc, int iIndent );
static void hb_astEmitCallArgs( PHB_EXPR pParms, FILE * yyc );

/* Returns szType if it is a standard Harbour type tag that
   include/astype.ch knows how to strip; returns NULL otherwise.
   Class names (and any other unknown tag) get filtered to NULL so
   the .hb emitter doesn't write `AS Calculator` which stock Harbour
   would reject as a syntax error. See hb_astStandardTypeOrObject
   for the caller-friendly wrapper that degrades class names to the
   generic OBJECT instead of losing the annotation entirely. */
static const char * hb_astStandardType( const char * szType )
{
   if( ! szType )
      return NULL;
   if( hb_stricmp( szType, "INTEGER"   ) == 0 ||
       hb_stricmp( szType, "DECIMAL"   ) == 0 ||
       hb_stricmp( szType, "STRING"    ) == 0 ||
       hb_stricmp( szType, "LOGICAL"   ) == 0 ||
       hb_stricmp( szType, "NUMERIC"   ) == 0 ||
       hb_stricmp( szType, "ARRAY"     ) == 0 ||
       hb_stricmp( szType, "HASH"      ) == 0 ||
       hb_stricmp( szType, "BLOCK"     ) == 0 ||
       hb_stricmp( szType, "OBJECT"    ) == 0 ||
       hb_stricmp( szType, "DATE"      ) == 0 ||
       hb_stricmp( szType, "TIMESTAMP" ) == 0 ||
       hb_stricmp( szType, "USUAL"     ) == 0 )
      return szType;
   return NULL;
}

/* Same as hb_astStandardType but maps non-standard, non-NULL type
   names (which in practice are always class names recorded by the
   type propagator — `Calculator`, `Foo`, etc.) to the generic
   `OBJECT` tag rather than dropping the annotation. The C# emitter
   has its own path that emits the concrete class name; the .hb
   round-trip lossily degrades to OBJECT because astype.ch doesn't
   know about user classes. Without this, `LOCAL oCalc := Foo():New()`
   rounds-tripped as a bare `LOCAL oCalc := Foo():New()` with no
   type annotation, even though the type information was available. */
static const char * hb_astStandardTypeOrObject( const char * szType )
{
   const char * szT = hb_astStandardType( szType );
   if( ! szT && szType )
      return "OBJECT";
   return szT;
}

/* Track last seen source line for blank line preservation */
static int s_iLastLine = 0;
static PHB_AST_NODE s_pClassList = NULL;
static PHB_REFTAB s_pRefTab = NULL;

/* Emit a single blank line if there's a gap in source line numbers,
   indicating the source had one or more blank/comment lines between statements */
static void hb_astEmitBlankLines( FILE * yyc, int iLine )
{
   if( iLine > 0 && s_iLastLine > 0 && iLine > s_iLastLine + 1 )
      fprintf( yyc, "\n" );
   if( iLine > 0 )
      s_iLastLine = iLine;
}

/* Emit indentation */
static void hb_astEmitIndent( FILE * yyc, int iIndent )
{
   int i;
   for( i = 0; i < iIndent; i++ )
      fprintf( yyc, "   " );
}

/* Get operator string for binary/unary operators */
static const char * hb_astOperatorStr( HB_EXPRTYPE type )
{
   switch( type )
   {
      case HB_EO_PLUS:    return " + ";
      case HB_EO_MINUS:   return " - ";
      case HB_EO_MULT:    return " * ";
      case HB_EO_DIV:     return " / ";
      case HB_EO_MOD:     return " % ";
      case HB_EO_POWER:   return " ^ ";
      case HB_EO_ASSIGN:  return " := ";
      case HB_EO_PLUSEQ:  return " += ";
      case HB_EO_MINUSEQ: return " -= ";
      case HB_EO_MULTEQ:  return " *= ";
      case HB_EO_DIVEQ:   return " /= ";
      case HB_EO_MODEQ:   return " %%= ";
      case HB_EO_EXPEQ:   return " ^= ";
      case HB_EO_EQUAL:   return " = ";
      case HB_EO_EQ:      return " == ";
      case HB_EO_NE:      return " != ";
      case HB_EO_LT:      return " < ";
      case HB_EO_GT:      return " > ";
      case HB_EO_LE:      return " <= ";
      case HB_EO_GE:      return " >= ";
      case HB_EO_AND:     return " .AND. ";
      case HB_EO_OR:      return " .OR. ";
      case HB_EO_IN:      return " $ ";
      default:            return " ??? ";
   }
}

/* Emit a function/method call argument list. Each HB_ET_VARREF item
   is prefixed with `@` so the by-ref intent at the call site survives
   the round-trip. (HB_ET_REFERENCE — `@arr[i]` etc. — already gets its
   `@` from the generic expression emitter.) */
static void hb_astEmitCallArgs( PHB_EXPR pParms, FILE * yyc )
{
   PHB_EXPR pItem;
   HB_BOOL  fFirst = HB_TRUE;

   if( ! pParms )
      return;

   if( pParms->ExprType == HB_ET_LIST ||
       pParms->ExprType == HB_ET_ARGLIST ||
       pParms->ExprType == HB_ET_MACROARGLIST )
      pItem = pParms->value.asList.pExprList;
   else
      pItem = pParms;

   while( pItem )
   {
      if( ! fFirst )
         fprintf( yyc, ", " );
      fFirst = HB_FALSE;

      /* HB_ET_NONE is Harbour's empty-slot sentinel, e.g. Fred(x, , z).
         Round-tripping demands we preserve the gap, so we just emit
         nothing and let the comma separators do the work. */
      if( pItem->ExprType == HB_ET_NONE )
      {
         pItem = pItem->pNext;
         continue;
      }

      if( pItem->ExprType == HB_ET_VARREF )
         fprintf( yyc, "@" );
      hb_astEmitExpr( pItem, yyc, HB_FALSE );
      pItem = pItem->pNext;
   }
}

/* Emit a Harbour expression to output */
static void hb_astEmitExpr( PHB_EXPR pExpr, FILE * yyc, HB_BOOL fParen )
{
   if( ! pExpr )
      return;

   switch( pExpr->ExprType )
   {
      case HB_ET_NONE:
         /* empty expression — skip silently */
         break;

      case HB_ET_NIL:
         fprintf( yyc, "NIL" );
         break;

      case HB_ET_NUMERIC:
         if( pExpr->value.asNum.NumType == HB_ET_LONG )
            fprintf( yyc, "%" HB_PFS "d", pExpr->value.asNum.val.l );
         else
            fprintf( yyc, "%.*f", pExpr->value.asNum.bDec,
                     pExpr->value.asNum.val.d );
         break;

      case HB_ET_STRING:
         {
            const char * s = pExpr->value.asString.string;
            HB_SIZE nLen = pExpr->nLength;
            HB_BOOL fHasDQ = HB_FALSE, fHasSQ = HB_FALSE;
            HB_SIZE n;

            for( n = 0; n < nLen; n++ )
            {
               if( s[ n ] == '"' ) fHasDQ = HB_TRUE;
               if( s[ n ] == '\'' ) fHasSQ = HB_TRUE;
            }

            if( ! fHasDQ )
            {
               fputc( '"', yyc );
               fwrite( s, 1, nLen, yyc );
               fputc( '"', yyc );
            }
            else if( ! fHasSQ )
            {
               fputc( '\'', yyc );
               fwrite( s, 1, nLen, yyc );
               fputc( '\'', yyc );
            }
            else
            {
               /* Contains both quote types - use [] brackets */
               fputc( '[', yyc );
               fwrite( s, 1, nLen, yyc );
               fputc( ']', yyc );
            }
         }
         break;

      case HB_ET_LOGICAL:
         fprintf( yyc, "%s", pExpr->value.asLogical ? ".T." : ".F." );
         break;

      case HB_ET_SELF:
         fprintf( yyc, "Self" );
         break;

      case HB_ET_VARIABLE:
      case HB_ET_VARREF:
         fprintf( yyc, "%s", pExpr->value.asSymbol.name );
         break;

      case HB_ET_FUNREF:
         fprintf( yyc, "@%s()", pExpr->value.asSymbol.name );
         break;

      case HB_ET_REFERENCE:
         fprintf( yyc, "@" );
         hb_astEmitExpr( pExpr->value.asReference, yyc, HB_TRUE );
         break;

      case HB_ET_FUNCALL:
         hb_astEmitExpr( pExpr->value.asFunCall.pFunName, yyc, HB_FALSE );
         fprintf( yyc, "(" );
         hb_astEmitCallArgs( pExpr->value.asFunCall.pParms, yyc );
         fprintf( yyc, ")" );
         break;

      case HB_ET_FUNNAME:
         fprintf( yyc, "%s", pExpr->value.asSymbol.name );
         break;

      case HB_ET_SEND:
         if( pExpr->value.asMessage.pObject )
         {
            /* Use :: shorthand for Self:member */
            if( pExpr->value.asMessage.pObject->ExprType == HB_ET_VARIABLE &&
                hb_stricmp( pExpr->value.asMessage.pObject->value.asSymbol.name, "Self" ) == 0 )
               fprintf( yyc, ":" );
            else
               hb_astEmitExpr( pExpr->value.asMessage.pObject, yyc, HB_TRUE );
         }
         if( pExpr->value.asMessage.szMessage )
            fprintf( yyc, ":%s", pExpr->value.asMessage.szMessage );
         else if( pExpr->value.asMessage.pMessage )
         {
            fprintf( yyc, ":" );
            hb_astEmitExpr( pExpr->value.asMessage.pMessage, yyc, HB_FALSE );
         }
         if( pExpr->value.asMessage.pParms )
         {
            fprintf( yyc, "(" );
            hb_astEmitCallArgs( pExpr->value.asMessage.pParms, yyc );
            fprintf( yyc, ")" );
         }
         break;

      case HB_ET_ARRAYAT:
         hb_astEmitExpr( pExpr->value.asList.pExprList, yyc, HB_TRUE );
         fprintf( yyc, "[" );
         hb_astEmitExpr( pExpr->value.asList.pIndex, yyc, HB_FALSE );
         fprintf( yyc, "]" );
         break;

      case HB_ET_ARRAY:
         {
            PHB_EXPR pItem;
            fprintf( yyc, "{" );
            pItem = pExpr->value.asList.pExprList;
            while( pItem )
            {
               hb_astEmitExpr( pItem, yyc, HB_FALSE );
               pItem = pItem->pNext;
               if( pItem )
                  fprintf( yyc, ", " );
            }
            fprintf( yyc, "}" );
         }
         break;

      case HB_ET_HASH:
         {
            PHB_EXPR pItem;
            fprintf( yyc, "{" );
            pItem = pExpr->value.asList.pExprList;
            while( pItem )
            {
               hb_astEmitExpr( pItem, yyc, HB_FALSE );
               pItem = pItem->pNext;
               if( pItem )
               {
                  fprintf( yyc, " => " );
                  hb_astEmitExpr( pItem, yyc, HB_FALSE );
                  pItem = pItem->pNext;
                  if( pItem )
                     fprintf( yyc, ", " );
               }
            }
            fprintf( yyc, "}" );
         }
         break;

      case HB_ET_IIF:
         fprintf( yyc, "IIF(" );
         /* IIF has 3 children in the list */
         if( pExpr->value.asList.pExprList )
         {
            PHB_EXPR pCond = pExpr->value.asList.pExprList;
            hb_astEmitExpr( pCond, yyc, HB_FALSE );
            if( pCond->pNext )
            {
               fprintf( yyc, ", " );
               hb_astEmitExpr( pCond->pNext, yyc, HB_FALSE );
               if( pCond->pNext->pNext )
               {
                  fprintf( yyc, ", " );
                  hb_astEmitExpr( pCond->pNext->pNext, yyc, HB_FALSE );
               }
            }
         }
         fprintf( yyc, ")" );
         break;

      case HB_ET_LIST:
      case HB_ET_ARGLIST:
      case HB_ET_MACROARGLIST:
         {
            PHB_EXPR pItem = pExpr->value.asList.pExprList;
            while( pItem )
            {
               hb_astEmitExpr( pItem, yyc, HB_FALSE );
               pItem = pItem->pNext;
               if( pItem )
                  fprintf( yyc, ", " );
            }
         }
         break;

      case HB_ET_MACRO:
         if( pExpr->value.asMacro.pExprList )
         {
            fprintf( yyc, "&(" );
            hb_astEmitExpr( pExpr->value.asMacro.pExprList, yyc, HB_FALSE );
            fprintf( yyc, ")" );
         }
         else if( pExpr->value.asMacro.szMacro )
         {
            fprintf( yyc, "&%s", pExpr->value.asMacro.szMacro );
            if( pExpr->value.asMacro.cMacroOp )
               fprintf( yyc, "%c", pExpr->value.asMacro.cMacroOp );
         }
         break;

      case HB_ET_ALIASVAR:
         /* When the alias side is an HB_ET_ALIAS keyword node with no
            name (the implicit-memvar wrap the parser inserts for
            unresolved identifiers), emit just the bare var — re-parsing
            will re-wrap it the same way, so the round-trip is
            semantically equivalent and syntactically valid. Named
            HB_ET_ALIAS (`FIELD->x`, `MEMVAR->x`) and workarea aliases
            (`cust->name`) fall through to the normal `alias->var`
            emission via the HB_ET_ALIAS / HB_ET_VARIABLE handlers. */
         if( pExpr->value.asAlias.pAlias &&
             pExpr->value.asAlias.pAlias->ExprType == HB_ET_ALIAS &&
             ! pExpr->value.asAlias.pAlias->value.asSymbol.name )
         {
            hb_astEmitExpr( pExpr->value.asAlias.pVar, yyc, HB_FALSE );
         }
         else
         {
            hb_astEmitExpr( pExpr->value.asAlias.pAlias, yyc, HB_FALSE );
            fprintf( yyc, "->" );
            hb_astEmitExpr( pExpr->value.asAlias.pVar, yyc, HB_FALSE );
         }
         break;

      case HB_ET_ALIASEXPR:
         hb_astEmitExpr( pExpr->value.asAlias.pAlias, yyc, HB_FALSE );
         fprintf( yyc, "->(" );
         hb_astEmitExpr( pExpr->value.asAlias.pExpList, yyc, HB_FALSE );
         fprintf( yyc, ")" );
         break;

      case HB_ET_ALIAS:
         /* Bare alias keyword (FIELD, MEMVAR, or an implicit wrapper
            with NULL name). When named, emit the keyword so the enclosing
            HB_ET_ALIASVAR produces valid `FIELD->x` / `MEMVAR->x`.
            The NULL case is handled one level up in HB_ET_ALIASVAR and
            shouldn't reach here, but emit a safe placeholder if it does
            instead of falling through to "unknown expr type 26". */
         if( pExpr->value.asSymbol.name )
            fprintf( yyc, "%s", pExpr->value.asSymbol.name );
         break;

      case HB_ET_CODEBLOCK:
         {
            PHB_CBVAR pVar;
            fprintf( yyc, "{|" );
            pVar = pExpr->value.asCodeblock.pLocals;
            while( pVar )
            {
               fprintf( yyc, "%s", pVar->szName );
               pVar = pVar->pNext;
               if( pVar )
                  fprintf( yyc, ", " );
            }
            fprintf( yyc, "| " );
            if( pExpr->value.asCodeblock.pExprList )
               hb_astEmitExpr( pExpr->value.asCodeblock.pExprList, yyc, HB_FALSE );
            fprintf( yyc, "}" );
         }
         break;

      case HB_ET_DATE:
         fprintf( yyc, "0d%08lx", pExpr->value.asDate.lDate );
         break;

      case HB_ET_TIMESTAMP:
         fprintf( yyc, "t\"%ld,%ld\"", pExpr->value.asDate.lDate,
                  pExpr->value.asDate.lTime );
         break;

      case HB_ET_RTVAR:
         /* PRIVATE/PUBLIC variable reference */
         if( pExpr->value.asRTVar.szName )
            fprintf( yyc, "%s", pExpr->value.asRTVar.szName );
         else if( pExpr->value.asRTVar.pMacro )
            hb_astEmitExpr( pExpr->value.asRTVar.pMacro, yyc, HB_FALSE );
         break;

      case HB_ET_SETGET:
         /* HB_ET_SETGET is used for PARAMETER defaults: IIF(var==NIL, expr, expr:=var) */
         hb_astEmitExpr( pExpr->value.asSetGet.pVar, yyc, HB_FALSE );
         break;

      case HB_EO_NEGATE:
         fprintf( yyc, "-" );
         hb_astEmitExpr( pExpr->value.asOperator.pLeft, yyc, HB_TRUE );
         break;

      case HB_EO_NOT:
         fprintf( yyc, "!" );
         hb_astEmitExpr( pExpr->value.asOperator.pLeft, yyc, HB_TRUE );
         break;

      case HB_EO_PREINC:
         fprintf( yyc, "++" );
         hb_astEmitExpr( pExpr->value.asOperator.pLeft, yyc, HB_TRUE );
         break;

      case HB_EO_PREDEC:
         fprintf( yyc, "--" );
         hb_astEmitExpr( pExpr->value.asOperator.pLeft, yyc, HB_TRUE );
         break;

      case HB_EO_POSTINC:
         hb_astEmitExpr( pExpr->value.asOperator.pLeft, yyc, HB_TRUE );
         fprintf( yyc, "++" );
         break;

      case HB_EO_POSTDEC:
         hb_astEmitExpr( pExpr->value.asOperator.pLeft, yyc, HB_TRUE );
         fprintf( yyc, "--" );
         break;

      default:
         /* Binary operators */
         if( pExpr->ExprType >= HB_EO_ASSIGN && pExpr->ExprType <= HB_EO_PREDEC )
         {
            /* Only parenthesise if this operator has lower precedence than parent.
               Higher ExprType value = higher precedence in Harbour's enum ordering.
               fParen is TRUE when we're a child of another operator. */
            HB_BOOL fNeedParen = HB_FALSE;
            if( fParen )
            {
               /* Check if left child is a lower-precedence operator */
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
            hb_astEmitExpr( pExpr->value.asOperator.pLeft, yyc, HB_TRUE );
            fprintf( yyc, "%s", hb_astOperatorStr( pExpr->ExprType ) );
            hb_astEmitExpr( pExpr->value.asOperator.pRight, yyc, HB_TRUE );
            if( fNeedParen )
               fprintf( yyc, ")" );
         }
         else
         {
            fprintf( yyc, "/* unknown expr type %d */", pExpr->ExprType );
         }
         break;
   }
}

/* Emit a single AST statement node */
static void hb_astEmitNode( PHB_AST_NODE pNode, FILE * yyc, int iIndent )
{
   if( ! pNode )
      return;

   /* Emit blank line separator for leaf statements when source had a gap.
      For compound structures (IF, FOR, etc.), reset tracking since
      they span many lines and we can't know the closing line number. */
   switch( pNode->type )
   {
      case HB_AST_EXPRSTMT:
      case HB_AST_LOCAL:
      case HB_AST_STATIC:
      case HB_AST_PUBLIC:
      case HB_AST_PRIVATE:
      case HB_AST_MEMVAR:
      case HB_AST_RETURN:
      case HB_AST_EXIT:
      case HB_AST_LOOP:
      case HB_AST_BREAK:
      case HB_AST_INCLUDE:
      case HB_AST_PPDEFINE:
      case HB_AST_COMMENT:
         hb_astEmitBlankLines( yyc, pNode->iLine );
         break;
      default:
         /* Compound structure — emit blank line if gap, then reset */
         if( pNode->iLine > 0 && s_iLastLine > 0 && pNode->iLine > s_iLastLine + 1 )
            fprintf( yyc, "\n" );
         s_iLastLine = 0;
         break;
   }

   switch( pNode->type )
   {
      case HB_AST_EXPRSTMT:
         hb_astEmitIndent( yyc, iIndent );
         hb_astEmitExpr( pNode->value.asExprStmt.pExpr, yyc, HB_FALSE );
         fprintf( yyc, "\n" );
         break;

      case HB_AST_RETURN:
         hb_astEmitIndent( yyc, iIndent > 0 ? iIndent - 1 : 0 );
         fprintf( yyc, "RETURN" );
         if( pNode->value.asReturn.pExpr )
         {
            fprintf( yyc, " " );
            hb_astEmitExpr( pNode->value.asReturn.pExpr, yyc, HB_FALSE );
         }
         fprintf( yyc, "\n" );
         break;

      case HB_AST_QOUT:
         hb_astEmitIndent( yyc, iIndent );
         fprintf( yyc, "? " );
         if( pNode->value.asQOut.pExprList )
            hb_astEmitExpr( pNode->value.asQOut.pExprList, yyc, HB_FALSE );
         fprintf( yyc, "\n" );
         break;

      case HB_AST_QQOUT:
         hb_astEmitIndent( yyc, iIndent );
         fprintf( yyc, "?? " );
         if( pNode->value.asQOut.pExprList )
            hb_astEmitExpr( pNode->value.asQOut.pExprList, yyc, HB_FALSE );
         fprintf( yyc, "\n" );
         break;

      case HB_AST_CLASSMETHOD:
         /* Method/Procedure implementation header (after ENDCLASS) */
         hb_astEmitIndent( yyc, iIndent );
         fprintf( yyc, "%s %s()",
                  pNode->value.asClassMethod.fProcedure ? "PROCEDURE" : "METHOD",
                  pNode->value.asClassMethod.szName );
         if( pNode->value.asClassMethod.szClass )
            fprintf( yyc, " CLASS %s", pNode->value.asClassMethod.szClass );
         fprintf( yyc, "\n" );
         break;

      case HB_AST_LOCAL:
         hb_astEmitIndent( yyc, iIndent );
         fprintf( yyc, "LOCAL %s", pNode->value.asVar.szName );
         if( pNode->value.asVar.pInit )
         {
            fprintf( yyc, " := " );
            hb_astEmitExpr( pNode->value.asVar.pInit, yyc, HB_FALSE );
         }
         /* Use propagated type if available, otherwise infer. Class
            names degrade to the generic OBJECT (astype.ch doesn't know
            user class names, but it does know OBJECT). */
         {
            const char * szT =
               pNode->value.asVar.szAlias ? pNode->value.asVar.szAlias
                                          : hb_astInferType( pNode->value.asVar.szName,
                                                             pNode->value.asVar.pInit );
            szT = hb_astStandardTypeOrObject( szT );
            if( szT )
               fprintf( yyc, " AS %s", szT );
         }
         fprintf( yyc, "\n" );
         break;

      case HB_AST_MEMVAR:
         hb_astEmitIndent( yyc, iIndent );
         fprintf( yyc, "MEMVAR %s\n", pNode->value.asVar.szName );
         break;

      case HB_AST_STATIC:
      case HB_AST_PUBLIC:
      case HB_AST_PRIVATE:
         hb_astEmitIndent( yyc, iIndent );
         fprintf( yyc, "%s %s",
                  pNode->type == HB_AST_STATIC ? "STATIC" :
                  pNode->type == HB_AST_PUBLIC ? "PUBLIC" : "PRIVATE",
                  pNode->value.asVar.szName );
         if( pNode->value.asVar.pInit )
         {
            fprintf( yyc, " := " );
            hb_astEmitExpr( pNode->value.asVar.pInit, yyc, HB_FALSE );
         }
         {
            const char * szT =
               pNode->value.asVar.szAlias ? pNode->value.asVar.szAlias
                                          : hb_astInferType( pNode->value.asVar.szName,
                                                             pNode->value.asVar.pInit );
            szT = hb_astStandardTypeOrObject( szT );
            if( szT )
               fprintf( yyc, " AS %s", szT );
         }
         fprintf( yyc, "\n" );
         break;

      case HB_AST_IF:
         hb_astEmitIndent( yyc, iIndent );
         fprintf( yyc, "IF " );
         hb_astEmitExpr( pNode->value.asIf.pCondition, yyc, HB_FALSE );
         fprintf( yyc, "\n" );
         if( pNode->value.asIf.pThen )
            hb_astEmitBlock( pNode->value.asIf.pThen, yyc, iIndent + 1 );
         /* ELSEIF chain */
         {
            PHB_AST_NODE pElseIf = pNode->value.asIf.pElseIfs;
            while( pElseIf )
            {
               hb_astEmitIndent( yyc, iIndent );
               fprintf( yyc, "ELSEIF " );
               hb_astEmitExpr( pElseIf->value.asElseIf.pCondition, yyc, HB_FALSE );
               fprintf( yyc, "\n" );
               s_iLastLine = 0;
               if( pElseIf->value.asElseIf.pBody )
                  hb_astEmitBlock( pElseIf->value.asElseIf.pBody, yyc, iIndent + 1 );
               pElseIf = pElseIf->pNext;
            }
         }
         if( pNode->value.asIf.pElse )
         {
            hb_astEmitIndent( yyc, iIndent );
            fprintf( yyc, "ELSE\n" );
            s_iLastLine = 0;
            hb_astEmitBlock( pNode->value.asIf.pElse, yyc, iIndent + 1 );
         }
         hb_astEmitIndent( yyc, iIndent );
         fprintf( yyc, "ENDIF\n" );
         break;

      case HB_AST_DOWHILE:
         hb_astEmitIndent( yyc, iIndent );
         fprintf( yyc, "DO WHILE " );
         hb_astEmitExpr( pNode->value.asWhile.pCondition, yyc, HB_FALSE );
         fprintf( yyc, "\n" );
         if( pNode->value.asWhile.pBody )
            hb_astEmitBlock( pNode->value.asWhile.pBody, yyc, iIndent + 1 );
         hb_astEmitIndent( yyc, iIndent );
         fprintf( yyc, "ENDDO\n" );
         break;

      case HB_AST_FOR:
         hb_astEmitIndent( yyc, iIndent );
         fprintf( yyc, "FOR %s := ", pNode->value.asFor.szVar );
         hb_astEmitExpr( pNode->value.asFor.pStart, yyc, HB_FALSE );
         fprintf( yyc, " TO " );
         hb_astEmitExpr( pNode->value.asFor.pEnd, yyc, HB_FALSE );
         if( pNode->value.asFor.pStep )
         {
            fprintf( yyc, " STEP " );
            hb_astEmitExpr( pNode->value.asFor.pStep, yyc, HB_FALSE );
         }
         fprintf( yyc, "\n" );
         if( pNode->value.asFor.pBody )
            hb_astEmitBlock( pNode->value.asFor.pBody, yyc, iIndent + 1 );
         hb_astEmitIndent( yyc, iIndent );
         fprintf( yyc, "NEXT\n" );
         break;

      case HB_AST_EXIT:
         hb_astEmitIndent( yyc, iIndent );
         fprintf( yyc, "EXIT\n" );
         break;

      case HB_AST_LOOP:
         hb_astEmitIndent( yyc, iIndent );
         fprintf( yyc, "LOOP\n" );
         break;

      case HB_AST_BREAK:
         hb_astEmitIndent( yyc, iIndent );
         fprintf( yyc, "BREAK" );
         if( pNode->value.asBreak.pExpr )
         {
            fprintf( yyc, " " );
            hb_astEmitExpr( pNode->value.asBreak.pExpr, yyc, HB_FALSE );
         }
         fprintf( yyc, "\n" );
         break;

      case HB_AST_DOCASE:
         hb_astEmitIndent( yyc, iIndent );
         fprintf( yyc, "DO CASE\n" );
         {
            PHB_AST_NODE pCase = pNode->value.asDoCase.pCases;
            while( pCase )
            {
               hb_astEmitIndent( yyc, iIndent + 1 );
               fprintf( yyc, "CASE " );
               hb_astEmitExpr( pCase->value.asCase.pCondition, yyc, HB_FALSE );
               fprintf( yyc, "\n" );
               s_iLastLine = 0;
               if( pCase->value.asCase.pBody )
                  hb_astEmitBlock( pCase->value.asCase.pBody, yyc, iIndent + 2 );
               pCase = pCase->pNext;
            }
         }
         if( pNode->value.asDoCase.pOtherwise )
         {
            hb_astEmitIndent( yyc, iIndent + 1 );
            fprintf( yyc, "OTHERWISE\n" );
            s_iLastLine = 0;
            hb_astEmitBlock( pNode->value.asDoCase.pOtherwise, yyc, iIndent + 2 );
         }
         hb_astEmitIndent( yyc, iIndent );
         fprintf( yyc, "ENDCASE\n" );
         break;

      case HB_AST_SWITCH:
         hb_astEmitIndent( yyc, iIndent );
         fprintf( yyc, "SWITCH " );
         hb_astEmitExpr( pNode->value.asSwitch.pSwitch, yyc, HB_FALSE );
         fprintf( yyc, "\n" );
         {
            PHB_AST_NODE pCase = pNode->value.asSwitch.pCases;
            while( pCase )
            {
               if( pCase->value.asCase.pCondition )
               {
                  hb_astEmitIndent( yyc, iIndent + 1 );
                  fprintf( yyc, "CASE " );
                  hb_astEmitExpr( pCase->value.asCase.pCondition, yyc, HB_FALSE );
                  fprintf( yyc, "\n" );
               }
               s_iLastLine = 0;
               if( pCase->value.asCase.pBody )
                  hb_astEmitBlock( pCase->value.asCase.pBody, yyc, iIndent + 2 );
               pCase = pCase->pNext;
            }
         }
         if( pNode->value.asSwitch.pDefault )
         {
            hb_astEmitIndent( yyc, iIndent + 1 );
            fprintf( yyc, "OTHERWISE\n" );
            s_iLastLine = 0;
            hb_astEmitBlock( pNode->value.asSwitch.pDefault, yyc, iIndent + 2 );
         }
         hb_astEmitIndent( yyc, iIndent );
         fprintf( yyc, "ENDSWITCH\n" );
         break;

      case HB_AST_BEGINSEQ:
         hb_astEmitIndent( yyc, iIndent );
         fprintf( yyc, "BEGIN SEQUENCE\n" );
         s_iLastLine = 0;
         if( pNode->value.asSeq.pBody )
            hb_astEmitBlock( pNode->value.asSeq.pBody, yyc, iIndent + 1 );
         if( pNode->value.asSeq.pRecover )
         {
            hb_astEmitIndent( yyc, iIndent );
            if( pNode->value.asSeq.szRecoverVar )
               fprintf( yyc, "RECOVER USING %s\n", pNode->value.asSeq.szRecoverVar );
            else
               fprintf( yyc, "RECOVER\n" );
            s_iLastLine = 0;
            hb_astEmitBlock( pNode->value.asSeq.pRecover, yyc, iIndent + 1 );
         }
         if( pNode->value.asSeq.pAlways )
         {
            hb_astEmitIndent( yyc, iIndent );
            fprintf( yyc, "ALWAYS\n" );
            s_iLastLine = 0;
            hb_astEmitBlock( pNode->value.asSeq.pAlways, yyc, iIndent + 1 );
         }
         hb_astEmitIndent( yyc, iIndent );
         fprintf( yyc, "END SEQUENCE\n" );
         break;

      case HB_AST_FOREACH:
         hb_astEmitIndent( yyc, iIndent );
         fprintf( yyc, "FOR EACH " );
         hb_astEmitExpr( pNode->value.asForEach.pVar, yyc, HB_FALSE );
         fprintf( yyc, " IN " );
         hb_astEmitExpr( pNode->value.asForEach.pEnum, yyc, HB_FALSE );
         if( pNode->value.asForEach.iDir < 0 )
            fprintf( yyc, " DESCEND" );
         fprintf( yyc, "\n" );
         if( pNode->value.asForEach.pBody )
            hb_astEmitBlock( pNode->value.asForEach.pBody, yyc, iIndent + 1 );
         hb_astEmitIndent( yyc, iIndent );
         fprintf( yyc, "NEXT\n" );
         break;

      case HB_AST_WITHOBJECT:
         hb_astEmitIndent( yyc, iIndent );
         fprintf( yyc, "WITH OBJECT " );
         hb_astEmitExpr( pNode->value.asWithObj.pObject, yyc, HB_FALSE );
         fprintf( yyc, "\n" );
         s_iLastLine = 0;
         if( pNode->value.asWithObj.pBody )
            hb_astEmitBlock( pNode->value.asWithObj.pBody, yyc, iIndent + 1 );
         hb_astEmitIndent( yyc, iIndent );
         fprintf( yyc, "END WITH\n" );
         break;

      case HB_AST_CLASS:
         hb_astEmitIndent( yyc, iIndent );
         fprintf( yyc, "CLASS %s", pNode->value.asClass.szName );
         if( pNode->value.asClass.szParent )
            fprintf( yyc, " INHERIT %s", pNode->value.asClass.szParent );
         fprintf( yyc, "\n\n" );
         {
            PHB_AST_NODE pMember = pNode->value.asClass.pMembers;
            int iPrevScope = -1;
            int iLastMemberLine = pNode->iLine;

            while( pMember )
            {
               /* Emit blank line when source had a gap between members */
               if( pMember->iLine > 0 && iLastMemberLine > 0 &&
                   pMember->iLine > iLastMemberLine + 1 )
                  fprintf( yyc, "\n" );
               iLastMemberLine = pMember->iLine;

               if( pMember->type == HB_AST_CLASSDATA )
               {
                  /* Emit scope section header when scope changes */
                  if( pMember->value.asClassData.iScope != iPrevScope )
                  {
                     iPrevScope = pMember->value.asClassData.iScope;
                     if( iPrevScope == HB_AST_SCOPE_PROTECTED )
                     {
                        hb_astEmitIndent( yyc, iIndent + 1 );
                        fprintf( yyc, "PROTECTED:\n" );
                     }
                     else if( iPrevScope == HB_AST_SCOPE_HIDDEN )
                     {
                        hb_astEmitIndent( yyc, iIndent + 1 );
                        fprintf( yyc, "HIDDEN:\n" );
                     }
                     else if( iPrevScope == HB_AST_SCOPE_EXPORTED && pMember != pNode->value.asClass.pMembers )
                     {
                        hb_astEmitIndent( yyc, iIndent + 1 );
                        fprintf( yyc, "EXPORTED:\n" );
                     }
                  }

                  hb_astEmitIndent( yyc, iIndent + 1 );
                  switch( pMember->value.asClassData.iKind )
                  {
                     case HB_AST_DATA_CLASS:   fprintf( yyc, "CLASSDATA " ); break;
                     case HB_AST_DATA_ACCESS:  fprintf( yyc, "ACCESS " ); break;
                     case HB_AST_DATA_ASSIGN:  fprintf( yyc, "ASSIGN " ); break;
                     default:                  fprintf( yyc, "DATA " ); break;
                  }
                  fprintf( yyc, "%s", pMember->value.asClassData.szName );
                  if( pMember->value.asClassData.szType )
                     fprintf( yyc, " AS %s", pMember->value.asClassData.szType );
                  else if( pMember->value.asClassData.iKind != HB_AST_DATA_ACCESS &&
                           pMember->value.asClassData.iKind != HB_AST_DATA_ASSIGN )
                  {
                     /* Infer type from INIT value and/or Hungarian prefix */
                     const char * szInferred = hb_astInferTypeFromInit(
                        pMember->value.asClassData.szName,
                        pMember->value.asClassData.szInit );
                     if( szInferred )
                        fprintf( yyc, " AS %s", szInferred );
                  }
                  if( pMember->value.asClassData.szInit )
                     fprintf( yyc, " INIT %s", pMember->value.asClassData.szInit );
                  if( pMember->value.asClassData.fReadOnly )
                     fprintf( yyc, " READONLY" );
                  fprintf( yyc, "\n" );
               }
               else if( pMember->type == HB_AST_CLASSMETHOD )
               {
                  /* Emit scope section header when scope changes */
                  if( pMember->value.asClassMethod.iScope != iPrevScope )
                  {
                     iPrevScope = pMember->value.asClassMethod.iScope;
                     if( iPrevScope == HB_AST_SCOPE_PROTECTED )
                     {
                        hb_astEmitIndent( yyc, iIndent + 1 );
                        fprintf( yyc, "PROTECTED:\n" );
                     }
                     else if( iPrevScope == HB_AST_SCOPE_HIDDEN )
                     {
                        hb_astEmitIndent( yyc, iIndent + 1 );
                        fprintf( yyc, "HIDDEN:\n" );
                     }
                     else if( iPrevScope == HB_AST_SCOPE_EXPORTED && pMember != pNode->value.asClass.pMembers )
                     {
                        hb_astEmitIndent( yyc, iIndent + 1 );
                        fprintf( yyc, "EXPORTED:\n" );
                     }
                  }

                  hb_astEmitIndent( yyc, iIndent + 1 );
                  if( pMember->value.asClassMethod.szParams )
                     fprintf( yyc, "%s %s( %s )\n",
                              pMember->value.asClassMethod.fProcedure ? "PROCEDURE" : "METHOD",
                              pMember->value.asClassMethod.szName,
                              pMember->value.asClassMethod.szParams );
                  else
                     fprintf( yyc, "%s %s()\n",
                              pMember->value.asClassMethod.fProcedure ? "PROCEDURE" : "METHOD",
                              pMember->value.asClassMethod.szName );
               }
               pMember = pMember->pNext;
            }
         }
         fprintf( yyc, "\n" );
         hb_astEmitIndent( yyc, iIndent );
         fprintf( yyc, "ENDCLASS\n\n" );
         s_iLastLine = 0;
         break;

      case HB_AST_COMMENT:
         hb_astEmitIndent( yyc, iIndent );
         fprintf( yyc, "%s\n", pNode->value.asComment.szText );
         break;

      case HB_AST_INCLUDE:
         hb_astEmitIndent( yyc, iIndent );
         fprintf( yyc, "#include \"%s\"\n", pNode->value.asInclude.szFile );
         break;

      case HB_AST_PPDEFINE:
         hb_astEmitIndent( yyc, iIndent );
         fprintf( yyc, "#define %s\n", pNode->value.asDefine.szDefine );
         break;

      case HB_AST_BLOCK:
         hb_astEmitBlock( pNode, yyc, iIndent );
         break;

      default:
         hb_astEmitIndent( yyc, iIndent );
         fprintf( yyc, "/* unhandled AST node type %d */\n", pNode->type );
         break;
   }
}

/* Emit all statements in a block */
static void hb_astEmitBlock( PHB_AST_NODE pBlock, FILE * yyc, int iIndent )
{
   PHB_AST_NODE pStmt;

   if( ! pBlock )
      return;

   if( pBlock->type == HB_AST_BLOCK )
   {
      pStmt = pBlock->value.asBlock.pFirst;
      while( pStmt )
      {
         hb_astEmitNode( pStmt, yyc, iIndent );
         pStmt = pStmt->pNext;
      }
   }
   else
   {
      /* Single statement, not wrapped in a block */
      hb_astEmitNode( pBlock, yyc, iIndent );
   }
}

/* Emit a function definition */
static void hb_astEmitFunc( PHB_AST_NODE pFunc, PHB_HFUNC pCompFunc, FILE * yyc )
{
   PHB_HVAR pVar;
   HB_USHORT nParam = 0;
   const char * szRetType = NULL;

   /* Run type propagation before emitting */
   if( pFunc->value.asFunc.pBody )
      szRetType = hb_astPropagate( pFunc->value.asFunc.pBody, s_pClassList, s_pRefTab );

   /* Emit blank line if there's a gap from previous output */
   if( pFunc->iLine > 0 && s_iLastLine > 0 && pFunc->iLine > s_iLastLine + 1 )
      fprintf( yyc, "\n" );
   s_iLastLine = pFunc->iLine;

   /* Check if body starts with a CLASSMETHOD node — if so, this is a
      METHOD implementation after ENDCLASS */
   {
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
         /* Emit as METHOD/PROCEDURE ... CLASS ... */
         fprintf( yyc, "%s %s(",
                  ( pFirstStmt->value.asClassMethod.fProcedure ? "PROCEDURE" : "METHOD" ),
                  pFunc->value.asFunc.szName );
      }
      else if( pFunc->value.asFunc.fProcedure )
         fprintf( yyc, "PROCEDURE %s(", pFunc->value.asFunc.szName );
      else
         fprintf( yyc, "FUNCTION %s(", pFunc->value.asFunc.szName );

      /* Emit parameters with inferred types. Harbour's grammar has no
         syntax for declaring a by-ref parameter in a FUNCTION/PROCEDURE
         signature (by-ref is purely a call-site notion in the language),
         so we tag by-ref slots with the user's existing block-comment
         marker placed immediately before the parameter name — Harbour
         harmlessly ignores it as a comment, and downstream tools can
         scan for the literal sequence. */
      pVar = pFunc->value.asFunc.pParams;
      {
         /* Methods are keyed Class::Method in the table; use the
            method-key helper to consult the right entry. */
         char szKeyBuf[ 256 ];
         const char * szFnName = hb_refTabMethodKey(
            szClassName, pFunc->value.asFunc.szName );
         hb_strncpy( szKeyBuf, szFnName, sizeof( szKeyBuf ) - 1 );
         szFnName = szKeyBuf;
         {
            int iPos = 0;
            while( pVar && nParam < pCompFunc->wParamCount )
            {
               HB_BOOL fByRef = hb_refTabIsRef( s_pRefTab, szFnName, iPos );
               /* Prefer the table's refined type (which may reflect
                  cross-file call-site analysis) over bare Hungarian
                  inference — keeps the .hb annotations in lockstep with
                  the -GS output. */
               const HB_REFPARAM * pP =
                  hb_refTabParam( s_pRefTab, szFnName, iPos );
               const char * szSlotType = NULL;
               if( pP && pP->szType && hb_stricmp( pP->szType, "USUAL" ) != 0 )
                  szSlotType = pP->szType;
               if( ! szSlotType )
                  szSlotType = hb_astInferType( pVar->szName, NULL );

               if( nParam > 0 )
                  fprintf( yyc, ", " );
               else
                  fprintf( yyc, " " );
               {
                  /* Class-typed parameters degrade to the generic
                     OBJECT tag in .hb output (astype.ch doesn't know
                     about user class names); the .cs path keeps the
                     concrete type via its own emitter. */
                  const char * szT = hb_astStandardTypeOrObject( szSlotType );
                  if( szT )
                     fprintf( yyc, "%s%s AS %s",
                              fByRef ? "/*@*/" : "",
                              pVar->szName,
                              szT );
                  else
                     fprintf( yyc, "%s%s",
                              fByRef ? "/*@*/" : "",
                              pVar->szName );
               }
               nParam++;
               iPos++;
               pVar = pVar->pNext;
            }
         }
      }
      if( nParam > 0 )
         fprintf( yyc, " " );
      fprintf( yyc, ")" );

      /* Emit return type for FUNCTIONs and METHODs (not PROCEDUREs).
         Class-typed returns degrade to the generic OBJECT tag. */
      if( szRetType && ! pFunc->value.asFunc.fProcedure )
      {
         const char * szT = hb_astStandardTypeOrObject( szRetType );
         if( szT )
            fprintf( yyc, " AS %s", szT );
      }
      else if( szRetType && szClassName &&
               pFirstStmt && ! pFirstStmt->value.asClassMethod.fProcedure )
      {
         const char * szT = hb_astStandardTypeOrObject( szRetType );
         if( szT )
            fprintf( yyc, " AS %s", szT );
      }

      if( szClassName )
         fprintf( yyc, " CLASS %s", szClassName );

      fprintf( yyc, "\n" );

      /* Emit body, skipping the leading CLASSMETHOD marker node */
      if( pFunc->value.asFunc.pBody )
      {
         if( szClassName && pFunc->value.asFunc.pBody->type == HB_AST_BLOCK &&
             pFunc->value.asFunc.pBody->value.asBlock.pFirst )
         {
            /* Skip the CLASSMETHOD marker and emit remaining body */
            PHB_AST_NODE pStmt = pFunc->value.asFunc.pBody->value.asBlock.pFirst->pNext;
            s_iLastLine = 0;
            while( pStmt )
            {
               hb_astEmitNode( pStmt, yyc, 1 );
               pStmt = pStmt->pNext;
            }
         }
         else
            hb_astEmitBlock( pFunc->value.asFunc.pBody, yyc, 1 );
      }
   }
}

/* Main entry point: generate transpiled output */
void hb_compGenTranspile( HB_COMP_DECL, PHB_FNAME pFileName )
{
   char szFileName[ HB_PATH_MAX ];
   PHB_AST_NODE pFunc;
   FILE * yyc;

   HB_SYMBOL_UNUSED( HB_COMP_PARAM );

   /* Build output filename with .hb extension.
      Use -o output path if specified, otherwise use source file's directory. */
   {
      PHB_FNAME pOut = hb_fsFNameSplit( HB_COMP_PARAM->szFile );
      pFileName->szExtension = ".hb";
      if( HB_COMP_PARAM->pOutPath && HB_COMP_PARAM->pOutPath->szPath )
         pFileName->szPath = HB_COMP_PARAM->pOutPath->szPath;
      else if( pOut->szPath )
         pFileName->szPath = pOut->szPath;
      hb_fsFNameMerge( szFileName, pFileName );
      hb_xfree( pOut );
   }

   yyc = hb_fopen( szFileName, "w" );
   if( ! yyc )
   {
      hb_compGenError( HB_COMP_PARAM, hb_comp_szErrors, 'E',
                       HB_COMP_ERR_CREATE_OUTPUT, szFileName, NULL );
      return;
   }

   fprintf( yyc, "#include \"astype.ch\"\n" );
   s_iLastLine = 0;

   /* Build the user-function signature table. Merge in any pre-scanned
      table from disk first so cross-file by-ref usage is visible, then
      scan this file's own AST. */
   s_pRefTab = hb_refTabNew();
   hb_refTabLoad( s_pRefTab, hb_refTabGetPath() );
   hb_refTabCollect( s_pRefTab, HB_COMP_PARAM );

   /* Find CLASS nodes in the startup function's body for type propagation */
   s_pClassList = NULL;
   {
      PHB_AST_NODE pFirst = HB_COMP_PARAM->ast.pFuncList;
      if( pFirst && pFirst->type == HB_AST_FUNCTION &&
          pFirst->value.asFunc.pBody &&
          pFirst->value.asFunc.pBody->type == HB_AST_BLOCK )
         s_pClassList = pFirst->value.asFunc.pBody->value.asBlock.pFirst;
   }

   /* Walk the AST function list and compiler function list in parallel */
   {
      PHB_HFUNC pCompFunc = HB_COMP_PARAM->functions.pFirst;
      pFunc = HB_COMP_PARAM->ast.pFuncList;
      while( pFunc )
      {
         if( pFunc->type == HB_AST_FUNCTION )
         {
            /* Advance compiler function list past file-decl functions */
            while( pCompFunc && ( pCompFunc->funFlags & HB_FUNF_FILE_DECL ) )
               pCompFunc = pCompFunc->pNext;

            /* Check if this is the auto-generated startup function */
            if( pCompFunc && ( pCompFunc->funFlags & HB_FUNF_FILE_FIRST ) &&
                pFunc->value.asFunc.pBody )
            {
               /* Emit top-level nodes (CLASS, #include, #define) directly;
                  skip the auto-generated function wrapper */
               PHB_AST_NODE pStmt = pFunc->value.asFunc.pBody->value.asBlock.pFirst;
               HB_BOOL fHasOther = HB_FALSE;
               while( pStmt )
               {
                  if( pStmt->type == HB_AST_CLASS ||
                      pStmt->type == HB_AST_CLASSMETHOD ||
                      pStmt->type == HB_AST_INCLUDE ||
                      pStmt->type == HB_AST_PPDEFINE ||
                      pStmt->type == HB_AST_COMMENT )
                     hb_astEmitNode( pStmt, yyc, 0 );
                  else
                     fHasOther = HB_TRUE;
                  pStmt = pStmt->pNext;
               }
               /* If there were non-top-level statements that aren't method body
                  artifacts, emit the startup function. For now, skip it entirely
                  since method bodies after ENDCLASS leak into the startup function. */
            }
            else if( pFunc->value.asFunc.pBody &&
                     pFunc->value.asFunc.pBody->value.asBlock.pFirst )
               hb_astEmitFunc( pFunc, pCompFunc, yyc );
            if( pCompFunc )
               pCompFunc = pCompFunc->pNext;
         }
         pFunc = pFunc->pNext;
      }
   }

   hb_refTabFree( s_pRefTab );
   s_pRefTab = NULL;
   fclose( yyc );

   if( ! HB_COMP_PARAM->fQuiet )
   {
      char buffer[ HB_PATH_MAX + 64 ];
      hb_snprintf( buffer, sizeof( buffer ),
                   "Generating transpiled output to '%s'... Done.\n", szFileName );
      hb_compOutStd( HB_COMP_PARAM, buffer );
   }
}
