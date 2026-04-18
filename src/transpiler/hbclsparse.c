/*
 * Harbour Transpiler - Class definition pre-parser
 *
 * Scans the preprocessor token stream for CLASS/DATA/METHOD/ENDCLASS
 * patterns and builds AST nodes directly, bypassing the yacc parser.
 *
 * Copyright 2026 harbour.github.io
 */

#include "hbcomp.h"
#include "hbast.h"

/* Get next token from the preprocessor */
static PHB_PP_TOKEN hb_clsNextToken( HB_COMP_DECL )
{
   return hb_pp_tokenGet( HB_COMP_PARAM->pLex->pPP );
}

/* Get current source line from preprocessor */
static int hb_clsCurrLine( HB_COMP_DECL )
{
   return hb_pp_line( HB_COMP_PARAM->pLex->pPP ) + 1;
}

/* Case-insensitive keyword match */
static HB_BOOL hb_clsTokenIs( PHB_PP_TOKEN pToken, const char * szKeyword )
{
   return pToken &&
          HB_PP_TOKEN_TYPE( pToken->type ) == HB_PP_TOKEN_KEYWORD &&
          hb_stricmp( pToken->value, szKeyword ) == 0;
}

/* Check if token is end of line */
static HB_BOOL hb_clsTokenIsEOL( PHB_PP_TOKEN pToken )
{
   return ! pToken ||
          HB_PP_TOKEN_TYPE( pToken->type ) == HB_PP_TOKEN_EOL ||
          HB_PP_TOKEN_TYPE( pToken->type ) == HB_PP_TOKEN_EOC;
}

/* Save a token value as an identifier */
static const char * hb_clsSaveId( HB_COMP_DECL, PHB_PP_TOKEN pToken )
{
   if( pToken && HB_PP_TOKEN_TYPE( pToken->type ) == HB_PP_TOKEN_KEYWORD )
      return hb_compIdentifierNew( HB_COMP_PARAM, pToken->value, HB_IDENT_COPY );
   return NULL;
}

/* Collect remaining tokens on current line into a string */
static const char * hb_clsCollectLine( HB_COMP_DECL, PHB_PP_TOKEN * ppToken )
{
   char szBuf[ 256 ];
   HB_SIZE n = 0;
   PHB_PP_TOKEN pToken = *ppToken;

   szBuf[ 0 ] = '\0';
   while( ! hb_clsTokenIsEOL( pToken ) )
   {
      if( n > 0 && pToken->spaces > 0 && n < sizeof( szBuf ) - 1 )
         szBuf[ n++ ] = ' ';

      /* String tokens need quotes wrapped around them */
      if( HB_PP_TOKEN_TYPE( pToken->type ) == HB_PP_TOKEN_STRING )
      {
         if( n < sizeof( szBuf ) - 2 )
         {
            szBuf[ n++ ] = '"';
            if( n + pToken->len < sizeof( szBuf ) - 2 )
            {
               memcpy( szBuf + n, pToken->value, pToken->len );
               n += pToken->len;
            }
            szBuf[ n++ ] = '"';
         }
      }
      else if( n + pToken->len < sizeof( szBuf ) - 1 )
      {
         memcpy( szBuf + n, pToken->value, pToken->len );
         n += pToken->len;
      }
      pToken = hb_clsNextToken( HB_COMP_PARAM );
   }
   szBuf[ n ] = '\0';
   *ppToken = pToken;
   return n > 0 ? hb_compIdentifierNew( HB_COMP_PARAM, szBuf, HB_IDENT_COPY ) : NULL;
}

/* Skip remaining tokens to end of line */
static PHB_PP_TOKEN hb_clsSkipLine( HB_COMP_DECL, PHB_PP_TOKEN pToken )
{
   while( ! hb_clsTokenIsEOL( pToken ) )
      pToken = hb_clsNextToken( HB_COMP_PARAM );
   return pToken;
}

/* Append a member node to the class node */
static void hb_clsAddMember( PHB_AST_NODE pClass, PHB_AST_NODE pMember )
{
   if( pClass->value.asClass.pMembersLast )
      pClass->value.asClass.pMembersLast->pNext = pMember;
   else
      pClass->value.asClass.pMembers = pMember;
   pClass->value.asClass.pMembersLast = pMember;
}

/*
 * Called from the lexer when a METHOD keyword is detected in LOOKUP state.
 * Checks if this is a method implementation (METHOD name(...) CLASS classname).
 * If so, consumes the header tokens AND creates a proper function scope via
 * hb_compFunctionAdd() so the yacc parser captures the body.
 *
 * Returns HB_TRUE if a METHOD implementation was found and processed.
 */
HB_BOOL hb_compMethodParse( HB_COMP_DECL, HB_BOOL fProcedure )
{
   PHB_PP_TOKEN pToken;
   const char * szMethodName;
   const char * szClassName = NULL;
   const char * szParams[ 32 ];
   int nParams = 0;
   int iLine = hb_clsCurrLine( HB_COMP_PARAM );

   /* Next token should be method name */
   pToken = hb_clsNextToken( HB_COMP_PARAM );
   szMethodName = hb_clsSaveId( HB_COMP_PARAM, pToken );
   if( ! szMethodName )
      return HB_FALSE;

   pToken = hb_clsNextToken( HB_COMP_PARAM );

   /* Capture parameters between ( and ) */
   if( pToken && HB_PP_TOKEN_TYPE( pToken->type ) == HB_PP_TOKEN_LEFT_PB )
   {
      pToken = hb_clsNextToken( HB_COMP_PARAM );
      while( pToken &&
             HB_PP_TOKEN_TYPE( pToken->type ) != HB_PP_TOKEN_RIGHT_PB &&
             ! hb_clsTokenIsEOL( pToken ) )
      {
         if( HB_PP_TOKEN_TYPE( pToken->type ) == HB_PP_TOKEN_KEYWORD &&
             nParams < 32 )
         {
            szParams[ nParams++ ] = hb_clsSaveId( HB_COMP_PARAM, pToken );
         }
         /* Skip commas and other tokens */
         pToken = hb_clsNextToken( HB_COMP_PARAM );
      }
      if( pToken && HB_PP_TOKEN_TYPE( pToken->type ) == HB_PP_TOKEN_RIGHT_PB )
         pToken = hb_clsNextToken( HB_COMP_PARAM );
   }

   /* Scan rest of line looking for CLASS keyword */
   while( ! hb_clsTokenIsEOL( pToken ) )
   {
      if( hb_clsTokenIs( pToken, "CLASS" ) )
      {
         pToken = hb_clsNextToken( HB_COMP_PARAM );
         szClassName = hb_clsSaveId( HB_COMP_PARAM, pToken );
         break;
      }
      pToken = hb_clsNextToken( HB_COMP_PARAM );
   }

   if( ! szClassName )
      return HB_FALSE;  /* Not a METHOD ... CLASS ... line */

   /* Skip rest of line */
   hb_clsSkipLine( HB_COMP_PARAM, pToken );

   /* Register the method under a class-scoped internal name so it
      doesn't collide with a same-named free function in the same
      file — real code does this all the time (e.g. easikzg.prg has
      both `static function BuyVoucher(...)` and `METHOD BuyVoucher
      CLASS EasiKzg`). The emitter uses the HB_AST_CLASSMETHOD node's
      szName/szClass fields for output, so the internal key is opaque.
      Double-underscore matches the existing `<File>__<Var>` STATIC
      mangling convention. */
   {
      size_t nLen = strlen( szClassName ) + 2 + strlen( szMethodName ) + 1;
      char * szKey = ( char * ) hb_xgrab( nLen );
      hb_snprintf( szKey, nLen, "%s__%s", szClassName, szMethodName );
      hb_compFunctionAdd( HB_COMP_PARAM,
                          hb_compIdentifierNew( HB_COMP_PARAM, szKey, HB_IDENT_COPY ),
                          HB_FS_PUBLIC,
                          fProcedure ? HB_FUNF_PROCEDURE : 0 );
      hb_xfree( szKey );
   }

   /* Add parameters as local variables */
   if( nParams > 0 )
   {
      int i;
      HB_COMP_PARAM->iVarScope = HB_VSCOMP_PARAMETER;
      for( i = 0; i < nParams; i++ )
      {
         hb_compVariableAdd( HB_COMP_PARAM, szParams[ i ],
                             hb_compVarTypeNew( HB_COMP_PARAM, 0, NULL ) );
      }
      HB_COMP_PARAM->iVarScope = HB_VSCOMP_NONE;
   }

   /* Create a METHOD AST node as the first statement in the function body
      so the emitter knows this is a METHOD, not a PROCEDURE */
   {
      PHB_AST_NODE pMethod = hb_astNew( HB_AST_CLASSMETHOD, iLine );
      pMethod->value.asClassMethod.szName      = szMethodName;
      pMethod->value.asClassMethod.szClass     = szClassName;
      pMethod->value.asClassMethod.pBody       = NULL;
      pMethod->value.asClassMethod.iScope      = HB_AST_SCOPE_EXPORTED;
      pMethod->value.asClassMethod.fProcedure  = fProcedure;
      hb_astAppend( HB_COMP_PARAM, pMethod );
   }

   return HB_TRUE;
}

/*
 * Called from the lexer when a CLASS keyword is detected in LOOKUP state.
 * The initial CLASS token has already been fetched.
 * Consumes all tokens through ENDCLASS and builds AST nodes.
 *
 * Returns HB_TRUE if a CLASS was successfully parsed.
 */
HB_BOOL hb_compClassParse( HB_COMP_DECL )
{
   PHB_PP_TOKEN pToken;
   PHB_AST_NODE pClass;
   const char * szName;
   const char * szParent = NULL;
   int iLine = hb_clsCurrLine( HB_COMP_PARAM );

   /* Next token should be class name */
   pToken = hb_clsNextToken( HB_COMP_PARAM );
   szName = hb_clsSaveId( HB_COMP_PARAM, pToken );
   if( ! szName )
      return HB_FALSE;

   /* Look for INHERIT/FROM */
   pToken = hb_clsNextToken( HB_COMP_PARAM );
   if( hb_clsTokenIs( pToken, "INHERIT" ) || hb_clsTokenIs( pToken, "FROM" ) )
   {
      pToken = hb_clsNextToken( HB_COMP_PARAM );
      szParent = hb_clsSaveId( HB_COMP_PARAM, pToken );
      pToken = hb_clsNextToken( HB_COMP_PARAM );
   }

   /* Skip rest of CLASS line */
   pToken = hb_clsSkipLine( HB_COMP_PARAM, pToken );

   /* Create CLASS node */
   pClass = hb_astNew( HB_AST_CLASS, iLine );
   pClass->value.asClass.szName       = szName;
   pClass->value.asClass.szParent     = szParent;
   pClass->value.asClass.pMembers     = NULL;
   pClass->value.asClass.pMembersLast = NULL;

   /* Track current scope — default is EXPORTED */
   {
      int iScope = HB_AST_SCOPE_EXPORTED;

      /* Parse members until ENDCLASS */
      for( ;; )
      {
         pToken = hb_clsNextToken( HB_COMP_PARAM );

         /* Skip EOL/EOC */
         while( hb_clsTokenIsEOL( pToken ) && pToken )
            pToken = hb_clsNextToken( HB_COMP_PARAM );

         if( ! pToken )
            break;

         if( hb_clsTokenIs( pToken, "ENDCLASS" ) )
         {
            pToken = hb_clsNextToken( HB_COMP_PARAM );
            hb_clsSkipLine( HB_COMP_PARAM, pToken );
            break;
         }
         /* `END CLASS` — Harbour's hbclass.ch has a PP rule for this
            but the transpiler skips hbclass.ch (ppcomp.c), so the
            class parser must handle it natively. Only match `END`
            when followed by `CLASS` to avoid false-matching END IF /
            END SEQUENCE / etc. inside INLINE methods. */
         /* NOTE: only `ENDCLASS` (one word) is supported. Harbour's
            `END CLASS` (two words) requires hbclass.ch PP rules which
            the transpiler doesn't load. Source files using `END CLASS`
            must be changed to `ENDCLASS` before transpiling. */

         /* Scope section headers: EXPORTED/VISIBLE, PROTECTED, HIDDEN */
         if( hb_clsTokenIs( pToken, "EXPORTED" ) || hb_clsTokenIs( pToken, "VISIBLE" ) ||
             hb_clsTokenIs( pToken, "EXPORT" ) )
         {
            iScope = HB_AST_SCOPE_EXPORTED;
            hb_clsSkipLine( HB_COMP_PARAM, hb_clsNextToken( HB_COMP_PARAM ) );
            continue;
         }
         if( hb_clsTokenIs( pToken, "PROTECTED" ) )
         {
            iScope = HB_AST_SCOPE_PROTECTED;
            hb_clsSkipLine( HB_COMP_PARAM, hb_clsNextToken( HB_COMP_PARAM ) );
            continue;
         }
         if( hb_clsTokenIs( pToken, "HIDDEN" ) )
         {
            iScope = HB_AST_SCOPE_HIDDEN;
            hb_clsSkipLine( HB_COMP_PARAM, hb_clsNextToken( HB_COMP_PARAM ) );
            continue;
         }

         /* `CLASS VAR name` / `CLASS DATA name` — two-word form that
            the PP tokenizer splits into separate CLASS + VAR/DATA
            tokens. Equivalent to `CLASSVAR` / `CLASSDATA`. Promote
            the current token to VAR/DATA and mark iKind below. */
         {
            HB_BOOL fClassPrefix = HB_FALSE;
            if( hb_clsTokenIs( pToken, "CLASS" ) )
            {
               PHB_PP_TOKEN pNext = pToken->pNext;
               if( pNext &&
                   ( hb_clsTokenIs( pNext, "VAR" ) ||
                     hb_clsTokenIs( pNext, "DATA" ) ) )
               {
                  pToken = hb_clsNextToken( HB_COMP_PARAM );   /* consume CLASS, now at VAR/DATA */
                  fClassPrefix = HB_TRUE;
               }
            }

         /* DATA / VAR / CLASSDATA / CLASS VAR / ACCESS / ASSIGN, plus
            the hbclass.ch shorthand forms PROTECT and EXPORT, which
            expand to `PROTECTED: VAR <name>` / `EXPORTED: VAR <name>`.
            We don't load hbclass.ch (the PP rules there trigger
            recursive includes that hang the parser); handling the
            shorthands natively here is the alternative. Without this,
            `PROTECT oEasiCdSOR` and `EXPORT oWnd` lines silently fall
            through the unknown-token branch and the class is left
            without those DATA members — ~368 such declarations in
            the easipos corpus. */
         if( hb_clsTokenIs( pToken, "DATA" ) || hb_clsTokenIs( pToken, "VAR" ) ||
             hb_clsTokenIs( pToken, "CLASSDATA" ) || hb_clsTokenIs( pToken, "CLASSVAR" ) ||
             hb_clsTokenIs( pToken, "ACCESS" ) || hb_clsTokenIs( pToken, "ASSIGN" ) ||
             hb_clsTokenIs( pToken, "PROTECT" ) || hb_clsTokenIs( pToken, "EXPORT" ) )
         {
            PHB_AST_NODE pData;
            const char * szDataName;
            const char * szType = NULL;
            const char * szInit = NULL;
            int iKind = HB_AST_DATA_INSTANCE;
            int iMemberScope = iScope;
            HB_BOOL fReadOnly = HB_FALSE;

            if( fClassPrefix ||
                hb_clsTokenIs( pToken, "CLASSDATA" ) ||
                hb_clsTokenIs( pToken, "CLASSVAR" ) )
               iKind = HB_AST_DATA_CLASS;
            else if( hb_clsTokenIs( pToken, "ACCESS" ) )
               iKind = HB_AST_DATA_ACCESS;
            else if( hb_clsTokenIs( pToken, "ASSIGN" ) )
               iKind = HB_AST_DATA_ASSIGN;
            else if( hb_clsTokenIs( pToken, "PROTECT" ) )
               iMemberScope = HB_AST_SCOPE_PROTECTED;
            else if( hb_clsTokenIs( pToken, "EXPORT" ) )
               iMemberScope = HB_AST_SCOPE_EXPORTED;

            pToken = hb_clsNextToken( HB_COMP_PARAM );
            szDataName = hb_clsSaveId( HB_COMP_PARAM, pToken );
            pToken = hb_clsNextToken( HB_COMP_PARAM );

            /* Parse optional clauses in any order until EOL */
            while( ! hb_clsTokenIsEOL( pToken ) )
            {
               if( hb_clsTokenIs( pToken, "AS" ) )
               {
                  pToken = hb_clsNextToken( HB_COMP_PARAM );
                  szType = hb_clsSaveId( HB_COMP_PARAM, pToken );
                  pToken = hb_clsNextToken( HB_COMP_PARAM );
               }
               else if( hb_clsTokenIs( pToken, "INIT" ) )
               {
                  pToken = hb_clsNextToken( HB_COMP_PARAM );
                  szInit = hb_clsCollectLine( HB_COMP_PARAM, &pToken );
               }
               else if( hb_clsTokenIs( pToken, "EXPORTED" ) || hb_clsTokenIs( pToken, "VISIBLE" ) ||
                        hb_clsTokenIs( pToken, "EXPORT" ) )
               {
                  iMemberScope = HB_AST_SCOPE_EXPORTED;
                  pToken = hb_clsNextToken( HB_COMP_PARAM );
               }
               else if( hb_clsTokenIs( pToken, "PROTECTED" ) )
               {
                  iMemberScope = HB_AST_SCOPE_PROTECTED;
                  pToken = hb_clsNextToken( HB_COMP_PARAM );
               }
               else if( hb_clsTokenIs( pToken, "HIDDEN" ) )
               {
                  iMemberScope = HB_AST_SCOPE_HIDDEN;
                  pToken = hb_clsNextToken( HB_COMP_PARAM );
               }
               else if( hb_clsTokenIs( pToken, "READONLY" ) || hb_clsTokenIs( pToken, "RO" ) )
               {
                  fReadOnly = HB_TRUE;
                  pToken = hb_clsNextToken( HB_COMP_PARAM );
               }
               else
               {
                  /* Unknown clause — skip token */
                  pToken = hb_clsNextToken( HB_COMP_PARAM );
               }
            }

            pData = hb_astNew( HB_AST_CLASSDATA, hb_clsCurrLine( HB_COMP_PARAM ) );
            pData->value.asClassData.szName    = szDataName;
            pData->value.asClassData.szType    = szType;
            pData->value.asClassData.szInit    = szInit;
            pData->value.asClassData.iScope    = iMemberScope;
            pData->value.asClassData.iKind     = iKind;
            pData->value.asClassData.fReadOnly = fReadOnly;
            hb_clsAddMember( pClass, pData );
         }
         else if( hb_clsTokenIs( pToken, "METHOD" ) ||
                  hb_clsTokenIs( pToken, "PROCEDURE" ) )
         {
            PHB_AST_NODE pMethod;
            const char * szMethodName;
            const char * szParams = NULL;
            const char * szInline = NULL;
            int iMemberScope = iScope;
            HB_BOOL fProcedure = hb_clsTokenIs( pToken, "PROCEDURE" );

            pToken = hb_clsNextToken( HB_COMP_PARAM );
            szMethodName = hb_clsSaveId( HB_COMP_PARAM, pToken );
            pToken = hb_clsNextToken( HB_COMP_PARAM );

            /* Capture parameters between ( and ) */
            if( pToken && HB_PP_TOKEN_TYPE( pToken->type ) == HB_PP_TOKEN_LEFT_PB )
            {
               char szBuf[ 256 ];
               HB_SIZE n = 0;
               pToken = hb_clsNextToken( HB_COMP_PARAM );
               while( pToken &&
                      HB_PP_TOKEN_TYPE( pToken->type ) != HB_PP_TOKEN_RIGHT_PB &&
                      ! hb_clsTokenIsEOL( pToken ) )
               {
                  if( HB_PP_TOKEN_TYPE( pToken->type ) == HB_PP_TOKEN_KEYWORD )
                  {
                     if( n > 0 && n < sizeof( szBuf ) - 3 )
                     {
                        szBuf[ n++ ] = ',';
                        szBuf[ n++ ] = ' ';
                     }
                     if( n + pToken->len < sizeof( szBuf ) - 1 )
                     {
                        memcpy( szBuf + n, pToken->value, pToken->len );
                        n += pToken->len;
                     }
                  }
                  pToken = hb_clsNextToken( HB_COMP_PARAM );
               }
               szBuf[ n ] = '\0';
               if( n > 0 )
                  szParams = hb_compIdentifierNew( HB_COMP_PARAM, szBuf, HB_IDENT_COPY );
               if( pToken && HB_PP_TOKEN_TYPE( pToken->type ) == HB_PP_TOKEN_RIGHT_PB )
                  pToken = hb_clsNextToken( HB_COMP_PARAM );
            }

            /* Scan rest of line for scope modifiers and INLINE body.
               INLINE is Harbour's short-form method body — everything
               after the keyword up to EOL is the expression whose value
               the method returns. hb_clsCollectLine captures it
               verbatim (parens and all) so the emitter can re-parse it
               as a Harbour expression at emit time. */
            while( ! hb_clsTokenIsEOL( pToken ) )
            {
               if( hb_clsTokenIs( pToken, "EXPORTED" ) || hb_clsTokenIs( pToken, "VISIBLE" ) ||
                   hb_clsTokenIs( pToken, "EXPORT" ) )
               {
                  iMemberScope = HB_AST_SCOPE_EXPORTED;
                  pToken = hb_clsNextToken( HB_COMP_PARAM );
               }
               else if( hb_clsTokenIs( pToken, "PROTECTED" ) )
               {
                  iMemberScope = HB_AST_SCOPE_PROTECTED;
                  pToken = hb_clsNextToken( HB_COMP_PARAM );
               }
               else if( hb_clsTokenIs( pToken, "HIDDEN" ) )
               {
                  iMemberScope = HB_AST_SCOPE_HIDDEN;
                  pToken = hb_clsNextToken( HB_COMP_PARAM );
               }
               else if( hb_clsTokenIs( pToken, "INLINE" ) )
               {
                  pToken = hb_clsNextToken( HB_COMP_PARAM );
                  szInline = hb_clsCollectLine( HB_COMP_PARAM, &pToken );
               }
               else
                  pToken = hb_clsNextToken( HB_COMP_PARAM );
            }

            pMethod = hb_astNew( HB_AST_CLASSMETHOD, hb_clsCurrLine( HB_COMP_PARAM ) );
            pMethod->value.asClassMethod.szName      = szMethodName;
            pMethod->value.asClassMethod.szClass      = NULL;
            pMethod->value.asClassMethod.szParams     = szParams;
            pMethod->value.asClassMethod.pBody        = NULL;
            pMethod->value.asClassMethod.szInline     = szInline;
            pMethod->value.asClassMethod.iScope       = iMemberScope;
            pMethod->value.asClassMethod.fProcedure   = fProcedure;
            hb_clsAddMember( pClass, pMethod );
         }
         else
         {
            /* Skip unknown line */
            hb_clsSkipLine( HB_COMP_PARAM, pToken );
         }
         }  /* close fClassPrefix scope */
      }
   }

   /* Add CLASS to startup function's body (top-level scope).
      This is needed because hb_compClassParse may be called while
      a method body is open, and we don't want the CLASS nested
      inside another function's body. */
   hb_astAppendToStartup( HB_COMP_PARAM, pClass );

   return HB_TRUE;
}
