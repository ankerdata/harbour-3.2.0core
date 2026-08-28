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
#include "hbdefinemap.h"
#include "hbfieldtypes.h"
#include "hbhbxcanon.h"
#include "hbfilecase.h"

/* Forward declarations */
static void hb_csEmitExpr( PHB_EXPR pExpr, FILE * yyc, HB_BOOL fParen );
static void hb_csEmitNode( PHB_AST_NODE pNode, FILE * yyc, int iIndent );
static void hb_csEmitBlock( PHB_AST_NODE pBlock, FILE * yyc, int iIndent );
static void hb_csEmitCallArgs( const char * szFunc, PHB_EXPR pParms, FILE * yyc );
static void hb_csEmitIndent( FILE * yyc, int iIndent );

/* Emit a dim expression inside an `new dynamic[<dim>]` allocation. A
   long integer literal goes bare (`5`); anything else is wrapped in
   `(long)(...)` — C# array-creation sizes take long natively, and the
   cast coerces whatever Harbour computed at runtime (a `#define`d
   constant, a `decimal` local, ...). Single integral tier: nothing the
   emitter produces is Int32. NULL pDim falls back to a literal 0 so
   the site still compiles. */
static void hb_csEmitArrayDim( PHB_EXPR pDim, FILE * yyc )
{
   if( ! pDim )
   {
      fprintf( yyc, "0" );
      return;
   }
   if( pDim->ExprType == HB_ET_NUMERIC &&
       pDim->value.asNum.NumType == HB_ET_LONG )
   {
      fprintf( yyc, "%" HB_PF64 "d", pDim->value.asNum.val.l );
      return;
   }
   fprintf( yyc, "(long)(" );
   hb_csEmitExpr( pDim, yyc, HB_FALSE );
   fprintf( yyc, ")" );
}
static const char * hb_csTypeMap( const char * szHbType );
static const char * hb_csShimSlotType( const HB_REFPARAM * pP, char * szBuf,
                                       HB_SIZE nBuf );
static HB_BOOL hb_csIsFileMemvar( const char * szName );

/* Track last source line for blank line preservation */
static int s_iLastLine = 0;
/* Source line of the statement currently being emitted. Updated at the
   top of every hb_csEmitNode so anything fired from inside an
   expression walk (e.g. W0018 extra-arg warnings) can attach a source
   coordinate. s_pCompCtx->currLine is stuck at end-of-file during
   codegen so it can't be used for this. */
static int s_iCurrentStmtLine = 0;
/* pComp pointer captured at hb_compGenCSharp entry so deeply-nested
   static emitters can call hb_compGenError without each having to
   accept a HB_COMP_DECL parameter. */
static PHB_COMP s_pCompCtx = NULL;
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
static char s_szCurrentClass[ 128 ] = "";  /* class name of the method currently
                                              being emitted; "" for free functions.
                                              Lets `Self:classvar` emit `Class.var`
                                              instead of `this.var` (CS0176). */
static HB_BOOL s_fCurrentClassDynamic = HB_FALSE;  /* current class extends
                                              HbDynamicObject — a `Self:` access
                                              to an undeclared member routes
                                              through `((dynamic)this)`. */

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
/* Parallel array — Harbour-side type name (NUMERIC/STRING/...) for each
   file-static, used by the assignment emitter to convert `:= NIL` to
   `= default` when the LHS is a value type. */
static const char * s_pFileStaticTypes[ HB_CS_MAX_FILE_STATICS ];
/* Parallel array — owning function name for STATICs declared inside a
   function body, NULL for file-scope STATICs (those declared before
   the first function, which the parser parks in the file-decl head).
   Harbour scopes a function-body STATIC to that function only; two
   functions each declaring `STATIC nCounter` are two variables. The
   owner feeds both lookup (a function resolves its own statics first,
   then file-scope ones) and the emitted field name
   (`<FileBase>_<Owner>_<Var>` vs `<FileBase>_<Var>`). */
static const char * s_pFileStaticOwners[ HB_CS_MAX_FILE_STATICS ];
static int s_iFileStaticCount = 0;
static char s_szFileBase[ 64 ] = "";
/* Scope for registry lookups: the function whose body is currently
   being walked or emitted; NULL = file level. Set by hb_csEmitFunc /
   hb_csEmitMethodBody and by the pre-pass walkers in
   hb_compGenCSharp. */
static const char * s_szStaticScope = NULL;
/* The file-decl head of ast.pFuncList — statics found in its body are
   file-scope. Captured at hb_compGenCSharp entry. */
static PHB_AST_NODE s_pFileDeclFunc = NULL;

/* Column at which a hash/array literal should place each top-level
   element when broken across lines. Zero means "stay on one line"
   (the historical behaviour and the only mode that's safe inside
   nested expression contexts where we don't track the running cut
   point). Set just before emitting a multi-line-eligible initializer
   (currently file-static class fields) and cleared right after, so
   incidental expression emits elsewhere are unaffected. */
static int s_iExprIndent = 0;

/* C# key type an empty/key-less hash literal should emit with —
   pointed at the declared variable's key type around decl-init
   emission (see HB_ET_HASH). Default string. */
static const char * s_szHashKeyCs = "string";

static const char * hb_csHashKeyCsFor( const char * szType )
{
   return ( szType && hb_stricmp( szType, "HASHN" ) == 0 )
          ? "decimal" : "string";
}

static const char * hb_csLocalTypeGet( const char * szName );
static HB_BOOL hb_csSendMemberIsInteger( PHB_EXPR pSend );

/* Writes into an INTEGER-typed variable need an (int) coercion for
   anything but an int literal: the classifier proved integral-ness in
   Harbour terms, but the C#-side types still disagree (Len() returns
   decimal). Reads need nothing — int widens implicitly. */
static HB_BOOL hb_csNeedsIntCast( PHB_EXPR pExpr )
{
   if( pExpr && pExpr->ExprType == HB_ET_NUMERIC &&
       pExpr->value.asNum.NumType == HB_ET_LONG )
      return HB_FALSE;

   /* An ORM def-class field access whose generated model property is
      already C# int needs no coercion either — `int n = oDept.nNo`
      is int = int. */
   if( pExpr && pExpr->ExprType == HB_ET_SEND &&
       pExpr->value.asMessage.szMessage &&
       pExpr->value.asMessage.pObject &&
       pExpr->value.asMessage.pObject->ExprType == HB_ET_VARIABLE )
   {
      const char * szRecvType = hb_csLocalTypeGet(
         pExpr->value.asMessage.pObject->value.asSymbol.name );
      if( szRecvType && hb_fieldTypesClassCanon( szRecvType ) )
      {
         const char * szCs = hb_fieldTypesMember( szRecvType,
            pExpr->value.asMessage.szMessage, NULL );
         if( szCs && ( hb_stricmp( szCs, "int" ) == 0 ||
                       hb_stricmp( szCs, "long" ) == 0 ) )
            return HB_FALSE;
      }
      /* declared-integral user-class member reads are already long */
      if( hb_csSendMemberIsInteger( pExpr ) )
         return HB_FALSE;
   }

   return HB_TRUE;
}

static HB_BOOL hb_csVarIsInteger( const char * szName )
{
   const char * szT = hb_csLocalTypeGet( szName );
   return szT && hb_stricmp( szT, "INTEGER" ) == 0;
}

/* True when the current class declares szMember with a type that maps
   to C# int (source-side `as int` — the dialog-model convention).
   Writes into such members need the same (int) coercion as int locals. */
static HB_BOOL hb_csMemberIsInteger( const char * szMember );

/* Emit-side: does this expression have a C#-integral static type
   (int/long)? Drives the (decimal) cast that keeps `/` on Harbour
   float semantics — C# int/int truncates (7/2 == 3, Harbour 3.5).
   Errs in the safe direction: a wrong TRUE adds a harmless (decimal)
   cast to an already-decimal division; a wrong FALSE risks silent
   truncation, so every int source the emitter can produce is
   enumerated:
     - integer literals (C# infers int/long)
     - INTEGER-typed locals/statics (Pass 2.5)
     - int/long #define consts (defines map)
     - ORM def-class int/long fields (fieldtypes map)
     - `as int` members of the current class
     - +,-,*,%,unary over integral operands (C# int arithmetic stays
       int); / and ^ excluded — they emit decimal-producing forms. */
static HB_BOOL hb_csExprIsCsIntegral( PHB_EXPR pExpr )
{
   if( ! pExpr )
      return HB_FALSE;
   switch( pExpr->ExprType )
   {
      case HB_ET_NUMERIC:
         return pExpr->value.asNum.NumType == HB_ET_LONG;

      case HB_ET_VARIABLE:
      {
         const char * szName = pExpr->value.asSymbol.name;
         const char * szDefType;
         if( hb_csVarIsInteger( szName ) )
            return HB_TRUE;
         szDefType = hb_defineMapLookupType( szName );
         return szDefType && ( hb_stricmp( szDefType, "int" ) == 0 ||
                               hb_stricmp( szDefType, "long" ) == 0 );
      }

      case HB_ET_SEND:
         if( pExpr->value.asMessage.szMessage &&
             pExpr->value.asMessage.pObject &&
             pExpr->value.asMessage.pObject->ExprType == HB_ET_VARIABLE )
         {
            PHB_EXPR pRecv = pExpr->value.asMessage.pObject;
            if( hb_stricmp( pRecv->value.asSymbol.name, "Self" ) == 0 )
               return hb_csMemberIsInteger(
                  pExpr->value.asMessage.szMessage );
            {
               const char * szRecvType =
                  hb_csLocalTypeGet( pRecv->value.asSymbol.name );
               if( szRecvType && hb_fieldTypesClassCanon( szRecvType ) )
               {
                  const char * szCs = hb_fieldTypesMember( szRecvType,
                     pExpr->value.asMessage.szMessage, NULL );
                  return szCs && ( strcmp( szCs, "int" ) == 0 ||
                                   strcmp( szCs, "long" ) == 0 );
               }
            }
            /* user-class members declared AS INTEGER */
            return hb_csSendMemberIsInteger( pExpr );
         }
         return HB_FALSE;

      case HB_EO_PLUS:
      case HB_EO_MINUS:
      case HB_EO_MULT:
      case HB_EO_MOD:
         return hb_csExprIsCsIntegral( pExpr->value.asOperator.pLeft ) &&
                hb_csExprIsCsIntegral( pExpr->value.asOperator.pRight );

      case HB_EO_NEGATE:
      case HB_EO_PREINC:
      case HB_EO_PREDEC:
      case HB_EO_POSTINC:
      case HB_EO_POSTDEC:
         return hb_csExprIsCsIntegral( pExpr->value.asOperator.pLeft );

      default:
         break;
   }
   /* Parenthesised single-element list */
   if( ( pExpr->ExprType == HB_ET_LIST ||
         pExpr->ExprType == HB_ET_ARGLIST ) &&
       pExpr->value.asList.pExprList &&
       ! pExpr->value.asList.pExprList->pNext )
      return hb_csExprIsCsIntegral( pExpr->value.asList.pExprList );
   return HB_FALSE;
}

static HB_BOOL hb_csSendMemberIsInteger( PHB_EXPR pSend )
{
   PHB_EXPR pRecv;
   const char * szCls;
   int iDepth;

   if( ! pSend || pSend->ExprType != HB_ET_SEND ||
       ! pSend->value.asMessage.szMessage )
      return HB_FALSE;
   pRecv = pSend->value.asMessage.pObject;
   if( ! pRecv || pRecv->ExprType != HB_ET_VARIABLE )
      return HB_FALSE;
   if( hb_stricmp( pRecv->value.asSymbol.name, "Self" ) == 0 )
      return hb_csMemberIsInteger( pSend->value.asMessage.szMessage );
   szCls = hb_csLocalTypeGet( pRecv->value.asSymbol.name );
   if( ! szCls )
      return HB_FALSE;
   /* ORM def-class field (map, not reftab) — integral tokens are C#
      long, so a decimal write needs the same coercion as an AS INTEGER
      member (oClock.nClerkNo = nDecimalLocal). */
   if( hb_fieldTypesClassCanon( szCls ) )
   {
      const char * szCs = hb_fieldTypesMember( szCls,
         pSend->value.asMessage.szMessage, NULL );
      return szCs && ( hb_stricmp( szCs, "int" ) == 0 ||
                       hb_stricmp( szCs, "long" ) == 0 );
   }
   if( ! s_pRefTab || ! hb_refTabIsClass( s_pRefTab, szCls ) )
      return HB_FALSE;
   for( iDepth = 0; szCls && iDepth < 16; iDepth++ )
   {
      const char * szMT = hb_refTabReturnType( s_pRefTab,
         hb_refTabMethodKey( szCls, pSend->value.asMessage.szMessage ) );
      if( szMT )
         return hb_stricmp( szMT, "INTEGER" ) == 0;
      szCls = hb_refTabClassParent( s_pRefTab, szCls );
   }
   return HB_FALSE;
}

/* Find the registry slot szName resolves to under the current scope:
   a STATIC owned by the function being walked/emitted wins, then a
   file-scope STATIC (owner NULL). A function never sees another
   function's statics — that's the Harbour visibility rule this
   registry exists to reproduce. Returns -1 when not visible. */
static int hb_csFileStaticIdx( const char * szName )
{
   int i;
   if( ! szName || s_iFileStaticCount == 0 )
      return -1;
   if( s_szStaticScope )
   {
      for( i = 0; i < s_iFileStaticCount; i++ )
         if( s_pFileStaticOwners[ i ] &&
             hb_stricmp( s_pFileStaticOwners[ i ], s_szStaticScope ) == 0 &&
             hb_stricmp( s_pFileStatics[ i ], szName ) == 0 )
            return i;
   }
   for( i = 0; i < s_iFileStaticCount; i++ )
      if( ! s_pFileStaticOwners[ i ] &&
          hb_stricmp( s_pFileStatics[ i ], szName ) == 0 )
         return i;
   return -1;
}

static HB_BOOL hb_csIsFileStatic( const char * szName )
{
   return hb_csFileStaticIdx( szName ) >= 0;
}

/* Write slot i's C# field name into buf and return buf:
   `<FileBase>_<Owner>_<Var>` for a function-scope STATIC,
   `<FileBase>_<Var>` for a file-scope one. Uses the declaration-site
   casing (Harbour identifiers are case-insensitive; C# isn't). */
static const char * hb_csStaticFieldName( int i, char * buf, int nLen )
{
   if( s_pFileStaticOwners[ i ] )
      hb_snprintf( buf, nLen, "%s_%s_%s", s_szFileBase,
                   s_pFileStaticOwners[ i ], s_pFileStatics[ i ] );
   else
      hb_snprintf( buf, nLen, "%s_%s", s_szFileBase,
                   s_pFileStatics[ i ] );
   return buf;
}

/* Return the inferred Harbour-side type name for the file-static
   szName (NUMERIC, STRING, ARRAY, ...), or NULL if unknown / not
   a file-static / not registered with a type. */
static const char * hb_csFileStaticType( const char * szName )
{
   int i = hb_csFileStaticIdx( szName );
   return i >= 0 ? s_pFileStaticTypes[ i ] : NULL;
}

static HB_BOOL hb_csIsValueType( const char * szType )
{
   return szType && (
      hb_stricmp( szType, "NUMERIC"   ) == 0 ||
      hb_stricmp( szType, "INTEGER"   ) == 0 ||
      hb_stricmp( szType, "LOGICAL"   ) == 0 ||
      hb_stricmp( szType, "DATE"      ) == 0 ||
      hb_stricmp( szType, "TIMESTAMP" ) == 0 );
}

/* Register a STATIC under its owner (NULL = file scope). Dedup is per
   (owner, name): the collection passes walk every function's body more
   than once per emission, but two different functions declaring the
   same name are two distinct variables and get two slots. */
static void hb_csAddFileStatic( const char * szName, const char * szOwner )
{
   int i;
   if( ! szName || s_iFileStaticCount >= HB_CS_MAX_FILE_STATICS )
      return;
   for( i = 0; i < s_iFileStaticCount; i++ )
   {
      if( hb_stricmp( s_pFileStatics[ i ], szName ) != 0 )
         continue;
      if( ! s_pFileStaticOwners[ i ] && ! szOwner )
         return;
      if( s_pFileStaticOwners[ i ] && szOwner &&
          hb_stricmp( s_pFileStaticOwners[ i ], szOwner ) == 0 )
         return;
   }
   s_pFileStaticTypes[ s_iFileStaticCount ] = NULL;
   s_pFileStaticOwners[ s_iFileStaticCount ] = szOwner;
   s_pFileStatics[ s_iFileStaticCount++ ] = szName;
}

/* Record (or update) the inferred type for a previously-registered
   STATIC, resolved under the current scope. Tolerates being called
   before the name was added — silently no-ops in that case. */
static void hb_csSetFileStaticType( const char * szName, const char * szType )
{
   int i = hb_csFileStaticIdx( szName );
   if( i >= 0 )
      s_pFileStaticTypes[ i ] = szType;
}

/* Per-function local-variable types, recorded as each LOCAL/STATIC
   declaration is emitted (the Harbour-side type that drove the C#
   declaration: szAlias if propagated, else hb_astInferType). The
   ref-shim decision consults this so it compares an @arg against the
   variable's *real* emitted type rather than a Hungarian prefix that
   can disagree (a `c3rdParty` whose digit defeats prefix detection, an
   `aRCFlag` static that is actually `dynamic`). Stored pointers are
   AST/literal-owned and stable for the whole emit; reset per function. */
#define HB_CS_MAX_LOCALS 1024
static const char * s_pLocalNames[ HB_CS_MAX_LOCALS ];
static const char * s_pLocalTypes[ HB_CS_MAX_LOCALS ];
static int s_iLocalCount = 0;

static void hb_csLocalTypeReset( void )
{
   s_iLocalCount = 0;
}

static void hb_csLocalTypeSet( const char * szName, const char * szType )
{
   int i;
   if( ! szName || ! szType )
      return;
   for( i = 0; i < s_iLocalCount; i++ )
      if( hb_stricmp( s_pLocalNames[ i ], szName ) == 0 )
      {
         s_pLocalTypes[ i ] = szType;   /* last declaration wins */
         return;
      }
   if( s_iLocalCount >= HB_CS_MAX_LOCALS )
      return;
   s_pLocalNames[ s_iLocalCount ] = szName;
   s_pLocalTypes[ s_iLocalCount++ ] = szType;
}

static const char * hb_csLocalTypeGet( const char * szName )
{
   int i;
   if( ! szName )
      return NULL;
   for( i = 0; i < s_iLocalCount; i++ )
      if( hb_stricmp( s_pLocalNames[ i ], szName ) == 0 )
         return s_pLocalTypes[ i ];
   return NULL;
}

/* The Harbour-side type of an @arg variable at a call site, resolved the
   way the variable was actually emitted: a function local/static first,
   then a file-static (untyped → USUAL, i.e. `dynamic`), then a parameter
   of the current function, and only as a last resort the Hungarian-prefix
   guess. */
static const char * hb_csArgVarType( const char * szName )
{
   const char * szType = hb_csLocalTypeGet( szName );
   if( szType )
      return szType;
   if( hb_csIsFileStatic( szName ) )
   {
      szType = hb_csFileStaticType( szName );
      return szType ? szType : "USUAL";
   }
   if( szName && s_szCurrentFunc[ 0 ] && s_pRefTab )
   {
      int n = hb_refTabParamCount( s_pRefTab, s_szCurrentFunc );
      int i;
      for( i = 0; i < n; i++ )
      {
         const HB_REFPARAM * pP =
            hb_refTabParam( s_pRefTab, s_szCurrentFunc, i );
         if( pP && pP->szName && hb_stricmp( pP->szName, szName ) == 0 )
            return pP->szType ? pP->szType : "USUAL";
      }
   }
   /* PUBLIC / MEMVAR variables are runtime-typed — always emitted as
      `dynamic` regardless of any Hungarian prefix. */
   if( hb_csIsFileMemvar( szName ) ||
       ( szName && s_pRefTab && hb_refTabIsPublic( s_pRefTab, szName ) ) )
      return "USUAL";
   return hb_astInferType( szName, NULL );
}

/* File-scope MEMVAR declarations. Harbour memvars are globally visible
   at runtime, but the MEMVAR declaration itself is per-file — it tells
   THIS file's compiler to treat bare references to the name as memvar
   accesses. We mirror the file-static pattern: each file's MEMVAR is
   hoisted to a `public static dynamic <filebase>_<name>;` field under
   the merged partial class Program, and references inside the file
   get rewritten to the mangled name. Cross-file sharing is sacrificed
   to avoid CS0102 duplicate-member errors when two files both declare
   MEMVAR <name>. PUBLIC / PRIVATE inside a function body, when the name
   appears in this registry, emits as an assignment to the class field
   instead of a local variable declaration. */
#define HB_CS_MAX_FILE_MEMVARS 256
static const char * s_pFileMemvars[ HB_CS_MAX_FILE_MEMVARS ];
static int s_iFileMemvarCount = 0;

/* File-scope STATIC function registry. Harbour `STATIC FUNCTION foo`
   and `STATIC PROCEDURE foo` are file-private — callable only from the
   same .prg. The transpiler merges every standalone function into one
   `public static partial class Program`, so two files each declaring
   `STATIC FUNCTION Helper()` collide (CS0111). Same mechanism as for
   STATIC vars: collect the STATIC function names once, rewrite both
   the declaration and every intra-file call site to
   `<FileBase>_<FuncName>`, file-unique by construction. */
#define HB_CS_MAX_FILE_STATIC_FUNCS 256
static const char * s_pFileStaticFuncs[ HB_CS_MAX_FILE_STATIC_FUNCS ];
static int s_iFileStaticFuncCount = 0;

static HB_BOOL hb_csIsFileStaticFunc( const char * szName )
{
   int i;
   if( ! szName || s_iFileStaticFuncCount == 0 )
      return HB_FALSE;
   for( i = 0; i < s_iFileStaticFuncCount; i++ )
      if( hb_stricmp( s_pFileStaticFuncs[ i ], szName ) == 0 )
         return HB_TRUE;
   return HB_FALSE;
}

static void hb_csAddFileStaticFunc( const char * szName )
{
   if( ! szName || s_iFileStaticFuncCount >= HB_CS_MAX_FILE_STATIC_FUNCS )
      return;
   if( hb_csIsFileStaticFunc( szName ) )
      return;
   s_pFileStaticFuncs[ s_iFileStaticFuncCount++ ] = szName;
}

/* Return the mangled name `<FileBase>_<szName>` in a caller-supplied
   buffer when szName is a STATIC function in this file; otherwise
   return szName unchanged. The mangle uses the declaration-site
   casing so call sites with drifted case (MyDBUSEAREA vs MyDBUseArea
   for one declared STATIC FUNCTION) collapse to a single C# name. */
static const char * hb_csMangleStaticFunc( const char * szName,
                                           char * szBuf, size_t nBufSize )
{
   if( hb_csIsFileStaticFunc( szName ) && s_szFileBase[ 0 ] )
   {
      const char * szCanon = szName;
      int i;
      for( i = 0; i < s_iFileStaticFuncCount; i++ )
         if( hb_stricmp( s_pFileStaticFuncs[ i ], szName ) == 0 )
         {
            szCanon = s_pFileStaticFuncs[ i ];
            break;
         }
      hb_snprintf( szBuf, nBufSize, "%s_%s", s_szFileBase, szCanon );
      return szBuf;
   }
   return szName;
}

/* Report an unsupported-construct warning without failing the file.
   Prints to stderr with a `warning:` prefix that gen-cs.sh's failure
   gate (grep for `Error E[0-9]+`) deliberately doesn't match — we
   want the partial .cs kept so every function/class defined in the
   file is still available to downstream callers. The unsupported
   expression itself lands as a `default` placeholder; executing that
   path at runtime will misbehave, but most of these constructs live
   in rarely-taken branches. */
static void hb_csWarnUnsupported( const char * szDesc )
{
   if( s_pCompCtx )
   {
      /* s_iCurrentStmtLine, NOT s_pCompCtx->currLine: the parser's
         counter is frozen near end-of-file by the time codegen runs,
         so warnings pointed at meaningless lines (kpprt's comma-op
         reported the tail of an unrelated function). */
      fprintf( stderr, "hbtranspiler: %s(%d): warning W0016  Unsupported construct '%s'\n",
               s_pCompCtx->currModule
                  ? hb_strCollapsePath( s_pCompCtx->currModule ) : "?",
               s_iCurrentStmtLine > 0
                  ? s_iCurrentStmtLine : s_pCompCtx->currLine,
               szDesc ? szDesc : "?" );
   }
}

/* Count argument slots in a call-site parameter list. Empty call
   `Foo()` has pParms->asList.pExprList == NULL or a single sentinel
   HB_ET_NONE — both yield 0. Middle gaps in `Foo(a, , c)` count as
   slots (Harbour pads them to NIL); trailing gaps `Foo(a, , , )` are
   dropped to match hb_csEmitCallArgs's truncation so the warning
   uses the same arity the emit does. */
static int hb_csCountCallArgs( PHB_EXPR pParms )
{
   PHB_EXPR pHead;
   PHB_EXPR pItem;
   int      iLastReal = -1;
   int      iPos;

   if( ! pParms )
      return 0;
   if( pParms->ExprType == HB_ET_LIST ||
       pParms->ExprType == HB_ET_ARGLIST ||
       pParms->ExprType == HB_ET_MACROARGLIST )
      pHead = pParms->value.asList.pExprList;
   else
      pHead = pParms;
   for( pItem = pHead, iPos = 0; pItem; pItem = pItem->pNext, iPos++ )
   {
      if( pItem->ExprType != HB_ET_NONE )
         iLastReal = iPos;
   }
   return iLastReal + 1;
}

/* Warn when szFunc is a known user function and the call passes more
   positional args than the declaration takes. Harbour silently drops
   the extras at runtime; C# surfaces them as CS1501 "no overload takes
   N arguments". Flagging at emit-time gives the user a per-call-site
   list in source-file coordinates instead of transpiled-.cs ones.
   Variadic and called-with-spread callees are exempt: both accept an
   arbitrary trailing list. */
static void hb_csWarnExtraArgs( const char * szFunc, PHB_EXPR pParms )
{
   int iDeclared;
   int iPassed;

   if( ! szFunc || ! s_pRefTab )
      return;
   iDeclared = hb_refTabParamCount( s_pRefTab, szFunc );
   if( iDeclared < 0 )
      return;  /* not a known user function */
   if( hb_refTabIsVariadic( s_pRefTab, szFunc ) ||
       hb_refTabIsCalledVarargs( s_pRefTab, szFunc ) )
      return;
   iPassed = hb_csCountCallArgs( pParms );
   if( iPassed > iDeclared && s_pCompCtx )
   {
      fprintf( stderr,
               "hbtranspiler: %s(%d): warning W0018  "
               "Call to '%s' passes %d args but declaration takes %d\n",
               s_pCompCtx->currModule
                  ? hb_strCollapsePath( s_pCompCtx->currModule ) : "?",
               s_iCurrentStmtLine, szFunc, iPassed, iDeclared );
   }
}

/* Warn when szFunc is a known user function with ref parameters and
   the call passes a non-@ argument at a position reftab marks as
   by-ref. Harbour's by-ref convention marker in the declaration
   (the @-prefix-in-a-comment form) is documentation only; the
   caller's `@` is what actually enables mutation, and Harbour
   silently copy-passes when the caller omits it. C# is stricter:
   once any caller uses `@` (putting the param in the reftab's ref
   bitmap, which the emitter renders as `ref T`), every call site
   must pass with `ref`. Non-`@` callers then fail CS1620 "Argument
   N must be passed with the 'ref' keyword". The warning gives the
   user a per-call-site punch list keyed on the original .prg
   coordinates; fixing either the declaration (drop the convention
   marker) or adding `@` at the call site clears the C# error. */
static void hb_csWarnMissingRef( const char * szFunc, PHB_EXPR pParms )
{
   int iDeclared;
   PHB_EXPR pHead;
   PHB_EXPR pItem;
   int iPos = 0;
   int iFirstMissing = -1;   /* earliest ref slot passed without @ */

   if( ! szFunc || ! s_pRefTab || ! pParms )
      return;
   iDeclared = hb_refTabParamCount( s_pRefTab, szFunc );
   if( iDeclared <= 0 )
      return;
   if( hb_refTabIsVariadic( s_pRefTab, szFunc ) ||
       hb_refTabIsCalledVarargs( s_pRefTab, szFunc ) )
      return;  /* spread callees don't have positional ref slots */

   if( pParms->ExprType == HB_ET_LIST ||
       pParms->ExprType == HB_ET_ARGLIST ||
       pParms->ExprType == HB_ET_MACROARGLIST )
      pHead = pParms->value.asList.pExprList;
   else
      pHead = pParms;

   for( pItem = pHead; pItem; pItem = pItem->pNext, iPos++ )
   {
      if( pItem->ExprType == HB_ET_NONE )
         continue;
      if( iPos >= iDeclared )
         break;
      if( ! hb_refTabIsRef( s_pRefTab, szFunc, iPos ) )
         continue;
      /* Caller wrote `@var` when the arg is HB_ET_VARREF; an lvalue
         that decays to by-ref arrives as HB_ET_REFERENCE. Either
         satisfies the ref requirement. */
      if( pItem->ExprType == HB_ET_VARREF ||
          pItem->ExprType == HB_ET_REFERENCE )
         continue;
      /* Only warn when `@` was POSSIBLE: a bare variable could have
         been passed by-ref and wasn't — the lost-result shape worth
         flagging (SockClose(pSock) leaving a dangling handle). A
         literal or computed expression (`Tax(..., 0)`) cannot take
         `@` at all — that is the intentional-discard idiom for an
         optional out-parameter, not a bug. */
      if( pItem->ExprType != HB_ET_VARIABLE )
         continue;
      /* A call-site `@` by-value marker on this argument (the mirror of
         the declaration-site out-param marker) says the by-value pass is
         deliberate — honour it and stay silent. */
      if( pItem->value.asSymbol.flags & HB_EXPRFLAG_BYREFSKIP )
         continue;
      if( iFirstMissing < 0 )
         iFirstMissing = iPos;
   }

   /* One warning per call site — the first missing position is
      enough to locate the bug; chasing every offending position in
      the same call would bloat the log without telling the user
      anything new (fixing one usually means re-checking the rest
      of the call anyway). */
   if( iFirstMissing >= 0 && s_pCompCtx )
      fprintf( stderr,
               "hbtranspiler: %s(%d): warning W0020  "
               "Call to '%s' omits '@' on ref parameter #%d\n",
               s_pCompCtx->currModule
                  ? hb_strCollapsePath( s_pCompCtx->currModule ) : "?",
               s_iCurrentStmtLine, szFunc, iFirstMissing + 1 );
}

/* Callback used by hb_refTabForEachPublic — emits one field line per
   PUBLIC variable whose owner matches this .prg. Sized-array forms
   (`PUBLIC name[size]`) emit as `dynamic[]` so cross-file callers can
   pass them by-ref to callees declared `ref dynamic[]` (the typical
   shape for flag-table mutators like LoadAFlag). The runtime
   allocation (`new dynamic[N]`) still happens at the source's PUBLIC
   statement — see HB_AST_PUBLIC emission. */
static void hb_csEmitPublicField( const char * szName, HB_BOOL fArrayDim,
                                   void * userdata )
{
   FILE * fp = *( FILE ** ) userdata;
   fprintf( fp, "    public static dynamic%s %s;\n",
            fArrayDim ? "[]" : "", szName );
}

static HB_BOOL hb_csIsFileMemvar( const char * szName )
{
   int i;
   if( ! szName || s_iFileMemvarCount == 0 )
      return HB_FALSE;
   for( i = 0; i < s_iFileMemvarCount; i++ )
      if( hb_stricmp( s_pFileMemvars[ i ], szName ) == 0 )
         return HB_TRUE;
   return HB_FALSE;
}

/* Declaration-site casing for a file-scope MEMVAR — Harbour names are
   case-insensitive, C# isn't, so drifted-case references collapse
   onto the declared spelling (same rationale as the STATIC registry's
   hb_csStaticFieldName). */
static const char * hb_csFileMemvarCanon( const char * szName )
{
   int i;
   if( ! szName )
      return szName;
   for( i = 0; i < s_iFileMemvarCount; i++ )
      if( hb_stricmp( s_pFileMemvars[ i ], szName ) == 0 )
         return s_pFileMemvars[ i ];
   return szName;
}

static void hb_csAddFileMemvar( const char * szName )
{
   if( ! szName || s_iFileMemvarCount >= HB_CS_MAX_FILE_MEMVARS )
      return;
   if( hb_csIsFileMemvar( szName ) )
      return;
   s_pFileMemvars[ s_iFileMemvarCount++ ] = szName;
}

static void hb_csResetFileStatics( void )
{
   s_iFileStaticCount = 0;
   s_szStaticScope = NULL;
   s_pFileDeclFunc = NULL;
   s_iFileStaticFuncCount = 0;
   s_iFileMemvarCount = 0;
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

/* True if szName is declared as a method-level local (HB_AST_LOCAL) in
   the current function body. Used by HB_AST_FOREACH emit to detect the
   shadow case where a loop variable reuses an already-declared local —
   in which case the foreach must rename its inner iterator and assign
   to the outer. The function body is an HB_AST_BLOCK wrapping the
   statement list via pFirst. */
static HB_BOOL hb_csIsMethodLocal( const char * szName )
{
   PHB_AST_NODE pNode;
   if( ! szName || ! s_pCurrentFuncNode )
      return HB_FALSE;
   pNode = s_pCurrentFuncNode->value.asFunc.pBody;
   if( pNode && pNode->type == HB_AST_BLOCK )
      pNode = pNode->value.asBlock.pFirst;
   while( pNode )
   {
      if( pNode->type == HB_AST_LOCAL &&
          pNode->value.asVar.szName &&
          hb_stricmp( pNode->value.asVar.szName, szName ) == 0 )
         return HB_TRUE;
      pNode = pNode->pNext;
   }
   return HB_FALSE;
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

/* Harbour builtins that may reallocate their first array argument.
   The HbRuntime overload takes `ref dynamic[]` so the new array
   propagates back; the emitter inserts `ref` at the call site. AIns,
   ADel, AFill, ASort etc. are size-stable and don't need it. */
static HB_BOOL hb_csIsArrayMutator( const char * szFunc )
{
   if( ! szFunc )
      return HB_FALSE;
   return hb_stricmp( szFunc, "ASize" ) == 0 ||
          hb_stricmp( szFunc, "AAdd"  ) == 0;
}

/* True when szMember is a CLASS VAR (CLASSDATA — a static, class-level
   member) of class szClass. A `Self:` access to such a member must
   emit `Class.member`, not `this.member`: the field is emitted static
   and C# rejects an instance reference to it (CS0176). Only the
   directly-named class is searched — inherited class vars (rare) fall
   through to the plain `this.` emit. */
static HB_BOOL hb_csIsClassVar( const char * szClass, const char * szMember )
{
   PHB_AST_NODE pStmt;

   if( ! szClass || ! *szClass || ! szMember )
      return HB_FALSE;

   for( pStmt = s_pClassList; pStmt; pStmt = pStmt->pNext )
   {
      if( pStmt->type == HB_AST_CLASS &&
          pStmt->value.asClass.szName &&
          hb_stricmp( pStmt->value.asClass.szName, szClass ) == 0 )
      {
         PHB_AST_NODE pMember;
         for( pMember = pStmt->value.asClass.pMembers; pMember;
              pMember = pMember->pNext )
         {
            if( pMember->type == HB_AST_CLASSDATA &&
                pMember->value.asClassData.iKind == HB_AST_DATA_CLASS &&
                pMember->value.asClassData.szName &&
                hb_stricmp( pMember->value.asClassData.szName, szMember ) == 0 )
               return HB_TRUE;
         }
         return HB_FALSE;
      }
   }
   return HB_FALSE;
}

/* True when the CURRENT class declares szMember with a type mapping
   to C# int (source `as int`). Writes into such members need the
   (int) coercion, mirroring int-typed locals. */
static HB_BOOL hb_csMemberIsInteger( const char * szMember )
{
   PHB_AST_NODE pStmt;

   if( ! s_szCurrentClass[ 0 ] || ! szMember )
      return HB_FALSE;

   for( pStmt = s_pClassList; pStmt; pStmt = pStmt->pNext )
   {
      if( pStmt->type == HB_AST_CLASS &&
          pStmt->value.asClass.szName &&
          hb_stricmp( pStmt->value.asClass.szName, s_szCurrentClass ) == 0 )
      {
         PHB_AST_NODE pMember;
         for( pMember = pStmt->value.asClass.pMembers; pMember;
              pMember = pMember->pNext )
         {
            if( pMember->type == HB_AST_CLASSDATA &&
                pMember->value.asClassData.szName &&
                hb_stricmp( pMember->value.asClassData.szName,
                            szMember ) == 0 )
               return pMember->value.asClassData.szType &&
                      hb_stricmp( hb_csTypeMap(
                         pMember->value.asClassData.szType ), "long" ) == 0;
         }
         return HB_FALSE;
      }
   }
   return HB_FALSE;
}

/* True when szName is any declared member of class szClass — a DATA
   member of any kind (VAR / CLASS VAR / ACCESS / ASSIGN) or a METHOD —
   walking the parent chain so inherited members count as declared. A
   `Self:` access to a name not declared anywhere in the hierarchy is
   either a dynamic class's dictionary-backed member or a name Harbour
   resolves at runtime; the caller routes it through `((dynamic)this)`
   so the DLR handles it rather than a missing static field surfacing
   as CS1061. A class whose parent is outside this unit stops the walk
   early (returns false) — routing through the DLR still works at
   runtime, it just forgoes the static dispatch. */
static HB_BOOL hb_csIsDeclaredMember( const char * szClass, const char * szName )
{
   int iGuard = 0;   /* bound the walk against accidental inherit cycles */

   if( ! szClass || ! *szClass || ! szName )
      return HB_FALSE;

   while( szClass && *szClass && iGuard++ < 64 )
   {
      PHB_AST_NODE pStmt;
      const char * szParent = NULL;
      HB_BOOL fFoundClass = HB_FALSE;

      for( pStmt = s_pClassList; pStmt; pStmt = pStmt->pNext )
      {
         if( pStmt->type == HB_AST_CLASS &&
             pStmt->value.asClass.szName &&
             hb_stricmp( pStmt->value.asClass.szName, szClass ) == 0 )
         {
            PHB_AST_NODE pMember;
            for( pMember = pStmt->value.asClass.pMembers; pMember;
                 pMember = pMember->pNext )
            {
               const char * szMember = NULL;
               if( pMember->type == HB_AST_CLASSDATA )
                  szMember = pMember->value.asClassData.szName;
               else if( pMember->type == HB_AST_CLASSMETHOD )
                  szMember = pMember->value.asClassMethod.szName;
               if( szMember && hb_stricmp( szMember, szName ) == 0 )
                  return HB_TRUE;
            }
            szParent    = pStmt->value.asClass.szParent;
            fFoundClass = HB_TRUE;
            break;
         }
      }

      if( ! fFoundClass )
         break;             /* class or an ancestor not in this unit */
      szClass = szParent;   /* ascend to the parent and repeat */
   }
   return HB_FALSE;
}

/* Built-in OO helpers (className / ClassName / Super) are emitted as
   extension methods on `object` (HbObjectExtensions). The DLR does not
   resolve extension methods, so a `Self:` call to one must stay `this.`
   (compile-time bound); routing it through `((dynamic)this)` would
   throw RuntimeBinderException at runtime. They are never declared
   members, so the undeclared-member check would otherwise divert them. */
static HB_BOOL hb_csIsBuiltinObjMsg( const char * szMsg )
{
   return ( HB_BOOL ) ( szMsg &&
          ( hb_stricmp( szMsg, "className" ) == 0 ||
            hb_stricmp( szMsg, "Super"     ) == 0 ) );
}

/* ---- ref-shim for USUAL-ref parameters ----

   C# `ref` is invariant: a `ref decimal` argument cannot bind to a
   `ref dynamic` parameter. Harbour's polymorphic by-ref params (an
   x-prefixed slot marked by-ref) emit as `ref dynamic`, yet callers
   routinely pass a typed lvalue by reference: LoadStaticField(row,
   @oStatus:nConsec), POSBrowse(..., @nSelection). The fix is a
   per-call shim: copy the typed lvalue into a `dynamic` temp, pass
   the temp by ref, copy it back. Only viable in statement context,
   where the temp and copies can live in a brace block (see the
   HB_AST_EXPRSTMT handler). */

#define HB_CS_MAXSHIM 64

/* When non-NULL during a funcall emit, s_aRefShim[iPos] != 0 means
   argument iPos must be emitted as `ref _hbref<base>_<iPos>` — the temp
   was already declared by the enclosing brace block, named with the
   base id in s_iRefShimBase. Consumed (and cleared) by hb_csEmitCallArgs
   so nested calls don't inherit it. */
static const HB_BOOL * s_aRefShim     = NULL;
static int             s_iRefShimBase = 0;   /* name base of the call being emitted */
/* Shim-block nesting depth, used as the `_hbref<base>` / `_hbcall<base>`
   id: incremented on entering a shim/hoist block and decremented on
   leaving, so a nested block gets a higher base than its enclosing one
   (no CS0136) while sequential sibling blocks reuse the same low number.
   Returns to 0 between top-level statements; reset per function for
   safety. */
static int             s_iShimDepth   = 0;

/* Hoisting: a funcall lifted out of a condition / return expression into
   a preceding statement. While s_pHoistCall is non-NULL, hb_csEmitExpr
   emits s_szHoistVar in place of that exact funcall node, so the original
   expression reads the already-computed result. */
static PHB_EXPR s_pHoistCall = NULL;
static char     s_szHoistVar[ 96 ] = "";

/* Look up parameter iPos of a called function in the reftab. A
   file-scoped STATIC function is keyed <FileBase>::<Name> there (the
   scan pass registers it that way), but a call site only carries the
   bare name — a plain hb_refTabParam(szFunc) misses, and the caller
   then loses the parameter types / names. Reftab lookups are
   case-insensitive, so s_szFileBase's casing doesn't matter. */
static const HB_REFPARAM * hb_csCallParam( const char * szFunc, int iPos )
{
   if( ! szFunc || ! s_pRefTab )
      return NULL;
   if( hb_csIsFileStaticFunc( szFunc ) && s_szFileBase[ 0 ] )
   {
      char szKey[ 256 ];
      const HB_REFPARAM * pP;
      hb_snprintf( szKey, sizeof( szKey ), "%s::%s", s_szFileBase, szFunc );
      pP = hb_refTabParam( s_pRefTab, szKey, iPos );
      if( pP )
         return pP;
   }
   return hb_refTabParam( s_pRefTab, szFunc, iPos );
}

/* True when parameter iPos of szFunc is a by-ref ARRAY slot the callee
   never reassigns. C# arrays are reference types, so element mutation
   propagates without `ref`; the `ref` is only needed when the variable is
   repointed (`p := ...`). Such a slot is emitted as a plain `dynamic[]`
   parameter, and call sites pass it without `ref` and without a shim. */
static HB_BOOL hb_csParamElidesArrayRef( const char * szFunc, int iPos )
{
   const HB_REFPARAM * pP = hb_csCallParam( szFunc, iPos );
   return pP && pP->fByRef && ! pP->fReassigned &&
          pP->szType && hb_stricmp( pP->szType, "ARRAY" ) == 0;
}

/* Whether parameter iPos of szFunc is actually emitted as `ref` — the
   reftab by-ref flag minus the array slots elided to a plain dynamic[].
   The short-overload generator keys off this so it doesn't manufacture a
   `ref _argN` against a parameter that emits plain. */
static HB_BOOL hb_csParamEmitsRef( const char * szFunc, int iPos )
{
   return s_pRefTab && hb_refTabIsRef( s_pRefTab, szFunc, iPos ) &&
          ! hb_csParamElidesArrayRef( szFunc, iPos );
}

/* Warn when a call passes an array by-ref (`@aArr`) to a parameter the
   callee never reassigns: the `@` is redundant (C# arrays are reference
   types, so element mutation already propagates), and the slot is emitted
   as a plain `dynamic[]`. One warning per call site. */
static void hb_csWarnArrayRefElided( const char * szFunc, PHB_EXPR pParms )
{
   PHB_EXPR pHead, pItem;
   int iPos = 0, iFirst = -1;

   if( ! szFunc || ! s_pRefTab || ! pParms )
      return;
   if( pParms->ExprType == HB_ET_LIST ||
       pParms->ExprType == HB_ET_ARGLIST ||
       pParms->ExprType == HB_ET_MACROARGLIST )
      pHead = pParms->value.asList.pExprList;
   else
      pHead = pParms;

   for( pItem = pHead; pItem; pItem = pItem->pNext, iPos++ )
   {
      if( pItem->ExprType != HB_ET_VARREF &&
          pItem->ExprType != HB_ET_REFERENCE )
         continue;
      if( iFirst < 0 && hb_csParamElidesArrayRef( szFunc, iPos ) )
         iFirst = iPos;
   }

   if( iFirst >= 0 && s_pCompCtx )
      fprintf( stderr,
               "hbtranspiler: %s(%d): warning W0023  "
               "'@' on array parameter #%d of '%s' is redundant — "
               "the callee never reassigns it\n",
               s_pCompCtx->currModule
                  ? hb_strCollapsePath( s_pCompCtx->currModule ) : "?",
               s_iCurrentStmtLine, iFirst + 1, szFunc );
}

/* The reftab key for a function being emitted. A file-scoped STATIC
   is keyed <FileBase>::<Name>; emitting it under the bare name would
   read a same-named global function's entry instead — wrong param
   types, ref flags and arity. szBuf must hold the composed key. */
static const char * hb_csFuncRefKey( const char * szName,
                                     char * szBuf, HB_SIZE nBuf )
{
   if( szName && hb_csIsFileStaticFunc( szName ) && s_szFileBase[ 0 ] )
   {
      hb_snprintf( szBuf, nBuf, "%s::%s", s_szFileBase, szName );
      return szBuf;
   }
   return szName;
}

/* Fill pfShim[0..iMax) for each @arg slot of pCall that needs a ref-shim,
   returning the count. C# `ref` is invariant, so a `ref dynamic` argument
   can't bind a typed `ref` parameter (or vice versa), and a property /
   dynamic member / array element isn't a ref-able storage location at all.
   Both are fixed by copying the lvalue into a temp typed as the parameter,
   passing the temp, and copying it back.

   A field/element (HB_ET_REFERENCE) always needs the temp. A plain @var
   (HB_ET_VARREF) binds the parameter directly when its inferred type
   already matches, so it is shimmed only on a real mismatch — shimming a
   matching @var would needlessly wrap (and, inside a condition, hoist) a
   call that compiles fine. */
static int hb_csCollectRefShims( PHB_EXPR pCall, HB_BOOL * pfShim, int iMax )
{
   const char * szFunc = NULL;
   PHB_EXPR pHead, pItem;
   int iPos, iCount = 0;

   for( iPos = 0; iPos < iMax; iPos++ )
      pfShim[ iPos ] = HB_FALSE;

   if( ! pCall || pCall->ExprType != HB_ET_FUNCALL || ! s_pRefTab )
      return 0;
   if( pCall->value.asFunCall.pFunName &&
       pCall->value.asFunCall.pFunName->ExprType == HB_ET_FUNNAME )
      szFunc = pCall->value.asFunCall.pFunName->value.asSymbol.name;
   if( ! szFunc )
      return 0;

   pHead = pCall->value.asFunCall.pParms;
   if( pHead && ( pHead->ExprType == HB_ET_LIST ||
                  pHead->ExprType == HB_ET_ARGLIST ||
                  pHead->ExprType == HB_ET_MACROARGLIST ) )
      pHead = pHead->value.asList.pExprList;

   for( pItem = pHead, iPos = 0; pItem && iPos < iMax;
        pItem = pItem->pNext, iPos++ )
   {
      const HB_REFPARAM * pP;
      if( pItem->ExprType == HB_ET_NONE )
         continue;   /* omitted slot — padded by hb_csEmitCallArgs */
      pP = hb_csCallParam( szFunc, iPos );
      if( ! pP || ! pP->fByRef )
         continue;
      /* By-ref array slot the callee never reassigns: emitted as a plain
         `dynamic[]` param, so no `ref` and no shim — element mutation
         flows through the shared reference. */
      if( hb_csParamElidesArrayRef( szFunc, iPos ) )
         continue;
      /* A plain variable (HB_ET_VARIABLE or HB_ET_VARREF) binds via
         `ref var` directly when its type already equals the parameter's,
         so no shim — otherwise we'd wrap (and hoist) calls that compile
         fine. Everything else — field access, array element, literal,
         expression — is not a ref-able C# storage location and needs a
         temp. hb_csEmitShimWriteback then copies back only for the @
         shapes (HB_ET_VARREF / HB_ET_REFERENCE); a non-@ arg means the
         author wanted value semantics (Harbour treats the parameter as
         a local copy at the call site), so we skip the writeback. */
      if( pItem->ExprType == HB_ET_VARREF ||
          pItem->ExprType == HB_ET_VARIABLE )
      {
         const char * szArgName = pItem->value.asSymbol.name;
         char szBuf[ 96 ];
         const char * szParamCs = hb_csShimSlotType( pP, szBuf, sizeof( szBuf ) );
         const char * szArgCs =
            hb_csTypeMap( hb_csArgVarType( szArgName ) );
         if( strcmp( szParamCs, szArgCs ) == 0 )
            continue;   /* `ref var` binds the parameter directly */
      }
      pfShim[ iPos ] = HB_TRUE;
      iCount++;
   }
   return iCount;
}

/* Emit the lvalue under an @arg item — HB_ET_VARREF wraps a plain
   variable, HB_ET_REFERENCE a field access or array element. For non-@
   arg shapes (HB_ET_VARIABLE, HB_ET_SEND, HB_ET_ARRAYAT, literal /
   expression) just emit the expression directly: the shim temp's
   initializer reads from it, and (for the writeback path) the same
   expression on the LHS is a writable lvalue for VARIABLE / SEND /
   ARRAYAT shapes. hb_csEmitShimWriteback guards against writing to
   literals or other non-lvalues. */
static void hb_csEmitRefTarget( PHB_EXPR pItem, FILE * yyc )
{
   if( pItem->ExprType == HB_ET_REFERENCE )
      hb_csEmitExpr( pItem->value.asReference, yyc, HB_FALSE );
   else  /* HB_ET_VARREF, or any non-@ shim shape */
      hb_csEmitExpr( pItem, yyc, HB_FALSE );
}

/* True if a non-@ shimmed arg has a writable lvalue we should copy the
   shim temp back into. Variables and class DATA / member accesses qualify;
   literals and arbitrary expressions don't. (@ shapes always writeback
   in the existing path — that's the whole point of `@`.) */
static HB_BOOL hb_csShimWritesBack( PHB_EXPR pItem )
{
   if( ! pItem )
      return HB_FALSE;
   switch( pItem->ExprType )
   {
      /* @-marked shapes: caller explicitly asked for ref → writeback. */
      case HB_ET_VARREF:
      case HB_ET_REFERENCE:
         return HB_TRUE;
      /* Non-@ shapes: caller wanted value semantics. Skip writeback. */
      default:
         return HB_FALSE;
   }
}

/* The C# type a shim temp must take to bind parameter pP by ref: exactly
   the parameter's emitted type, so `ref temp` is invariant-compatible.
   USUAL / untyped → dynamic. A nilable typed slot keeps its `?`. */
static const char * hb_csShimSlotType( const HB_REFPARAM * pP, char * szBuf,
                                       HB_SIZE nBuf )
{
   const char * szSlot = NULL;
   const char * szCs;
   if( pP && pP->szType && hb_stricmp( pP->szType, "USUAL" ) != 0 )
      szSlot = pP->szType;
   szCs = hb_csTypeMap( szSlot );
   if( pP && pP->fNilable && strcmp( szCs, "dynamic" ) != 0 )
   {
      hb_snprintf( szBuf, nBuf, "%s?", szCs );
      return szBuf;
   }
   return szCs;
}

/* Name the shim temp that backs a by-ref @arg. A plain @var becomes
   `_hbref_<var>` (the variable name reads cleanly and is unique within a
   call); a field / array element becomes `_hbref_<leaf>_<pos>` (the leaf
   identifier can repeat, so the arg position keeps it unique). A nested
   shim block (iDepth > 0) prefixes the depth so it can't shadow an
   enclosing block's temp (CS0136). Deterministic in (pItem, iDepth, iPos)
   so the temp declaration, the `ref` argument and the write-back all
   compute the same name without shared state. szBuf must hold it. */
static const char * hb_csShimTempName( PHB_EXPR pItem, int iDepth, int iPos,
                                       char * szBuf, HB_SIZE nBuf )
{
   const char * szLeaf = NULL;
   HB_BOOL fRef = HB_FALSE;
   char szPfx[ 16 ];

   if( pItem->ExprType == HB_ET_VARREF )
      szLeaf = pItem->value.asSymbol.name;
   else if( pItem->ExprType == HB_ET_VARIABLE )
   {
      /* Non-@ shim: caller passed the var without @ but the param is by-ref
         and the types disagree, so we shim the value. Position-disambiguate
         to avoid colliding with a sibling @var at a different slot. */
      szLeaf = pItem->value.asSymbol.name;
      fRef = HB_TRUE;
   }
   else if( pItem->ExprType == HB_ET_REFERENCE )
   {
      PHB_EXPR pInner = pItem->value.asReference;
      fRef = HB_TRUE;
      if( pInner )
      {
         if( pInner->ExprType == HB_ET_SEND &&
             pInner->value.asMessage.szMessage )
            szLeaf = pInner->value.asMessage.szMessage;       /* obj:member */
         else if( pInner->ExprType == HB_ET_VARIABLE )
            szLeaf = pInner->value.asSymbol.name;
         else if( pInner->ExprType == HB_ET_ARRAYAT &&
                  pInner->value.asList.pExprList &&
                  pInner->value.asList.pExprList->ExprType == HB_ET_VARIABLE )
            szLeaf = pInner->value.asList.pExprList->value.asSymbol.name; /* arr[i] */
      }
   }
   else if( pItem->ExprType == HB_ET_SEND &&
            pItem->value.asMessage.szMessage )
      szLeaf = pItem->value.asMessage.szMessage, fRef = HB_TRUE;
   else if( pItem->ExprType == HB_ET_ARRAYAT &&
            pItem->value.asList.pExprList &&
            pItem->value.asList.pExprList->ExprType == HB_ET_VARIABLE )
      szLeaf = pItem->value.asList.pExprList->value.asSymbol.name, fRef = HB_TRUE;
   if( ! szLeaf || ! szLeaf[ 0 ] )
   {
      szLeaf = "arg";
      fRef   = HB_TRUE;   /* anonymous: force the pos suffix */
   }

   if( iDepth > 0 )
      hb_snprintf( szPfx, sizeof( szPfx ), "_hbref%d_", iDepth );
   else
      hb_strncpy( szPfx, "_hbref_", sizeof( szPfx ) - 1 );

   if( fRef )
      hb_snprintf( szBuf, nBuf, "%s%s_%d", szPfx, szLeaf, iPos );
   else
      hb_snprintf( szBuf, nBuf, "%s%s", szPfx, szLeaf );
   return szBuf;
}

/* Declare a `<paramtype> <shimname> = <lvalue>;` temp for each shimmed
   arg (see hb_csShimTempName for the naming, which encodes the nesting
   depth iBase so a nested block can't shadow an enclosing one). */
static void hb_csEmitShimTemps( const char * szFunc, PHB_EXPR pHead,
                                const HB_BOOL * aShim, int iBase,
                                FILE * yyc, int iIndent )
{
   PHB_EXPR pArg;
   int iArg;
   for( pArg = pHead, iArg = 0; pArg; pArg = pArg->pNext, iArg++ )
   {
      if( iArg < HB_CS_MAXSHIM && aShim[ iArg ] )
      {
         char szType[ 96 ], szName[ 96 ];
         const HB_REFPARAM * pP = hb_csCallParam( szFunc, iArg );
         hb_csEmitIndent( yyc, iIndent );
         fprintf( yyc, "%s %s = ",
                  hb_csShimSlotType( pP, szType, sizeof( szType ) ),
                  hb_csShimTempName( pArg, iBase, iArg, szName, sizeof( szName ) ) );
         hb_csEmitRefTarget( pArg, yyc );
         fprintf( yyc, ";\n" );
      }
   }
}

/* Copy each shim temp back into its lvalue after the call. The @ shapes
   (HB_ET_VARREF / HB_ET_REFERENCE) always writeback; non-@ shapes
   (HB_ET_VARIABLE, HB_ET_SEND, HB_ET_ARRAYAT, literal, expression) get
   value semantics — the caller didn't mark the slot @, so they treated
   the parameter as a local copy in Harbour and the writeback would mutate
   state the author expected to stay put. hb_csShimWritesBack draws the
   line. */
static void hb_csEmitShimWriteback( PHB_EXPR pHead, const HB_BOOL * aShim,
                                    int iBase, FILE * yyc, int iIndent )
{
   PHB_EXPR pArg;
   int iArg;
   for( pArg = pHead, iArg = 0; pArg; pArg = pArg->pNext, iArg++ )
   {
      if( iArg < HB_CS_MAXSHIM && aShim[ iArg ] &&
          hb_csShimWritesBack( pArg ) )
      {
         char szName[ 96 ];
         hb_csEmitIndent( yyc, iIndent );
         hb_csEmitRefTarget( pArg, yyc );
         fprintf( yyc, " = %s;\n",
                  hb_csShimTempName( pArg, iBase, iArg, szName, sizeof( szName ) ) );
      }
   }
}

/* The arg list head of a funcall, unwrapped from its LIST node. */
static PHB_EXPR hb_csFunCallArgHead( PHB_EXPR pCall )
{
   PHB_EXPR pHead = pCall->value.asFunCall.pParms;
   if( pHead && ( pHead->ExprType == HB_ET_LIST ||
                  pHead->ExprType == HB_ET_ARGLIST ||
                  pHead->ExprType == HB_ET_MACROARGLIST ) )
      pHead = pHead->value.asList.pExprList;
   return pHead;
}

/* Find the first funcall within pExpr whose by-ref args need a shim. Used
   to hoist a ref-passing call out of a condition / return expression,
   where the brace-block shim can't be emitted in place. Descends through
   operators, iif and argument lists; depth-limited. */
static PHB_EXPR hb_csFindShimCall( PHB_EXPR pExpr, int iDepth )
{
   if( ! pExpr || iDepth > 32 )
      return NULL;
   if( pExpr->ExprType == HB_ET_FUNCALL )
   {
      HB_BOOL aShim[ HB_CS_MAXSHIM ];
      if( pExpr->value.asFunCall.pFunName &&
          pExpr->value.asFunCall.pFunName->ExprType == HB_ET_FUNNAME &&
          hb_csCollectRefShims( pExpr, aShim, HB_CS_MAXSHIM ) > 0 )
         return pExpr;
      return hb_csFindShimCall( hb_csFunCallArgHead( pExpr ), iDepth + 1 );
   }
   switch( pExpr->ExprType )
   {
      case HB_ET_LIST: case HB_ET_ARGLIST: case HB_ET_MACROARGLIST:
      case HB_ET_IIF:
      {
         PHB_EXPR p = pExpr->value.asList.pExprList;
         while( p )
         {
            PHB_EXPR pHit = hb_csFindShimCall( p, iDepth + 1 );
            if( pHit )
               return pHit;
            p = p->pNext;
         }
         break;
      }
      default:
         if( pExpr->ExprType >= HB_EO_ASSIGN && pExpr->ExprType <= HB_EO_PREDEC )
         {
            PHB_EXPR pHit =
               hb_csFindShimCall( pExpr->value.asOperator.pLeft, iDepth + 1 );
            if( pHit )
               return pHit;
            return hb_csFindShimCall( pExpr->value.asOperator.pRight,
                                      iDepth + 1 );
         }
         break;
   }
   return NULL;
}

/* Hoist pCall: emit its shim temps, `var _hbcall<base> = <call>;` and the
   write-backs at iIndent, then arm s_pHoistCall so the surrounding
   expression substitutes that temp for the call. The caller has already
   opened the enclosing brace; it must clear s_pHoistCall once it has
   emitted that expression, close the brace, and call hb_csEndShimBlock()
   to pop the depth this pushed (the brace — and the if/return body in it
   — stays at this depth so nested shims get a higher base). */
static void hb_csBeginHoist( PHB_EXPR pCall, FILE * yyc, int iIndent )
{
   HB_BOOL aShim[ HB_CS_MAXSHIM ];
   const char * szFunc = pCall->value.asFunCall.pFunName->value.asSymbol.name;
   PHB_EXPR pHead = hb_csFunCallArgHead( pCall );
   int iBase = s_iShimDepth++;

   hb_csCollectRefShims( pCall, aShim, HB_CS_MAXSHIM );
   hb_csEmitShimTemps( szFunc, pHead, aShim, iBase, yyc, iIndent );

   hb_csEmitIndent( yyc, iIndent );
   if( iBase > 0 )
      hb_snprintf( s_szHoistVar, sizeof( s_szHoistVar ), "_hbcall%d_%s",
                   iBase, szFunc );
   else
      hb_snprintf( s_szHoistVar, sizeof( s_szHoistVar ), "_hbcall_%s", szFunc );
   fprintf( yyc, "var %s = ", s_szHoistVar );
   s_aRefShim   = aShim;
   s_iRefShimBase = iBase;
   s_pHoistCall = NULL;   /* emit the call itself, not the placeholder */
   hb_csEmitExpr( pCall, yyc, HB_FALSE );
   fprintf( yyc, ";\n" );
   hb_csEmitShimWriteback( pHead, aShim, iBase, yyc, iIndent );
   s_pHoistCall = pCall;  /* substitute in the surrounding expression */
}

/* Pop the depth pushed by hb_csBeginHoist or an inline shim block. */
static void hb_csEndShimBlock( void )
{
   if( s_iShimDepth > 0 )
      s_iShimDepth--;
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
/* Resolve a `obj:Method(...)` SEND to the reftab key whose by-ref
   signature the arguments should match — the canonical method key
   `<Class>::<Class>__<Method>` when the receiver is a registered
   class, else NULL. NULL means "no signature": a dynamic/untypeable
   receiver dispatches dynamically in C# (no `ref` possible), and —
   crucially — we must NOT let the bare method name fall through to
   hb_csCallParam's file-static resolution, which would bind a proxy's
   `:TransactionHistory(aArr)` to a same-file static
   `TransactionHistory(cWiTrxId-byref, ...)` and mis-`ref` the args. */
static const char * hb_csSendRefKey( PHB_EXPR pObj, const char * szMethod,
                                     char * szBuf, HB_SIZE nBuf )
{
   const char * szClass = NULL;

   if( ! szMethod )
      return NULL;

   if( ! pObj )
      szClass = s_szCurrentClass[ 0 ] ? s_szCurrentClass : NULL;
   else if( pObj->ExprType == HB_ET_VARIABLE )
   {
      if( hb_stricmp( pObj->value.asSymbol.name, "Self" ) == 0 )
         szClass = s_szCurrentClass[ 0 ] ? s_szCurrentClass : NULL;
      else
      {
         const char * szT = hb_csArgVarType( pObj->value.asSymbol.name );
         if( szT && s_pRefTab && hb_refTabIsClass( s_pRefTab, szT ) )
            szClass = szT;
      }
   }
   else if( pObj->ExprType == HB_ET_SEND &&
            pObj->value.asMessage.szMessage )
   {
      const char * szT = hb_csArgVarType( pObj->value.asMessage.szMessage );
      if( szT && s_pRefTab && hb_refTabIsClass( s_pRefTab, szT ) )
         szClass = szT;
   }

   if( szClass )
   {
      hb_snprintf( szBuf, nBuf, "%s::%s__%s", szClass, szClass, szMethod );
      return szBuf;
   }
   /* Unresolved receiver: return an empty sentinel, NOT the bare method
      name — hb_csEmitCallArgs treats "" as "no known signature" and
      emits every arg plainly. (A NULL szFunc would make hb_csCallParam
      fall back too, but "" is explicit and can't be confused with a
      real name.) */
   szBuf[ 0 ] = '\0';
   return szBuf;
}

static void hb_csEmitCallArgs( const char * szFunc, PHB_EXPR pParms, FILE * yyc )
{
   PHB_EXPR pHead;
   PHB_EXPR pItem;
   int      nLastReal = -1;
   int      iPos;
   HB_BOOL  fFirst = HB_TRUE;
   HB_BOOL  fNamed = HB_FALSE;
   /* Consume the ref-shim map set by the enclosing brace block, then
      clear it so nested funcall args don't inherit it. iShimBase names
      the temps this call's shimmed slots refer to. */
   const HB_BOOL * aShim = s_aRefShim;
   int            iShimBase = s_iRefShimBase;
   s_aRefShim = NULL;

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

   /* Pass 2: emit each slot. C# can't omit a `ref` param, and a
      by-value param sitting before a ref isn't given a `= default`, so
      for a callee with any by-ref param we PAD every omitted slot up
      to the last ref instead of dropping it (CS7036) or emitting a
      named arg that skips it: an omitted ref becomes
      `ref HbDiscard<T>.Value` (a shared throwaway whose write-back the
      Harbour caller discarded anyway), an omitted by-value becomes
      `default`. Slots past the last ref are defaulted in the canonical
      so trailing omissions there are still dropped. A non-ref callee
      keeps the original behaviour: a gap flips later real slots into
      named-arg form. */
   {
      int iLastRef = -1;
      int iEnd, j;
      const HB_REFPARAM * pRP;
      for( j = 0; ( pRP = hb_csCallParam( szFunc, j ) ) != NULL; j++ )
         if( pRP->fByRef )
            iLastRef = j;
      iEnd = ( iLastRef > nLastReal ) ? iLastRef : nLastReal;

      pItem = pHead;
      for( iPos = 0; iPos <= iEnd; iPos++ )
      {
         PHB_EXPR pArg = pItem;   /* NULL once past the source arg list */

         if( ! pArg || pArg->ExprType == HB_ET_NONE )
         {
            /* Omitted slot. */
            if( iLastRef >= 0 )
            {
               const HB_REFPARAM * pP = hb_csCallParam( szFunc, iPos );
               if( ! fFirst )
                  fprintf( yyc, ", " );
               fFirst = HB_FALSE;
               {
                  /* Match the canonical's per-slot type exactly so the
                     `ref` binds (C# ref is invariant) and so a `default`
                     literal has an explicit target type — a bare
                     `default` in an overloaded/dynamic call has none
                     (CS8716). */
                  const char * szSlot = NULL;
                  const char * szCs;
                  if( pP && pP->szType && hb_stricmp( pP->szType, "USUAL" ) != 0 )
                     szSlot = pP->szType;
                  if( ! szSlot )
                     szSlot = hb_astInferType( pP ? pP->szName : NULL, NULL );
                  szCs = hb_csTypeMap( szSlot );
                  if( pP && pP->fByRef &&
                      ! hb_csParamElidesArrayRef( szFunc, iPos ) )
                     fprintf( yyc, "ref HbDiscard<%s%s>.Value",
                              szCs, pP->fNilable ? "?" : "" );
                  else
                     fprintf( yyc, "default(%s%s)",
                              szCs, pP && pP->fNilable ? "?" : "" );
               }
            }
            else
               fNamed = HB_TRUE;   /* gap; next real slot emits named */
            if( pItem )
               pItem = pItem->pNext;
            continue;
         }

         if( ! fFirst )
            fprintf( yyc, ", " );
         fFirst = HB_FALSE;

         if( fNamed )
         {
            const HB_REFPARAM * pP = hb_csCallParam( szFunc, iPos );
            if( pP && pP->szName && pP->szName[ 0 ] )
               fprintf( yyc, "%s: ", pP->szName );
            /* If we can't find the name the emission falls back to
               positional — only correct if no more gaps follow.
               Unknown functions aren't in the table. */
         }

         /* Shimmed slot: the enclosing block declared a temp of the
            parameter's type seeded from the lvalue (named by
            hb_csShimTempName) — pass it by ref; hb_csEmitShimWriteback
            copies it back afterwards. */
         if( aShim && iPos < HB_CS_MAXSHIM && aShim[ iPos ] )
         {
            char szName[ 96 ];
            fprintf( yyc, "ref %s",
                     hb_csShimTempName( pArg, iShimBase, iPos, szName,
                                        sizeof( szName ) ) );
            pItem = pItem->pNext;
            continue;
         }

         if( pArg->ExprType == HB_ET_VARREF )
         {
            /* `@aArr` to a non-reassigned array param: emit plain (no
               ref) — element mutation propagates through the reference. */
            if( ! ( szFunc && hb_csParamElidesArrayRef( szFunc, iPos ) ) )
               fprintf( yyc, "ref " );
         }
         else if( iPos == 0 && szFunc && hb_csIsArrayMutator( szFunc ) )
         {
            /* ASize / AAdd may reallocate the dynamic[]; the HbRuntime
               overload takes `ref dynamic[]`. Emit `ref` only when the
               first arg is something C# can take by reference: a plain
               variable or a no-arg field access (DATA emits as fields).
               Other shapes fall back to the non-ref overload. */
            HB_BOOL fRefable = HB_FALSE;
            if( pArg->ExprType == HB_ET_VARIABLE )
               fRefable = HB_TRUE;
            else if( pArg->ExprType == HB_ET_SEND &&
                     pArg->value.asMessage.szMessage )
            {
               PHB_EXPR pSendParms = pArg->value.asMessage.pParms;
               HB_BOOL fNoParms = ! pSendParms ||
                  ( ( pSendParms->ExprType == HB_ET_LIST ||
                      pSendParms->ExprType == HB_ET_ARGLIST ||
                      pSendParms->ExprType == HB_ET_MACROARGLIST ) &&
                    ( ! pSendParms->value.asList.pExprList ||
                      pSendParms->value.asList.pExprList->ExprType == HB_ET_NONE ) );
               if( fNoParms )
                  fRefable = HB_TRUE;
            }
            if( fRefable )
               fprintf( yyc, "ref " );
         }
         else if( pArg->ExprType == HB_ET_VARIABLE && szFunc )
         {
            /* Plain variable into a by-ref param WITHOUT Harbour's `@`.
               In Harbour this is by value — the caller's variable is
               NOT written back. C# forces a `ref` (the slot emitted
               ref for the `@` callers, CS1620 otherwise), so bind a
               throwaway seeded with the variable's value:
               `ref HbDiscard<T>.Seed(x)` gives the callee x as input
               and discards its write-back — faithful by-value
               semantics (`ref x` here would write back, the silent
               divergence W0020 warns about). A non-reassigned array
               param emits plain (element mutation flows through the
               reference), so no shim there. */
            const HB_REFPARAM * pP = hb_csCallParam( szFunc, iPos );
            if( pP && pP->fByRef &&
                ! hb_csParamElidesArrayRef( szFunc, iPos ) )
            {
               const char * szSlot =
                  ( pP->szType && hb_stricmp( pP->szType, "USUAL" ) != 0 )
                     ? pP->szType
                     : hb_astInferType( pP->szName, NULL );
               const char * szCs = hb_csTypeMap( szSlot );
               /* `dynamic` can't be a generic holder here: Seed's ref
                  return would bind dynamically and CS1510. `object` is
                  the same at runtime and ref-compatible with a
                  `ref dynamic` parameter. */
               HB_BOOL fDynSlot = hb_stricmp( szCs, "dynamic" ) == 0;
               const char * szHold = fDynSlot ? "object" : szCs;
               const char * szNil = pP->fNilable ? "?" : "";
               /* Cast the argument ONLY when it is itself dynamic-typed
                  — a dynamic argument makes Seed dynamically-dispatched
                  (CS1510), and the cast to the holder type forces static
                  binding (a runtime convert). A concrete-typed argument
                  binds statically already; casting it would turn a real
                  type mismatch (wrong arg vs the slot) from the usual
                  CS1503/1620 into a spurious CS0030. */
               const char * szArgT = ( pArg->ExprType == HB_ET_VARIABLE )
                  ? hb_csArgVarType( pArg->value.asSymbol.name ) : NULL;
               HB_BOOL fArgDyn = ! szArgT ||
                  hb_stricmp( hb_csTypeMap( szArgT ), "dynamic" ) == 0;
               fprintf( yyc, "ref HbDiscard<%s%s>.Seed(", szHold, szNil );
               if( fArgDyn )
                  fprintf( yyc, "(%s%s)(", szHold, szNil );
               hb_csEmitExpr( pArg, yyc, HB_FALSE );
               if( fArgDyn )
                  fprintf( yyc, ")" );
               fprintf( yyc, ")" );
               pItem = pItem->pNext;
               continue;
            }
         }
         hb_csEmitExpr( pArg, yyc, HB_FALSE );
         pItem = pItem->pNext;
      }
   }
}

/* ---- Type mapping ---- */

static const char * hb_csTypeMap( const char * szHbType )
{
   if( ! szHbType || hb_stricmp( szHbType, "USUAL" ) == 0 )
      return "dynamic";
   if( hb_stricmp( szHbType, "NUMERIC" ) == 0 )
      return "decimal";
   if( hb_stricmp( szHbType, "INTEGER" ) == 0 ||
       hb_stricmp( szHbType, "int" ) == 0 )
      /* Harbour's integral type is 64-bit; emitting Int32 only
         manufactured width distinctions Harbour doesn't have (a
         len-9 vs len-10 def field, int-vs-long #define tiers). ALL
         integrals are C# long (Alex's ruling, 2026-07-10): long
         widens to decimal implicitly, indexes arrays natively, and
         BCL int-only call sites already receive explicit casts.
         "int" is the legacy source-side `as int` member spelling —
         same meaning, superseded by AS INTEGER. */
      return "long";
   if( hb_stricmp( szHbType, "STRING" ) == 0 ||
       hb_stricmp( szHbType, "CHARACTER" ) == 0 )
      return "string";
   if( hb_stricmp( szHbType, "LOGICAL" ) == 0 )
      return "bool";
   if( hb_stricmp( szHbType, "DATE" ) == 0 )
      return "DateOnly";
   if( hb_stricmp( szHbType, "TIMESTAMP" ) == 0 )
      return "DateTime";
   if( hb_stricmp( szHbType, "OBJECT" ) == 0 )
      /* Harbour's `OBJECT` means "any object receiving dynamic
         message dispatch" — messages resolve at runtime, not compile
         time. The closest C# equivalent is `dynamic`, not `object`:
         `object` blocks member access without a cast, which CS1061s
         the entire `oFoo:Bar()` pattern. `dynamic` defers binding to
         the DLR, matching Harbour's semantics and letting Hungarian
         `oX` parameters work without the reftab having to refine
         every slot to a concrete class. */
      return "dynamic";
   if( hb_stricmp( szHbType, "ARRAY" ) == 0 )
      return "dynamic[]";
   if( hb_stricmp( szHbType, "HASH"  ) == 0 ||
       hb_stricmp( szHbType, "HASHC" ) == 0 )
      /* Keys-unknown HASH defaults to string keys — the dominant
         population. HASHN (numeric keys, inferred from key-typed
         literals or subscript usage) gets a decimal-keyed dictionary
         so `h[nRecNo]` compiles; an int literal key implicitly
         converts to decimal at the indexer, so no key-identity trap. */
      return "Dictionary<string, dynamic>";
   if( hb_stricmp( szHbType, "HASHN" ) == 0 )
      return "Dictionary<decimal, dynamic>";
   if( hb_stricmp( szHbType, "BLOCK" ) == 0 ||
       /* `AS CODEBLOCK` — the formal declared-type spelling; same
          dynamic mapping as the reftab's BLOCK. */
       hb_stricmp( szHbType, "CODEBLOCK" ) == 0 )
      return "dynamic";
   /* Class name — widen to `dynamic` when the class extends
      HbDynamicObject so unknown member names compile. Applies to
      ORM-style base classes (SQLtTable, Table, FUNCTIONS) whose
      concrete subclasses define runtime-only columns. */
   if( s_pRefTab && hb_refTabIsClassDynamic( s_pRefTab, szHbType ) )
      return "dynamic";
   /* TOleAuto is HbRuntime's COM-automation wrapper; its .New()
      returns a dynamic COM proxy whose member names (Fields, Open,
      CursorLocation, MoveNext, …) are defined by the instantiated
      ProgID at runtime, not on the stub. Widening to `dynamic` at
      every use site lets the COM call sites compile. */
   if( hb_stricmp( szHbType, "TOleAuto" ) == 0 )
      return "dynamic";
   /* Otherwise a specific class name — pass through as-is */
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
/* Textual translation of a Harbour INLINE-method body into C#. The
   body comes from hb_clsCollectLine (the class parser captures
   everything after the INLINE keyword verbatim), so we re-walk it
   character by character applying the minimum-viable substitutions:
     ::name       → this.name          (Self:member shorthand)
     :=           → =                  (Harbour assignment)
     .T./.t./.F./.f. → true/false      (logical literals, word-bounded)
   Anything else passes through. The result is C# source; the caller
   decides between expression-body (`=> expr`) and block-body (for
   sequence expressions with top-level commas — this helper does not
   try to split; see hb_csInlineHasTopLevelComma). Buffer is 1 KB which
   covers every INLINE body seen in the easipos corpus. */
static HB_BOOL hb_csInlineIsIdCh( char c )
{
   return ( c >= 'A' && c <= 'Z' ) || ( c >= 'a' && c <= 'z' ) ||
          ( c >= '0' && c <= '9' ) || c == '_';
}

/* True if szId names one of the comma-separated INLINE method
   parameters in szParams (case-insensitive — Harbour identifiers).
   Parameters shadow everything, so the identifier rewriter must leave
   them alone: a param named `nType` must not become `XxxConst.NTYPE`
   just because some header defines NTYPE. */
static HB_BOOL hb_csInlineIsParam( const char * szParams, const char * szId )
{
   const char * q = szParams;
   HB_SIZE nIdLen;

   if( ! szParams || ! szId )
      return HB_FALSE;
   nIdLen = strlen( szId );
   while( *q )
   {
      const char * pStart;
      while( *q == ' ' || *q == ',' )
         q++;
      if( ! *q )
         break;
      pStart = q;
      while( *q && *q != ',' && *q != ' ' )
         q++;
      if( ( HB_SIZE ) ( q - pStart ) == nIdLen &&
          hb_strnicmp( pStart, szId, nIdLen ) == 0 )
         return HB_TRUE;
   }
   return HB_FALSE;
}

static const char * hb_csTranslateInline( const char * szVal,
                                          const char * szParams )
{
   static char s_szBuf[ 1024 ];
   HB_SIZE      nIn, nOut = 0;
   HB_SIZE      nLen;
   const char * p;
   HB_BOOL      fInStr = HB_FALSE;
   char         cStrQ  = '\0';
   /* ( [ { nesting depth + active iif( rewrites (see the iif handler
      in the identifier branch below). */
   int          iDepth = 0;
   int          iIifTop = -1;
   struct { int depth; int commas; } aIif[ 16 ];

   if( ! szVal )
      return szVal;

   /* Strip outer parens and surrounding whitespace */
   while( *szVal == ' ' || *szVal == '\t' )
      szVal++;
   nLen = strlen( szVal );
   while( nLen > 0 &&
          ( szVal[ nLen - 1 ] == ' ' || szVal[ nLen - 1 ] == '\t' ) )
      nLen--;

   /* Strip trailing `// ...` line comment — the PP can leave this in the
      raw INLINE text and it would otherwise collide with the `;` we
      append at the emit site. Only strip if it sits outside any string
      literal (a `//` inside `"..."` is data, not a comment). */
   {
      HB_SIZE i;
      HB_BOOL fInStr = HB_FALSE;
      char    cStrQ = '\0';
      for( i = 0; i + 1 < nLen; i++ )
      {
         if( fInStr )
         {
            if( szVal[ i ] == cStrQ )
               fInStr = HB_FALSE;
            continue;
         }
         if( szVal[ i ] == '"' || szVal[ i ] == '\'' )
         {
            fInStr = HB_TRUE;
            cStrQ = szVal[ i ];
            continue;
         }
         if( szVal[ i ] == '/' && szVal[ i + 1 ] == '/' )
         {
            while( i > 0 && ( szVal[ i - 1 ] == ' ' || szVal[ i - 1 ] == '\t' ) )
               i--;
            nLen = i;
            break;
         }
      }
   }

   /* If the INLINE body contains `->` it's a workarea-ALIAS
      expression — unsupported in C#. Short-circuit the whole method
      body to a stub; otherwise our textual `::` / `:=` rewrites
      produce `(this.alias).->( ... )` which breaks C# syntax. This
      mirrors the hb_csWarnUnsupported treatment the regular emitter
      uses for HB_ET_ALIASEXPR. The method still exists so cross-file
      references compile; calling it at runtime no-ops.

      Scanned over [0,nLen) — i.e. after the trailing-comment strip
      above — so a `->` sitting in an INLINE line's `// ...` comment
      doesn't wrongly stub an otherwise valid method. */
   {
      HB_SIZE i;
      for( i = 0; i + 1 < nLen; i++ )
      {
         if( szVal[ i ] == '-' && szVal[ i + 1 ] == '>' )
            return "HbRuntime.MacroStub";
      }
   }

   if( nLen >= 2 && szVal[ 0 ] == '(' && szVal[ nLen - 1 ] == ')' )
   {
      /* Only strip if these really are outer parens (balanced) */
      int    depth = 0;
      HB_SIZE i;
      HB_BOOL fOuter = HB_TRUE;
      for( i = 0; i < nLen; i++ )
      {
         if( szVal[ i ] == '(' )
            depth++;
         else if( szVal[ i ] == ')' )
         {
            depth--;
            if( depth == 0 && i + 1 < nLen )
            {
               fOuter = HB_FALSE;
               break;
            }
         }
      }
      if( fOuter )
      {
         szVal++;
         nLen -= 2;
      }
   }

   p = szVal;
   for( nIn = 0; nIn < nLen && nOut < sizeof( s_szBuf ) - 48; nIn++ )
   {
      /* String-literal contents are data — copy them through verbatim
         so no identifier rewrite (funcTab prefix or defines-map) fires
         inside `"..."` / `'...'`. Without this a literal like "result"
         gets mangled into "XxxConst.RESULT" when a define of that name
         exists (the map matches case-folded). */
      if( fInStr )
      {
         if( p[ nIn ] == cStrQ )
            fInStr = HB_FALSE;
         s_szBuf[ nOut++ ] = p[ nIn ];
         continue;
      }
      if( p[ nIn ] == '"' || p[ nIn ] == '\'' )
      {
         fInStr = HB_TRUE;
         cStrQ  = p[ nIn ];
         s_szBuf[ nOut++ ] = p[ nIn ];
         continue;
      }
      /* `{}` → `System.Array.Empty<dynamic>()` — Harbour empty array
         literal. C# `{}` in expression context is a block. Fully
         qualified because HbRuntimeStubs declares a NIE stub named
         `Array`, which `using static HbRuntime;` pulls into scope and
         shadows the System.Array type.
         `{ => }` → empty hash, mirroring hb_csTranslateInit. Both
         forms allow interior whitespace; any other `{` falls through
         to the depth tracker at the bottom of the loop. */
      if( p[ nIn ] == '{' )
      {
         HB_SIZE k = nIn + 1;
         while( k < nLen && ( p[ k ] == ' ' || p[ k ] == '\t' ) )
            k++;
         if( k < nLen && p[ k ] == '}' )
         {
            memcpy( s_szBuf + nOut, "System.Array.Empty<dynamic>()", 29 );
            nOut += 29;
            nIn = k;
            continue;
         }
         if( k + 1 < nLen && p[ k ] == '=' && p[ k + 1 ] == '>' )
         {
            k += 2;
            while( k < nLen && ( p[ k ] == ' ' || p[ k ] == '\t' ) )
               k++;
            if( k < nLen && p[ k ] == '}' )
            {
               memcpy( s_szBuf + nOut, "new Dictionary<dynamic, dynamic>()", 34 );
               nOut += 34;
               nIn = k;
               continue;
            }
         }
      }
      /* ::name → this.name, or Class.name for a CLASS VAR (static) —
         an instance reference to a static member is CS0176. Peek the
         identifier after `::` and check it against the current class. */
      if( p[ nIn ] == ':' && nIn + 1 < nLen && p[ nIn + 1 ] == ':' )
      {
         HB_SIZE nIdStart = nIn + 2;
         HB_SIZE nIdEnd   = nIdStart;
         char    szId[ 128 ];
         HB_BOOL fClassVar = HB_FALSE;
         HB_BOOL fDynMember = HB_FALSE;
         while( nIdEnd < nLen && hb_csInlineIsIdCh( p[ nIdEnd ] ) )
            nIdEnd++;
         if( nIdEnd > nIdStart && ( nIdEnd - nIdStart ) < sizeof( szId ) )
         {
            memcpy( szId, p + nIdStart, nIdEnd - nIdStart );
            szId[ nIdEnd - nIdStart ] = '\0';
            fClassVar = hb_csIsClassVar( s_szCurrentClass, szId );
            if( ! fClassVar && s_fCurrentClassDynamic &&
                ! hb_csIsDeclaredMember( s_szCurrentClass, szId ) )
               fDynMember = HB_TRUE;
         }
         if( fClassVar && strlen( s_szCurrentClass ) < 32 )
         {
            HB_SIZE nC = strlen( s_szCurrentClass );
            memcpy( s_szBuf + nOut, s_szCurrentClass, nC );
            nOut += nC;
            s_szBuf[ nOut++ ] = '.';
         }
         else if( fDynMember )
         {
            /* undeclared member of a dynamic class → ((dynamic)this). */
            memcpy( s_szBuf + nOut, "((dynamic)this).", 16 );
            nOut += 16;
         }
         else
         {
            memcpy( s_szBuf + nOut, "this.", 5 );
            nOut += 5;
         }
         /* Consume the member identifier here, verbatim — otherwise
            the word loop below re-scans it as a free identifier and
            the funcTab / defines-map rewrites fire on a member name
            (`::nStatus` → `this.XConst.NSTATUS`). */
         if( nIdEnd > nIdStart )
         {
            HB_SIZE j;
            for( j = nIdStart; j < nIdEnd && nOut < sizeof( s_szBuf ) - 1; j++ )
               s_szBuf[ nOut++ ] = p[ j ];
            nIn = nIdEnd - 1;   /* outer ++ lands past the member */
         }
         else
            nIn++;   /* bare `::` with no identifier — skip second ':' */
         continue;
      }
      /* := → = (assignment, rewriting only when not followed by '=') */
      if( p[ nIn ] == ':' && nIn + 1 < nLen && p[ nIn + 1 ] == '=' )
      {
         s_szBuf[ nOut++ ] = '=';
         nIn++;
         continue;
      }
      /* <> → != (the PP canonicalizes a source-level != to <>) */
      if( p[ nIn ] == '<' && nIn + 1 < nLen && p[ nIn + 1 ] == '>' )
      {
         s_szBuf[ nOut++ ] = '!';
         s_szBuf[ nOut++ ] = '=';
         nIn++;
         continue;
      }
      /* .T. .t. .F. .f. — word-bounded logical literals */
      if( p[ nIn ] == '.' && nIn + 2 < nLen && p[ nIn + 2 ] == '.' &&
          ( p[ nIn + 1 ] == 'T' || p[ nIn + 1 ] == 't' ||
            p[ nIn + 1 ] == 'F' || p[ nIn + 1 ] == 'f' ) )
      {
         HB_BOOL fVal = ( p[ nIn + 1 ] == 'T' || p[ nIn + 1 ] == 't' );
         if( nIn == 0 || ! hb_csInlineIsIdCh( p[ nIn - 1 ] ) )
         {
            const char * szLit = fVal ? "true" : "false";
            HB_SIZE nL = fVal ? 4 : 5;
            memcpy( s_szBuf + nOut, szLit, nL );
            nOut += nL;
            nIn += 2;   /* skip T/F and trailing '.' */
            continue;
         }
      }
      /* .AND. / .OR. / .NOT. → && / || / ! (dot-delimited, any case).
         No clash with the .T./.F. handler above: that one only fires
         on a single T/t/F/f between the dots. */
      if( p[ nIn ] == '.' && ( nIn == 0 || ! hb_csInlineIsIdCh( p[ nIn - 1 ] ) ) )
      {
         if( nIn + 4 < nLen && p[ nIn + 4 ] == '.' &&
             hb_strnicmp( p + nIn + 1, "and", 3 ) == 0 )
         {
            memcpy( s_szBuf + nOut, "&&", 2 );
            nOut += 2;
            nIn += 4;
            continue;
         }
         if( nIn + 3 < nLen && p[ nIn + 3 ] == '.' &&
             hb_strnicmp( p + nIn + 1, "or", 2 ) == 0 )
         {
            memcpy( s_szBuf + nOut, "||", 2 );
            nOut += 2;
            nIn += 3;
            continue;
         }
         if( nIn + 4 < nLen && p[ nIn + 4 ] == '.' &&
             hb_strnicmp( p + nIn + 1, "not", 3 ) == 0 )
         {
            s_szBuf[ nOut++ ] = '!';
            nIn += 4;
            continue;
         }
      }
      /* self / Self (word-bounded) → this */
      if( ( p[ nIn ] == 's' || p[ nIn ] == 'S' ) && nIn + 3 < nLen &&
          ( p[ nIn + 1 ] == 'e' || p[ nIn + 1 ] == 'E' ) &&
          ( p[ nIn + 2 ] == 'l' || p[ nIn + 2 ] == 'L' ) &&
          ( p[ nIn + 3 ] == 'f' || p[ nIn + 3 ] == 'F' ) &&
          ( nIn + 4 == nLen || ! hb_csInlineIsIdCh( p[ nIn + 4 ] ) ) &&
          ( nIn == 0 || ! hb_csInlineIsIdCh( p[ nIn - 1 ] ) ) )
      {
         memcpy( s_szBuf + nOut, "this", 4 );
         nOut += 4;
         nIn += 3;   /* skip "elf", outer ++ skips 's' */
         continue;
      }
      /* Identifier token: look up in hbfuncs.tab. If it's a Harbour
         built-in with a namespace prefix, emit `Prefix.UPPERCASE` so
         the call resolves at C# compile time. Mirrors hb_csFuncMap's
         treatment of HB_ET_FUNCALL — needed here because class VAR
         INIT values and INLINE bodies come through as raw text, not
         AST, so the normal emitter path doesn't touch them. */
      if( ( ( p[ nIn ] >= 'A' && p[ nIn ] <= 'Z' ) ||
            ( p[ nIn ] >= 'a' && p[ nIn ] <= 'z' ) ||
            p[ nIn ] == '_' ) &&
          ( nIn == 0 || ! hb_csInlineIsIdCh( p[ nIn - 1 ] ) ) )
      {
         HB_SIZE nIdStart = nIn;
         HB_SIZE nIdLen;
         char    szId[ 128 ];
         const char * szPrefix;
         while( nIn < nLen && hb_csInlineIsIdCh( p[ nIn ] ) )
            nIn++;
         nIdLen = nIn - nIdStart;
         if( nIdLen < sizeof( szId ) )
         {
            memcpy( szId, p + nIdStart, nIdLen );
            szId[ nIdLen ] = '\0';
            /* NIL → null. Reserved word, so no param/member can shadow
               it and the scope checks below don't apply. */
            if( nIdLen == 3 && hb_strnicmp( szId, "nil", 3 ) == 0 )
            {
               memcpy( s_szBuf + nOut, "null", 4 );
               nOut += 4;
               nIn--;   /* outer loop `nIn++` advances past last id char */
               continue;
            }
            /* iif( a, b, c ) → (( a ) ? ( b ) : ( c )) — streaming:
               emit `((` now, then rewrite this call's two depth-0
               commas to `) ? (` / `) : (` and its closing paren to
               `))` as the main loop reaches them (aIif stack + the
               depth tracker at the bottom of the loop). Must stay a
               real ternary: an eager HbRuntime.IIF() helper would
               evaluate the losing branch, breaking the common
               `iif(hb_HHasKey(h, k), h[k], default)` guard idiom. */
            if( nIdLen == 3 && hb_strnicmp( szId, "iif", 3 ) == 0 )
            {
               HB_SIZE k = nIn;
               while( k < nLen && ( p[ k ] == ' ' || p[ k ] == '\t' ) )
                  k++;
               if( k < nLen && p[ k ] == '(' &&
                   iIifTop < ( int ) HB_SIZEOFARRAY( aIif ) - 1 )
               {
                  memcpy( s_szBuf + nOut, "((", 2 );
                  nOut += 2;
                  iDepth++;
                  iIifTop++;
                  aIif[ iIifTop ].depth = iDepth;
                  aIif[ iIifTop ].commas = 0;
                  nIn = k;   /* outer ++ lands past the '(' */
                  continue;
               }
            }
            /* Two scopes outrank any global rewrite:
               - a member name after a single-colon send (`oObj:Panel`)
                 belongs to the receiver, not the defines-map / funcTab;
               - an INLINE method parameter shadows everything. */
            if( ( nIdStart > 0 && p[ nIdStart - 1 ] == ':' ) ||
                hb_csInlineIsParam( szParams, szId ) )
            {
               HB_SIZE j;
               for( j = 0; j < nIdLen && nOut < sizeof( s_szBuf ) - 1; j++ )
                  s_szBuf[ nOut++ ] = szId[ j ];
               nIn--;   /* outer loop `nIn++` advances past last id char */
               continue;
            }
            szPrefix = hb_funcTabPrefix( szId );
            if( szPrefix )
            {
               const char * szCanon = hb_funcTabCanonName( szId );
               HB_SIZE j;
               if( ! szCanon ) szCanon = szId;
               for( j = 0; szPrefix[ j ] && nOut < sizeof( s_szBuf ) - 1; j++ )
                  s_szBuf[ nOut++ ] = szPrefix[ j ];
               if( nOut < sizeof( s_szBuf ) - 1 )
                  s_szBuf[ nOut++ ] = '.';
               for( j = 0; szCanon[ j ] && nOut < sizeof( s_szBuf ) - 1; j++ )
                  s_szBuf[ nOut++ ] = szCanon[ j ];
            }
            else
            {
               /* Header `#define` from the defines-map gets the same
                  qualified-name treatment as a Harbour built-in: a bare
                  PANELSIGNON in a CLASS VAR INIT or INLINE body becomes
                  `PanelsConst.PANELSIGNON`. Without this, the inline
                  translator emits PANELSIGNON unqualified — CS0103 at
                  C# compile because the per-source Const class isn't in
                  the file's `using static` set. Mirrors HB_ET_VARIABLE's
                  hb_defineMapLookup branch in hb_csEmitExpr. */
               const char * szDefCanon = NULL;
               const char * szDefClass =
                  hb_defineMapLookupCanon( szId, &szDefCanon );
               if( szDefClass && szDefCanon )
               {
                  HB_SIZE j;
                  for( j = 0; szDefClass[ j ] && nOut < sizeof( s_szBuf ) - 1; j++ )
                     s_szBuf[ nOut++ ] = szDefClass[ j ];
                  if( nOut < sizeof( s_szBuf ) - 1 )
                     s_szBuf[ nOut++ ] = '.';
                  for( j = 0; szDefCanon[ j ] && nOut < sizeof( s_szBuf ) - 1; j++ )
                     s_szBuf[ nOut++ ] = szDefCanon[ j ];
               }
               else
               {
                  HB_SIZE j;
                  for( j = 0; j < nIdLen && nOut < sizeof( s_szBuf ) - 1; j++ )
                     s_szBuf[ nOut++ ] = szId[ j ];
               }
            }
         }
         nIn--;   /* outer loop `nIn++` advances past last id char */
         continue;
      }
      /* Depth-aware fall-through: track ( [ { nesting so an active
         iif( rewrite (aIif stack above) can recognize its own
         top-level commas and closing paren. Commas nested deeper —
         inside a call, subscript, or array literal argument — copy
         through untouched. */
      if( p[ nIn ] == '(' || p[ nIn ] == '[' || p[ nIn ] == '{' )
         iDepth++;
      else if( p[ nIn ] == ')' || p[ nIn ] == ']' || p[ nIn ] == '}' )
      {
         if( iIifTop >= 0 && p[ nIn ] == ')' &&
             iDepth == aIif[ iIifTop ].depth )
         {
            memcpy( s_szBuf + nOut, "))", 2 );
            nOut += 2;
            iIifTop--;
            iDepth--;
            continue;
         }
         iDepth--;
      }
      else if( p[ nIn ] == ',' && iIifTop >= 0 &&
               iDepth == aIif[ iIifTop ].depth &&
               aIif[ iIifTop ].commas < 2 )
      {
         memcpy( s_szBuf + nOut,
                 aIif[ iIifTop ].commas == 0 ? ") ? (" : ") : (", 5 );
         nOut += 5;
         aIif[ iIifTop ].commas++;
         continue;
      }
      s_szBuf[ nOut++ ] = p[ nIn ];
   }
   s_szBuf[ nOut ] = '\0';
   return s_szBuf;
}

/* True if szVal has a comma at paren-depth 0 (outside any nested call
   or subexpression). Determines whether an INLINE body is a single
   expression (`=> expr`) or a sequence of statements (block body). */
static HB_BOOL hb_csInlineHasTopLevelComma( const char * szVal )
{
   int depth = 0;
   HB_BOOL fInStr = HB_FALSE;
   char cStrQ = '\0';

   if( ! szVal )
      return HB_FALSE;
   while( *szVal )
   {
      char c = *szVal++;
      if( fInStr )
      {
         if( c == cStrQ )
            fInStr = HB_FALSE;
         continue;
      }
      if( c == '"' || c == '\'' )
      {
         fInStr = HB_TRUE;
         cStrQ = c;
         continue;
      }
      if( c == '(' || c == '[' || c == '{' )
         depth++;
      else if( c == ')' || c == ']' || c == '}' )
         depth--;
      else if( c == ',' && depth == 0 )
         return HB_TRUE;
   }
   return HB_FALSE;
}

/* Trim leading and trailing blanks in place. */
static void hb_csTrimBoth( char * sz )
{
   char *  p = sz;
   HB_SIZE n;

   while( *p == ' ' || *p == '\t' )
      p++;
   if( p != sz )
      memmove( sz, p, strlen( p ) + 1 );
   n = strlen( sz );
   while( n > 0 && ( sz[ n - 1 ] == ' ' || sz[ n - 1 ] == '\t' ) )
      sz[ --n ] = '\0';
}

static const char * hb_csTranslateInit( const char * szVal )
{
   static char s_szBuf[ 512 ];
   HB_SIZE nLen;

   if( ! szVal )
      return szVal;


   nLen = strlen( szVal );

   /* Strip trailing line comment (two-slash to end-of-string). The
      preprocessor preserves the comment in the raw INIT text, which
      would otherwise eat the terminating semicolon we append at the
      emit site, and also prevents .F. / .T. / NIL / READONLY from
      matching the canonical checks below. Block comments are left
      alone — they do not span EOL. */
   {
      HB_SIZE i;
      for( i = 0; i + 1 < nLen; i++ )
      {
         if( szVal[ i ] == '/' && szVal[ i + 1 ] == '/' )
         {
            while( i > 0 && ( szVal[ i - 1 ] == ' ' || szVal[ i - 1 ] == '\t' ) )
               i--;
            if( i < sizeof( s_szBuf ) )
            {
               memcpy( s_szBuf, szVal, i );
               s_szBuf[ i ] = '\0';
               szVal = s_szBuf;
               nLen = i;
            }
            break;
         }
      }
   }

   /* Strip trailing READONLY */
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

   /* {} → empty array (fully qualified — see comment in the char-scan
      branch above for why System.Array is needed). */
   if( strcmp( szVal, "{}" ) == 0 )
      return "System.Array.Empty<dynamic>()";

   /* { => } → empty hash. Match with any whitespace between tokens. */
   {
      const char * p = szVal;
      if( *p == '{' )
      {
         p++;
         while( *p == ' ' || *p == '\t' ) p++;
         if( *p == '=' && p[ 1 ] == '>' )
         {
            p += 2;
            while( *p == ' ' || *p == '\t' ) p++;
            if( *p == '}' && p[ 1 ] == '\0' )
               return "new Dictionary<string, dynamic>()";
         }
      }
   }

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

   /* `{ |a, b| expr }` — a codeblock initialiser. The INLINE
      translator below has no notion of codeblock syntax and passes the
      braces through verbatim, emitting `{ || true }` into C# (CS1525
      invalid expression term). Lower it to the same shape the AST
      emitter produces for HB_ET_CODEBLOCK: an explicit Func<> cast,
      needed because C# cannot infer a delegate type for a lambda
      assigned to a `dynamic` field. Arity comes from the parameter
      list, and the body goes through the INLINE translator with those
      parameters declared so they are not mistaken for functions. */
   {
      const char * p = szVal;

      while( *p == ' ' || *p == '\t' )
         p++;
      if( *p == '{' )
      {
         const char * pBar = NULL;
         p++;
         while( *p == ' ' || *p == '\t' )
            p++;
         if( *p == '|' )
            pBar = strchr( p + 1, '|' );
         if( pBar )
         {
            const char * pClose = strrchr( pBar, '}' );

            if( pClose && pClose > pBar )
            {
               static char s_szCB[ 1024 ];
               char szParams[ 256 ], szBody[ 512 ];
               HB_SIZE nP = ( HB_SIZE ) ( pBar - ( p + 1 ) );
               HB_SIZE nB = ( HB_SIZE ) ( pClose - ( pBar + 1 ) );

               if( nP < sizeof( szParams ) && nB < sizeof( szBody ) )
               {
                  int iCount = 0, iOff = 0, j;
                  char szFunc[ 160 ];
                  const char * q;

                  memcpy( szParams, p + 1, nP );
                  szParams[ nP ] = '\0';
                  memcpy( szBody, pBar + 1, nB );
                  szBody[ nB ] = '\0';
                  hb_csTrimBoth( szParams );
                  hb_csTrimBoth( szBody );

                  if( *szParams )
                  {
                     iCount = 1;
                     for( q = szParams; *q; q++ )
                        if( *q == ',' )
                           iCount++;
                  }

                  szFunc[ 0 ] = '\0';
                  for( j = 0; j <= iCount &&
                       iOff < ( int ) sizeof( szFunc ) - 12; j++ )
                     iOff += hb_snprintf( szFunc + iOff,
                                          sizeof( szFunc ) - iOff,
                                          "dynamic%s", j < iCount ? ", " : "" );

                  hb_snprintf( s_szCB, sizeof( s_szCB ),
                               "((Func<%s>)((%s) => %s))", szFunc, szParams,
                               hb_csTranslateInline( szBody, szParams ) );
                  return s_szCB;
               }
            }
         }
      }
   }

   /* Fall through to the INLINE translator for general expressions:
      handles identifier remapping via hbfuncs.tab so `SPACE(30)` →
      `HbRuntime.SPACE(30)` and `ctod("")` → `HbRuntime.CTOD("")`.
      No parameter list — CLASS VAR INIT values have no params. */
   return hb_csTranslateInline( szVal, NULL );
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
      case HB_EO_EXPEQ:   return " ^= ";  /* TODO: a = HbRuntime.Pow(a, b) — C# ^= is XOR */
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
   const char * szPrefix;

   /* ToString() collides with object.ToString() inherited by every
      emitted class, so a bare `ToString(x)` call resolves to the
      zero-arg instance method and fails with CS1501. Qualify to
      avoid the shadow:
        - if a user PROCEDURE/FUNCTION ToString is declared in the
          corpus (reftab will have the entry), emit `Program.ToString`
          so the user's implementation wins. The call-site static
          `using static Program` isn't enough because the instance
          method takes precedence; the explicit `Program.` qualifier
          sidesteps that.
        - otherwise fall back to a thin HbRuntime.ToString stub
          (delegates to Str) so the number-to-string idiom used by
          easipos authors coming from C# keeps compiling even in
          trees that don't ship a ToString.prg.
      Method-call sites (`obj:ToString()`) go through HB_ET_SEND,
      not this path, so a genuine class method is unaffected. */
   if( hb_stricmp( szName, "ToString" ) == 0 )
   {
      if( s_pRefTab && hb_refTabParamCount( s_pRefTab, "ToString" ) >= 0 )
         return "Program.ToString";
      return "HbRuntime.ToString";
   }

   szPrefix = hb_funcTabPrefix( szName );
   if( szPrefix )
   {
      /* Use the canonical name recorded in hbfuncs.tab — that matches
         the actual method declared in HbRuntime.cs (C# is case-
         sensitive; Harbour is not, so the source-site spelling is
         unreliable). Fall back to the source name if for some reason
         the canonical isn't there. */
      const char * szCanon = hb_funcTabCanonName( szName );
      if( ! szCanon ) szCanon = szName;
      hb_snprintf( s_szBuf, sizeof( s_szBuf ), "%s.%s", szPrefix, szCanon );
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

   /* A funcall hoisted out of this expression: emit the temp that already
      holds its result instead of re-emitting (and re-evaluating) the call. */
   if( pExpr == s_pHoistCall && s_szHoistVar[ 0 ] )
   {
      fprintf( yyc, "%s", s_szHoistVar );
      return;
   }

   switch( pExpr->ExprType )
   {
      case HB_ET_NONE:
         break;

      case HB_ET_NIL:
         fprintf( yyc, "null" );
         break;

      case HB_ET_NUMERIC:
         if( pExpr->value.asNum.NumType == HB_ET_LONG )
            fprintf( yyc, "%" HB_PF64 "d", pExpr->value.asNum.val.l );
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
            const char * szLocal   = hb_csResolveLocal( szVarName );
            if( hb_stricmp( szVarName, "Self" ) == 0 )
               fprintf( yyc, "this" );
            else if( szLocal == NULL &&
                     s_pRefTab && hb_refTabIsPublic( s_pRefTab, szVarName ) )
            {
               /* PUBLIC variable reference — the owning .prg emits a
                  single `public static dynamic <name>;` field on the
                  merged Program partial, so references in THIS file
                  resolve bare. Prioritised above the STATIC/MEMVAR
                  mangling branches: a `MEMVAR aX` declaration in a
                  file that also references a PUBLIC `aX` should hit
                  the shared public field, not an isolated per-file
                  mangled shadow. Emits the caller's casing — PUBLIC
                  canonicalisation would need an API on hb_refTab to
                  fetch the declared name; currently a bool probe. */
               fprintf( yyc, "%s", szVarName );
            }
            else if( szLocal == NULL && hb_csIsFileStatic( szVarName ) )
            {
               /* STATIC reference — rewrite to the mangled class field
                  name (file-unique; function-scope statics also carry
                  their owner function). A local with the same name in
                  the current scope shadows the static, matching
                  Harbour's rule. Declaration-site casing so drifted-
                  case references collapse onto the single field. */
               char szFld[ 256 ];
               fprintf( yyc, "%s",
                        hb_csStaticFieldName(
                           hb_csFileStaticIdx( szVarName ),
                           szFld, sizeof( szFld ) ) );
            }
            else if( szLocal == NULL && hb_csIsFileMemvar( szVarName ) )
            {
               /* File-scope MEMVAR reference — same file-base mangling
                  as STATIC. Locals still shadow. */
               fprintf( yyc, "%s_%s", s_szFileBase,
                        hb_csFileMemvarCanon( szVarName ) );
            }
            else if( szLocal )
            {
               /* Local or parameter — emit the spelling captured at
                  the declaration site. Harbour source commonly drifts
                  case across references (oPLU vs oPlu) against a
                  single LOCAL/PARAMETER declaration; collapsing to
                  the declared form here eliminates the whole class
                  of CS0103 that would otherwise surface per-reference
                  (each mis-cased call site is a distinct C# name). */
               fprintf( yyc, "%s", szLocal );
            }
            else
            {
               /* Last-resort: a bare name that didn't resolve as a
                  local/static/memvar. Ask the defines map whether it
                  belongs to a per-source Const class (generated by
                  gendefines.py) and qualify the reference if so.
                  Use the canonical name from the map (not the source
                  spelling) so case-collision buckets — e.g. fixed.ch
                  defines `FixedName` (3) and fixedarr.ch defines
                  `FIXEDNAME` (1) — resolve to the right C# member. */
               const char * szDefCanon = NULL;
               const char * szDefClass = hb_defineMapLookupCanon(
                  szVarName, &szDefCanon );
               if( szDefClass )
                  fprintf( yyc, "%s.%s", szDefClass,
                           szDefCanon ? szDefCanon : szVarName );
               else
                  fprintf( yyc, "%s", szVarName );
            }
         }
         break;

      case HB_ET_FUNREF:
         /* Harbour's `@FuncName()` — a first-class function reference
            used in dispatch tables, hash values, LOCAL assignments.
            C# won't convert a method group to `dynamic` directly, so
            we route through HbRuntime.FuncPtr which reflects on
            Program and returns a cached `Func<dynamic[], dynamic>`.
            The delegate handles arity mismatch and default values at
            call time — caller invokes via dynamic dispatch or
            HbRuntime.EVAL and the args-splat lambda takes care of
            the rest. */
         fprintf( yyc, "HbRuntime.FuncPtr(\"%s\")",
                  pExpr->value.asSymbol.name ? pExpr->value.asSymbol.name : "" );
         break;

      case HB_ET_REFERENCE:
         fprintf( yyc, "ref " );
         hb_csEmitExpr( pExpr->value.asReference, yyc, HB_TRUE );
         break;

      case HB_ET_FUNCALL:
         {
            const char * szName = NULL;
            const char * szCanon = NULL;
            if( pExpr->value.asFunCall.pFunName &&
                pExpr->value.asFunCall.pFunName->ExprType == HB_ET_FUNNAME )
               szName = pExpr->value.asFunCall.pFunName->value.asSymbol.name;

            /* Canonicalise casing via harbour.hbx (if loaded). Runs
               before everything else so szName reflects the canonical
               spelling from here on. hb_hbxCanonLookup returns NULL
               for user identifiers (locals, file STATICs, etc.) so
               they pass through unchanged. */
            if( szName )
            {
               szCanon = hb_hbxCanonLookup( szName );
               if( szCanon )
                  szName = szCanon;
            }

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

            /* ORM construction — ConstructORMTable(XxxDef(...), ...)
               with a mapped def class emits as
               `new <CanonClass>(<original args>)`: the generated
               strongly-typed model (gen-fieldtypes.py --emit-models)
               replaces the dynamic Table metaclass, and the def-array
               argument still reaches the OrmTable base ctor so runtime
               metadata is unchanged. Canonical class casing comes from
               the map (a source-side `PluDef()` site must construct
               the generated `PLUDef`). Unmapped/indirect first args
               fall through to the plain call. */
            if( szName && hb_stricmp( szName, "ConstructORMTable" ) == 0 )
            {
               PHB_EXPR pParms = pExpr->value.asFunCall.pParms;
               PHB_EXPR pFirst = pParms;
               if( pParms && ( pParms->ExprType == HB_ET_LIST ||
                               pParms->ExprType == HB_ET_ARGLIST ) )
                  pFirst = pParms->value.asList.pExprList;
               if( pFirst && pFirst->ExprType == HB_ET_FUNCALL &&
                   pFirst->value.asFunCall.pFunName &&
                   pFirst->value.asFunCall.pFunName->ExprType == HB_ET_FUNNAME )
               {
                  const char * szCanon = hb_fieldTypesClassCanon(
                     pFirst->value.asFunCall.pFunName->value.asSymbol.name );
                  if( szCanon )
                  {
                     fprintf( yyc, "new %s(", szCanon );
                     hb_csEmitCallArgs( szName, pParms, yyc );
                     fprintf( yyc, ")" );
                     break;
                  }
               }
            }

            if( szName )
            {
               /* Intra-file STATIC call: resolve to the file-mangled
                  name. Checked before hb_csFuncMap — the stdlib table
                  won't have the file-prefixed form and would otherwise
                  pass the bare name straight through. */
               if( hb_csIsFileStaticFunc( szName ) )
               {
                  char szMangledBuf[ 256 ];
                  fprintf( yyc, "%s", hb_csMangleStaticFunc( szName,
                                         szMangledBuf, sizeof( szMangledBuf ) ) );
               }
               else if( szCanon )
               {
                  /* Canonicalised names from hbx are always routed
                     through HbRuntime. That sidesteps two collisions
                     that a bare emit would hit: (1) names matching
                     .NET BCL types (`Array`, `Type`, `Version`) cause
                     CS1955 "not invocable member"; (2) hb_-prefixed
                     runtime fns aren't globals in any namespace, so a
                     bare `hb_bitAnd(...)` CS0103s. HbRuntime owns the
                     cross-cutting implementation of both groups. */
                  fprintf( yyc, "HbRuntime.%s", szCanon );
               }
               else
               {
                  /* User function — hb_csFuncMap passes bare names
                     through unchanged, so for cross-file calls we
                     additionally collapse to the reftab's declaration-
                     site casing. Without this, a FUNCTION MyDBUseArea
                     called elsewhere as MyDBUSEAREA() emits two
                     distinct C# names and one loses to CS0103. */
                  const char * szMapped = hb_csFuncMap( szName );
                  if( szMapped == szName && s_pRefTab )
                     szMapped = hb_refTabFuncCanon( s_pRefTab, szName );
                  fprintf( yyc, "%s", szMapped );
                  /* Flag call sites that pass more args than the
                     declaration takes — that's a source-level bug
                     Harbour tolerates but C# rejects as CS1501. */
                  hb_csWarnExtraArgs( szName, pExpr->value.asFunCall.pParms );
                  /* Flag calls that pass a bare arg where reftab
                     says the slot is ref (inconsistent `@` usage
                     across call sites — C# rejects as CS1620). */
                  hb_csWarnMissingRef( szName, pExpr->value.asFunCall.pParms );
                  /* Flag `@array` passed to a param the callee never
                     reassigns — the `@` is redundant (W0023). */
                  hb_csWarnArrayRefElided( szName, pExpr->value.asFunCall.pParms );
               }
            }
            else
               hb_csEmitExpr( pExpr->value.asFunCall.pFunName, yyc, HB_FALSE );
            fprintf( yyc, "(" );
            hb_csEmitCallArgs( szName, pExpr->value.asFunCall.pParms, yyc );
            fprintf( yyc, ")" );
         }
         break;

      case HB_ET_FUNNAME:
         {
            const char * szName = pExpr->value.asSymbol.name;
            const char * szCanon = hb_hbxCanonLookup( szName );
            if( ! szCanon && s_pRefTab )
               szCanon = hb_refTabFuncCanon( s_pRefTab, szName );
            fprintf( yyc, "%s", szCanon ? szCanon : szName );
         }
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
                     /* Cast needed: New()/Init() returns object.
                        Capitalise the first letter of the method name:
                        Harbour is case-insensitive so some source uses
                        lowercase `:new(...)`, which would emit as the
                        reserved word `new` — C# can't call a method
                        named `new`. Uppercase-first yields `New` which
                        matches the convention used by Harbour classes. */
                     const char * szMsg = pExpr->value.asMessage.szMessage;
                     char szMsgBuf[ 128 ];
                     HB_SIZE nMsgLen = strlen( szMsg );
                     if( nMsgLen > 0 && nMsgLen < sizeof( szMsgBuf ) )
                     {
                        szMsgBuf[ 0 ] = ( szMsg[ 0 ] >= 'a' && szMsg[ 0 ] <= 'z' )
                                      ? szMsg[ 0 ] - 'a' + 'A' : szMsg[ 0 ];
                        memcpy( szMsgBuf + 1, szMsg + 1, nMsgLen );
                        szMsg = szMsgBuf;
                     }
                     fprintf( yyc, "(%s)new %s().%s(", szCtor, szCtor, szMsg );
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
         /* Dynamic member access: obj:&(nameExpr) or obj:&name.
            Detect macro pMessage BEFORE emitting the object, because
            we need to rewrite the entire expression as a runtime
            helper call: GETMEMBER(obj, name) for reads, SENDMSG for
            method calls. The SETMEMBER (write) case is intercepted
            in the HB_EO_ASSIGN path. */
         if( pExpr->value.asMessage.pMessage &&
             pExpr->value.asMessage.pMessage->ExprType == HB_ET_MACRO )
         {
            PHB_EXPR pMacro = pExpr->value.asMessage.pMessage;
            PHB_EXPR pObj   = pExpr->value.asMessage.pObject;
            HB_BOOL  fCall  = pExpr->value.asMessage.pParms != NULL;

            fprintf( yyc, "HbRuntime.%s(", fCall ? "SENDMSG" : "GETMEMBER" );
            /* Object argument */
            if( pObj )
            {
               if( pObj->ExprType == HB_ET_VARIABLE &&
                   hb_stricmp( pObj->value.asSymbol.name, "Self" ) == 0 )
                  fprintf( yyc, "this" );
               else
                  hb_csEmitExpr( pObj, yyc, HB_FALSE );
            }
            else if( s_pWithObject )
               hb_csEmitExpr( s_pWithObject, yyc, HB_FALSE );
            /* Member name argument */
            fprintf( yyc, ", " );
            if( pMacro->value.asMacro.pExprList )
               hb_csEmitExpr( pMacro->value.asMacro.pExprList, yyc, HB_FALSE );
            else if( pMacro->value.asMacro.szMacro )
            {
               /* The parser uppercases bare macro identifiers (`&name`
                  stores "NAME"). Resolve against the current function's
                  locals so the canonical casing is used — C# is
                  case-sensitive. */
               const char * szResolved =
                  hb_csResolveLocal( pMacro->value.asMacro.szMacro );
               fprintf( yyc, "%s",
                        szResolved ? szResolved : pMacro->value.asMacro.szMacro );
            }
            /* Method arguments if call */
            if( fCall )
            {
               PHB_EXPR pArgs = pExpr->value.asMessage.pParms;
               PHB_EXPR pItem = ( pArgs &&
                  ( pArgs->ExprType == HB_ET_ARGLIST ||
                    pArgs->ExprType == HB_ET_LIST ) )
                  ? pArgs->value.asList.pExprList : pArgs;
               while( pItem )
               {
                  if( pItem->ExprType != HB_ET_NONE )
                  {
                     fprintf( yyc, ", " );
                     hb_csEmitExpr( pItem, yyc, HB_FALSE );
                  }
                  pItem = pItem->pNext;
               }
            }
            fprintf( yyc, ")" );
            break;
         }
         /* `::Super:Method(args)` — calling a parent-class method via
            Harbour's inheritance Super keyword. Emit as C# `base.Method(args)`.
            Detected by an inner SEND chain: outer pObject is SEND with
            pObject=Self and szMessage=Super.
            Exception: `::Super:className()` / `::Super:ClassName()` —
            keep the HbSuperRef path so it returns BaseType.Name rather
            than the runtime type name (C#'s GetType() always returns
            the runtime type even via a base-class cast). */
         if( pExpr->value.asMessage.pObject &&
             pExpr->value.asMessage.pObject->ExprType == HB_ET_SEND &&
             pExpr->value.asMessage.pObject->value.asMessage.pObject &&
             pExpr->value.asMessage.pObject->value.asMessage.pObject->ExprType == HB_ET_VARIABLE &&
             hb_stricmp( pExpr->value.asMessage.pObject->value.asMessage.pObject->value.asSymbol.name, "Self" ) == 0 &&
             pExpr->value.asMessage.pObject->value.asMessage.szMessage &&
             hb_stricmp( pExpr->value.asMessage.pObject->value.asMessage.szMessage, "Super" ) == 0 &&
             pExpr->value.asMessage.szMessage &&
             hb_stricmp( pExpr->value.asMessage.szMessage, "className" ) != 0 &&
             hb_stricmp( pExpr->value.asMessage.szMessage, "ClassName" ) != 0 )
         {
            fprintf( yyc, "base.%s", pExpr->value.asMessage.szMessage );
            if( pExpr->value.asMessage.pParms )
            {
               fprintf( yyc, "(" );
               hb_csEmitCallArgs( pExpr->value.asMessage.szMessage,
                                  pExpr->value.asMessage.pParms, yyc );
               fprintf( yyc, ")" );
            }
            break;
         }
         /* `f:exec(args)` — Harbour's invocation syntax for a function
            pointer (Symbol class method, taken via @FuncName()). C#'s
            FuncPtr() returns Func<dynamic[],dynamic>, which has no
            member called `exec`; emitting this as `f.exec(args)` fails
            at the dynamic binder. Route through HbRuntime.Eval instead,
            which already covers Func<dynamic[],dynamic>, plain Delegate
            (via DynamicInvoke), and codeblocks. Symbol:exec lives in
            Harbour's runtime stdlib and is never user-overridden in
            our codebase. */
         if( pExpr->value.asMessage.szMessage &&
             hb_stricmp( pExpr->value.asMessage.szMessage, "exec" ) == 0 )
         {
            fprintf( yyc, "HbRuntime.Eval(" );
            if( pExpr->value.asMessage.pObject )
            {
               if( pExpr->value.asMessage.pObject->ExprType == HB_ET_VARIABLE &&
                   hb_stricmp( pExpr->value.asMessage.pObject->value.asSymbol.name, "Self" ) == 0 )
                  fprintf( yyc, "this" );
               else
                  hb_csEmitExpr( pExpr->value.asMessage.pObject, yyc, HB_FALSE );
            }
            else if( s_pWithObject )
               hb_csEmitExpr( s_pWithObject, yyc, HB_FALSE );
            if( pExpr->value.asMessage.pParms )
            {
               PHB_EXPR pArgs = pExpr->value.asMessage.pParms;
               PHB_EXPR pItem = ( pArgs->ExprType == HB_ET_ARGLIST ||
                                  pArgs->ExprType == HB_ET_LIST )
                                ? pArgs->value.asList.pExprList : pArgs;
               while( pItem )
               {
                  if( pItem->ExprType != HB_ET_NONE )
                  {
                     fprintf( yyc, ", " );
                     hb_csEmitExpr( pItem, yyc, HB_FALSE );
                  }
                  pItem = pItem->pNext;
               }
            }
            fprintf( yyc, ")" );
            break;
         }
         if( pExpr->value.asMessage.pObject )
         {
            /* Self:member → this.member, with two exceptions:
               - a CLASS VAR (static) must be Class.member (CS0176);
               - a member not declared in the class or any ancestor is
                 routed through `((dynamic)this)`. On a dynamic class
                 that reaches the dictionary-backed member; on a plain
                 class it's a name Harbour resolves at runtime (e.g. a
                 COM event-sink handler whose method was never
                 implemented). Either way it compiles and dispatches —
                 or throws — at runtime instead of failing CS1061. */
            if( pExpr->value.asMessage.pObject->ExprType == HB_ET_VARIABLE &&
                hb_stricmp( pExpr->value.asMessage.pObject->value.asSymbol.name, "Self" ) == 0 )
            {
               const char * szMsg = pExpr->value.asMessage.szMessage;
               if( szMsg && hb_csIsClassVar( s_szCurrentClass, szMsg ) )
                  fprintf( yyc, "%s", s_szCurrentClass );
               else if( szMsg &&
                        ! hb_csIsBuiltinObjMsg( szMsg ) &&
                        ! hb_csIsDeclaredMember( s_szCurrentClass, szMsg ) )
                  fprintf( yyc, "((dynamic)this)" );
               else
                  fprintf( yyc, "this" );
            }
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
            char szKeyBuf[ 256 ];
            fprintf( yyc, "(" );
            hb_csEmitCallArgs(
               hb_csSendRefKey( pExpr->value.asMessage.pObject,
                                pExpr->value.asMessage.szMessage,
                                szKeyBuf, sizeof( szKeyBuf ) ),
               pExpr->value.asMessage.pParms, yyc );
            fprintf( yyc, ")" );
         }
         else if( pExpr->value.asMessage.szMessage &&
                  ( hb_stricmp( pExpr->value.asMessage.szMessage, "Super" ) == 0 ||
                    hb_stricmp( pExpr->value.asMessage.szMessage, "className" ) == 0 ) )
         {
            /* Harbour: `obj:Super`, `obj:className` — bare (no parens) forms
               of built-in OO helpers. In C# these resolve to extension
               methods on `object` (HbObjectExtensions), which need parens
               to invoke. Without this the emit would be a property access
               that doesn't exist. */
            fprintf( yyc, "()" );
         }
         break;

      case HB_ET_ARRAYAT:
         hb_csEmitExpr( pExpr->value.asList.pExprList, yyc, HB_TRUE );
         {
            /* Decide hash-vs-array. Hash if any of:
                 - the index is a string literal or a string-typed
                   expression (e.g. `Str(N)`)
                 - the index is a `c<X>` / `sc<X>`-prefixed variable
                   (resolves to STRING via the type registry / Hungarian)
                 - the indexed expression resolves to HASH via the
                   type registry (locals, file-statics, params) or via
                   Hungarian on a `h<X>` / `sh<X>` name (variable or
                   message).
               Dictionary<string, dynamic> accepts any string key; the
               array branch wraps in `(long)(...) - 1` which is wrong
               for hashes and produces CS errors at runtime indices. */
            HB_BOOL fHash = HB_FALSE;
            PHB_EXPR pIdx = pExpr->value.asList.pIndex;
            PHB_EXPR pLhs = pExpr->value.asList.pExprList;
            if( pIdx && pIdx->ExprType == HB_ET_STRING )
               fHash = HB_TRUE;
            if( ! fHash && pIdx )
            {
               /* For a bare variable index we need its declared/inferred
                  type. hb_astInferType( NULL, pIdx ) sees no name (only
                  the expression) and falls through to USUAL — pass the
                  variable's own name so its `c`/`sc` prefix is honored,
                  and prefer the type registry which captures explicit
                  type seeding from declarations. */
               const char * szT = NULL;
               if( pIdx->ExprType == HB_ET_VARIABLE )
                  szT = hb_csArgVarType( pIdx->value.asSymbol.name );
               if( ! szT )
                  szT = hb_astInferType(
                     pIdx->ExprType == HB_ET_VARIABLE
                        ? pIdx->value.asSymbol.name : NULL,
                     pIdx );
               if( szT && hb_stricmp( szT, "STRING" ) == 0 )
                  fHash = HB_TRUE;
            }
            if( ! fHash && pLhs )
            {
               const char * szLhsName = NULL;
               if( pLhs->ExprType == HB_ET_VARIABLE ||
                   pLhs->ExprType == HB_ET_VARREF )
                  szLhsName = pLhs->value.asSymbol.name;
               else if( pLhs->ExprType == HB_ET_SEND )
                  szLhsName = pLhs->value.asMessage.szMessage;
               else if( pLhs->ExprType == HB_ET_ALIASVAR &&
                        pLhs->value.asAlias.pVar &&
                        pLhs->value.asAlias.pVar->ExprType == HB_ET_VARIABLE )
                  /* Harbour parses an unqualified file-static reference
                     inside a function body as `MEMVAR->name` (see
                     gencsharp HB_ET_ALIASVAR handler). The inner pVar
                     carries the real name, which the file-static and
                     Hungarian-prefix lookups can still resolve. */
                  szLhsName = pLhs->value.asAlias.pVar->value.asSymbol.name;
               if( szLhsName )
               {
                  const char * szT = hb_csArgVarType( szLhsName );
                  if( ! szT )
                     szT = hb_astInferType( szLhsName, NULL );
                  if( hb_astIsHashFamily( szT ) )
                     fHash = HB_TRUE;
               }
            }
            if( fHash )
            {
               fprintf( yyc, "[" );
               hb_csEmitExpr( pIdx, yyc, HB_FALSE );
               fprintf( yyc, "]" );
            }
            /* Integer literal index — emit decremented value directly */
            else if( pIdx &&
                     pIdx->ExprType == HB_ET_NUMERIC &&
                     pIdx->value.asNum.NumType == HB_ET_LONG )
            {
               fprintf( yyc, "[%" HB_PF64 "d]",
                        pIdx->value.asNum.val.l - 1 );
            }
            /* INTEGER-typed variable index (Pass 2.5 int candidacy) —
               already a C# int, no cast needed. */
            else if( pIdx && pIdx->ExprType == HB_ET_VARIABLE &&
                     hb_csLocalTypeGet( pIdx->value.asSymbol.name ) &&
                     hb_stricmp( hb_csLocalTypeGet(
                                    pIdx->value.asSymbol.name ),
                                 "INTEGER" ) == 0 )
            {
               fprintf( yyc, "[%s - 1]", pIdx->value.asSymbol.name );
            }
            /* Variable or expression index — cast and subtract at runtime */
            else
            {
               fprintf( yyc, "[(long)(" );
               hb_csEmitExpr( pIdx, yyc, HB_FALSE );
               fprintf( yyc, ") - 1]" );
            }
         }
         break;

      case HB_ET_ARRAY:
         {
            PHB_EXPR pItem;
            pItem = pExpr->value.asList.pExprList;
            /* `{ ... }` — array of all the enclosing variadic's args.
               Its lone child is the `...` spread (an empty,
               reference-flagged ARGLIST). Emit the vararg array `hbva`
               itself; wrapping it in a one-element `new dynamic[] {
               hbva }` would make every `aArgs[i]` index the wrong
               level. */
            if( pItem && ! pItem->pNext &&
                pItem->ExprType == HB_ET_ARGLIST &&
                pItem->value.asList.reference &&
                ! pItem->value.asList.pExprList )
            {
               fprintf( yyc, "hbva" );
               break;
            }
            fprintf( yyc, "new dynamic[] { " );
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
            HB_BOOL fComplex = HB_FALSE;
            int    iPairs = 0;
            int    iInd = s_iExprIndent;
            /* C# key type for this literal: its own literal keys win
               (any numeric key → decimal); an empty/unlabeled literal
               inherits s_szHashKeyCs, which decl-init emission points
               at the declared variable's key type so
               `LOCAL hById := { => }` matches its
               Dictionary<decimal, dynamic> declaration. */
            const char * szKeyCs = s_szHashKeyCs;
            {
               PHB_EXPR pKey = pExpr->value.asList.pExprList;
               while( pKey )
               {
                  if( pKey->ExprType == HB_ET_NUMERIC )
                     { szKeyCs = "decimal"; break; }
                  if( pKey->ExprType == HB_ET_STRING )
                     { szKeyCs = "string"; break; }
                  if( ! pKey->pNext )
                     break;
                  pKey = pKey->pNext->pNext;
               }
            }

            /* "Complex" = any value is itself a hash or array literal.
               A flag/config dict like Flags_shFlags has one nested
               meta-hash per key and reads as a 500KB single line in
               the historical layout. */
            for( pItem = pExpr->value.asList.pExprList; pItem; )
            {
               PHB_EXPR pVal = pItem->pNext;
               iPairs++;
               if( ! pVal )
                  break;
               if( pVal->ExprType == HB_ET_HASH ||
                   pVal->ExprType == HB_ET_ARRAY )
                  fComplex = HB_TRUE;
               pItem = pVal->pNext;
            }

            /* Break at the commas — one `{ key, value },` per line —
               for any nested initializer or anything beyond a handful
               of pairs; a jsonify-style payload dict in argument
               position was previously one multi-KB line. Small scalar
               hashes stay inline. Contexts that don't track a column
               (locals, arguments — s_iExprIndent 0) get a method-body
               default: items at 12, braces at 8. C# doesn't care and
               it reads fine even when the opener sits deeper. */
            if( iInd <= 0 && ( fComplex || iPairs > 4 ) )
               iInd = 12;

            if( fComplex || iPairs > 4 )
            {
               fprintf( yyc, "new Dictionary<%s, dynamic>\n%*s{\n",
                        szKeyCs, iInd - 4, "" );
               /* Bump indent so a nested complex hash/array (if we add
                  one to the heuristic later) lines its own children up
                  one level deeper. Restored on the way out. */
               s_iExprIndent = iInd + 4;
               for( pItem = pExpr->value.asList.pExprList; pItem; )
               {
                  fprintf( yyc, "%*s{ ", iInd, "" );
                  hb_csEmitExpr( pItem, yyc, HB_FALSE );
                  pItem = pItem->pNext;
                  if( pItem )
                  {
                     fprintf( yyc, ", " );
                     hb_csEmitExpr( pItem, yyc, HB_FALSE );
                     pItem = pItem->pNext;
                  }
                  fprintf( yyc, " }%s\n", pItem ? "," : "" );
               }
               s_iExprIndent = iInd;
               fprintf( yyc, "%*s}", iInd - 4, "" );
            }
            else
            {
               fprintf( yyc, "new Dictionary<%s, dynamic> { ", szKeyCs );
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
               PHB_EXPR pTrue = pCond->pNext;
               PHB_EXPR pFalse = pTrue->pNext;
               fprintf( yyc, " ? " );
               if( pTrue->ExprType == HB_ET_NONE )
                  fprintf( yyc, "default" );
               else
                  hb_csEmitExpr( pTrue, yyc, HB_FALSE );
               fprintf( yyc, " : " );
               if( pFalse && pFalse->ExprType != HB_ET_NONE )
                  hb_csEmitExpr( pFalse, yyc, HB_FALSE );
               else
                  fprintf( yyc, "default" );
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
            /* Multi-element HB_ET_LIST is Harbour's comma-operator —
               `(a, b)` evaluates both and returns the last value. C#
               has no equivalent in expression position. Emit a
               `default` placeholder and a warning so the rest of the
               file still compiles and downstream callers of any
               FUNCTIONs/PROCs defined here still resolve. The one
               broken expression will fail at runtime if it executes,
               but the source rarely hits these paths. HB_ET_ARGLIST
               and _MACROARGLIST are real argument lists and keep the
               comma-separated emit. */
            if( pExpr->ExprType == HB_ET_LIST && pItem && pItem->pNext )
            {
               /* Name the leading sub-expressions so the construct is
                  findable — the line number alone has proven too
                  coarse for a 2000-line file. */
               char szDesc[ 160 ];
               char szParts[ 96 ] = "";
               PHB_EXPR pI = pItem;
               int n;
               for( n = 0; pI && n < 3; pI = pI->pNext, n++ )
               {
                  const char * szN = "<expr>";
                  if( pI->ExprType == HB_ET_VARIABLE )
                     szN = pI->value.asSymbol.name;
                  else if( pI->ExprType == HB_ET_FUNCALL &&
                           pI->value.asFunCall.pFunName &&
                           pI->value.asFunCall.pFunName->ExprType ==
                              HB_ET_FUNNAME )
                     szN = pI->value.asFunCall.pFunName->value.asSymbol.name;
                  else if( pI->ExprType == HB_ET_STRING )
                     szN = "<string>";
                  else if( pI->ExprType == HB_ET_NUMERIC )
                     szN = "<num>";
                  if( n )
                     hb_strncat( szParts, ", ", sizeof( szParts ) - 1 );
                  hb_strncat( szParts, szN, sizeof( szParts ) - 1 );
               }
               hb_snprintf( szDesc, sizeof( szDesc ),
                            "comma-operator (%s%s)", szParts,
                            pI ? ", ..." : "" );
               hb_csWarnUnsupported( szDesc );
               fprintf( yyc, "HbRuntime.MacroStub" );
               break;
            }
            {
               /* A single-element LIST is either:
                    (a) a source-level `(expr)` parenthesization, OR
                    (b) the parser's auto-wrap around IF/WHILE/RETURN
                        conditions, assignment RHS, etc.
                  We can't tell them apart structurally, but context
                  resolves it: when fParen=true (we're nested inside
                  another op), the parens matter — preserve them.
                  Without preservation, `IF !(a + b == c)` would emit
                  as `!a + b == c` and the unary `!` would bind only
                  to `a` (Harbour's .NOT. has low precedence; C#'s !
                  has highest).
                  When fParen=false (top level — IF condition, RHS,
                  arg slot), the wrap is parser bookkeeping; peel it
                  so we don't emit `if ((cond))`. */
               HB_BOOL fSingle = pItem && ! pItem->pNext;
               HB_BOOL fEmitParen = fSingle && fParen;
               if( fEmitParen )
                  fprintf( yyc, "(" );
               while( pItem )
               {
                  hb_csEmitExpr( pItem, yyc, HB_FALSE );
                  pItem = pItem->pNext;
                  if( pItem )
                     fprintf( yyc, ", " );
               }
               if( fEmitParen )
                  fprintf( yyc, ")" );
            }
         }
         break;

      case HB_ET_MACRO:
      {
         /* Macros (`&name`) can't be transpiled. Surface a W0016
            warning and emit an `HbRuntime.MacroStub` placeholder — a
            bare `default` won't do where an identifier is required
            (e.g. member access `oObj.&name` would leave nothing after
            the dot). The rest of the .cs still emits so downstream
            callers keep compiling. */
         const char * szMacro = pExpr->value.asMacro.szMacro;
         char szDesc[ 128 ];
         hb_snprintf( szDesc, sizeof( szDesc ), "macro &%s",
                      szMacro ? szMacro : "?" );
         hb_csWarnUnsupported( szDesc );
         fprintf( yyc, "HbRuntime.MacroStub" );
         break;
      }

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
            if( szLocal )
            {
               /* Known case-typo'd local — emit canonical form. */
               fprintf( yyc, "%s", szLocal );
            }
            else if( s_pRefTab && hb_refTabIsPublic( s_pRefTab, szVarName ) )
            {
               /* PUBLIC variable that the parser auto-wrapped as a
                  `MEMVAR->aName` alias because hb_compVariableFind
                  doesn't resolve PUBLICs at parse time. The PUBLIC
                  owner emits a bare `public static dynamic <name>;`
                  field, so references — including this alias-wrapped
                  one — must bind bare. Prioritised over the
                  IsFileMemvar branch because that registry
                  double-duties as a PUBLIC-owner dedup set (see the
                  HB_AST_PUBLIC walker below), which would otherwise
                  steer PUBLIC refs into the file-mangled path and
                  CS0103 (`hwfinit_aHWFlag` when the field is plain
                  `aHWFlag`). */
               fprintf( yyc, "%s", szVarName );
            }
            else if( hb_csIsFileStatic( szVarName ) )
            {
               /* File-scope STATIC the parser auto-wrapped as an
                  implicit alias (happens for indexed references
                  like `saPOSList[i]` — parser doesn't consult the
                  file-static registry). Same recovery as the case-
                  typo'd local above; emit the mangled field name
                  instead of the @-prefix syntactic-survival branch. */
               char szFld[ 256 ];
               fprintf( yyc, "%s",
                        hb_csStaticFieldName(
                           hb_csFileStaticIdx( szVarName ),
                           szFld, sizeof( szFld ) ) );
            }
            else if( hb_csIsFileMemvar( szVarName ) )
            {
               fprintf( yyc, "%s_%s", s_szFileBase,
                        hb_csFileMemvarCanon( szVarName ) );
            }
            else
            {
               /* Unknown identifier wrapped as implicit alias. Could
                  be a workarea field (`Flags->string`), an undeclared
                  memvar, or a name-collision with a C# keyword
                  (`string`, `int`, `float`, `true`, etc.). Emit a
                  verbatim identifier via `@`-prefix so C# keywords
                  don't turn into syntax errors — unresolved refs
                  still fall through to the runtime's memvar lookup
                  and surface cleanly there rather than breaking the
                  entire file's syntax. */
               fprintf( yyc, "@%s", szVarName ? szVarName : "" );
            }
         }
         else
         {
            /* Named-alias reference (WORKAREA->field). Unsupported in C#
               output — surface a W0016 warning and emit a placeholder so
               the rest of the .cs stays syntactically valid and
               downstream callers keep compiling. */
            const char * szAlias = ( pExpr->value.asAlias.pAlias &&
                                     pExpr->value.asAlias.pAlias->ExprType == HB_ET_ALIAS )
                                   ? pExpr->value.asAlias.pAlias->value.asSymbol.name : NULL;
            const char * szVar = ( pExpr->value.asAlias.pVar &&
                                   pExpr->value.asAlias.pVar->ExprType == HB_ET_VARIABLE )
                                 ? pExpr->value.asAlias.pVar->value.asSymbol.name : NULL;
            char szDesc[ 128 ];
            hb_snprintf( szDesc, sizeof( szDesc ), "ALIAS reference %s->%s",
                         szAlias ? szAlias : "?", szVar ? szVar : "?" );
            hb_csWarnUnsupported( szDesc );
            fprintf( yyc, "HbRuntime.MacroStub" );
         }
         break;

      case HB_ET_ALIASEXPR:
      {
         const char * szAlias = ( pExpr->value.asAlias.pAlias &&
                                  pExpr->value.asAlias.pAlias->ExprType == HB_ET_ALIAS )
                                ? pExpr->value.asAlias.pAlias->value.asSymbol.name : NULL;
         char szDesc[ 128 ];
         hb_snprintf( szDesc, sizeof( szDesc ), "ALIAS expression %s->(...)",
                      szAlias ? szAlias : "?" );
         hb_csWarnUnsupported( szDesc );
         fprintf( yyc, "HbRuntime.MacroStub" );
         break;
      }

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
               /* Wrap the lambda in an explicit `(Func<dynamic, ..., dynamic>)`
                  cast. C# can't infer a delegate type when the lambda is
                  passed to a `dynamic`-typed parameter (CS1660), which is
                  the common case for HbRuntime helpers like AScan / AEval
                  that take an arbitrary code block. The arity is taken
                  from the codeblock's pLocals chain. */
               int iParamCount = 0;
               PHB_CBVAR pCount = pExpr->value.asCodeblock.pLocals;
               while( pCount ) { iParamCount++; pCount = pCount->pNext; }
               fprintf( yyc, "((Func<" );
               {
                  int j;
                  for( j = 0; j <= iParamCount; j++ )
                     fprintf( yyc, "dynamic%s", j < iParamCount ? ", " : "" );
               }
               fprintf( yyc, ">)(" );
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
               fprintf( yyc, "))" );
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
         /* Propagate fParen so wrapped ASSIGN can self-parenthesize
            when in a disambiguation context. */
         hb_csEmitExpr( pExpr->value.asSetGet.pVar, yyc, fParen );
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
               /* a ^ b → HbRuntime.Pow(a, b). Not Math.Pow: that
                  returns double, which then poisons surrounding
                  decimal arithmetic (e.g. `x % (2 ^ 31)` → CS0019
                  decimal-vs-double). hbtypes.c infers ^ as NUMERIC
                  (decimal), so the emitted call must return decimal
                  too. */
               fprintf( yyc, "HbRuntime.Pow(" );
               hb_csEmitExpr( pExpr->value.asOperator.pLeft, yyc, HB_FALSE );
               fprintf( yyc, ", " );
               hb_csEmitExpr( pExpr->value.asOperator.pRight, yyc, HB_FALSE );
               fprintf( yyc, ")" );
            }
            else if( pExpr->ExprType == HB_EO_IN )
            {
               /* a $ b → HbRuntime.HbIn(a, b). Dispatches at runtime:
                  string b → substring, hash b → key containment.
                  b.Contains(a) only works for strings — a Dictionary
                  has ContainsKey, not Contains. */
               fprintf( yyc, "HbRuntime.HbIn(" );
               hb_csEmitExpr( pExpr->value.asOperator.pLeft, yyc, HB_FALSE );
               fprintf( yyc, ", " );
               hb_csEmitExpr( pExpr->value.asOperator.pRight, yyc, HB_FALSE );
               fprintf( yyc, ")" );
            }
            else if( pExpr->ExprType == HB_EO_ASSIGN &&
                     pExpr->value.asOperator.pLeft &&
                     ( ( pExpr->value.asOperator.pLeft->ExprType ==
                            HB_ET_VARIABLE &&
                         hb_csVarIsInteger(
                            pExpr->value.asOperator.pLeft->value.asSymbol.name ) ) ||
                       hb_csSendMemberIsInteger(
                          pExpr->value.asOperator.pLeft ) ) &&
                     hb_csNeedsIntCast( pExpr->value.asOperator.pRight ) )
            {
               /* Write into an int-typed variable or a class member
                  declared integral (Self `as int`, or any class-typed
                  receiver's AS INTEGER member via the scan-registered
                  Class::member reftab rows) — coerce the RHS. */
               hb_csEmitExpr( pExpr->value.asOperator.pLeft, yyc, HB_FALSE );
               fprintf( yyc, " = (long)(" );
               hb_csEmitExpr( pExpr->value.asOperator.pRight, yyc, HB_FALSE );
               fprintf( yyc, ")" );
            }
            else if( pExpr->ExprType == HB_EO_DIV &&
                     hb_csExprIsCsIntegral( pExpr->value.asOperator.pLeft ) &&
                     hb_csExprIsCsIntegral( pExpr->value.asOperator.pRight ) )
            {
               /* Harbour `/` is ALWAYS float (7/2 == 3.5); C# int/int
                  truncates. Both operands are statically C#-integral
                  here, so force decimal division by casting the left
                  operand. A decimal on either side already gives
                  decimal division — this fires only when both sides
                  are int-shaped, so ordinary decimal divisions stay
                  cast-free. This is what lets int/long typing (Pass
                  2.5 locals, int defines, ORM fields, AS INTEGER/AS
                  LONG members) spread without division semantics
                  pushing everything back to decimal. */
               if( fParen )
                  fprintf( yyc, "(" );
               fprintf( yyc, "(decimal)(" );
               hb_csEmitExpr( pExpr->value.asOperator.pLeft, yyc, HB_FALSE );
               fprintf( yyc, ") / " );
               hb_csEmitExpr( pExpr->value.asOperator.pRight, yyc, HB_TRUE );
               if( fParen )
                  fprintf( yyc, ")" );
            }
            else if( pExpr->ExprType == HB_EO_ASSIGN &&
                     pExpr->value.asOperator.pLeft &&
                     pExpr->value.asOperator.pLeft->ExprType == HB_ET_SEND &&
                     pExpr->value.asOperator.pLeft->value.asMessage.pMessage &&
                     pExpr->value.asOperator.pLeft->value.asMessage.pMessage->ExprType == HB_ET_MACRO )
            {
               /* obj:&(name) := value → HbRuntime.SETMEMBER(obj, name, value) */
               PHB_EXPR pSend  = pExpr->value.asOperator.pLeft;
               PHB_EXPR pMacro = pSend->value.asMessage.pMessage;
               PHB_EXPR pObj   = pSend->value.asMessage.pObject;

               fprintf( yyc, "HbRuntime.SETMEMBER(" );
               if( pObj )
               {
                  if( pObj->ExprType == HB_ET_VARIABLE &&
                      hb_stricmp( pObj->value.asSymbol.name, "Self" ) == 0 )
                     fprintf( yyc, "this" );
                  else
                     hb_csEmitExpr( pObj, yyc, HB_FALSE );
               }
               else if( s_pWithObject )
                  hb_csEmitExpr( s_pWithObject, yyc, HB_FALSE );
               fprintf( yyc, ", " );
               if( pMacro->value.asMacro.pExprList )
                  hb_csEmitExpr( pMacro->value.asMacro.pExprList, yyc, HB_FALSE );
               else if( pMacro->value.asMacro.szMacro )
               {
                  const char * szResolved =
                     hb_csResolveLocal( pMacro->value.asMacro.szMacro );
                  fprintf( yyc, "%s",
                           szResolved ? szResolved : pMacro->value.asMacro.szMacro );
               }
               fprintf( yyc, ", " );
               hb_csEmitExpr( pExpr->value.asOperator.pRight, yyc, HB_FALSE );
               fprintf( yyc, ")" );
            }
            else
            {
               HB_BOOL fNeedParen = HB_FALSE;
               if( fParen )
               {
                  PHB_EXPR pLeft = pExpr->value.asOperator.pLeft;
                  PHB_EXPR pRight = pExpr->value.asOperator.pRight;
                  /* Assignment has Harbour-expression semantics (`:=`
                     returns the assigned value) and is commonly used
                     inside comparisons — `(h := FCreate(...)) < 0`.
                     In Harbour the parens are required to bind `<` to
                     the result. When this expression is itself a
                     child of another binop (fParen set by the parent),
                     emit parens ourselves if we're the ASSIGN — C#
                     `=` has lower precedence than `<`, so without
                     them the parent would parse as `h = (call < 0)`,
                     a bool→decimal assignment and a wrong result. */
                  if( pExpr->ExprType == HB_EO_ASSIGN ||
                      pExpr->ExprType == HB_EO_PLUSEQ ||
                      pExpr->ExprType == HB_EO_MINUSEQ ||
                      pExpr->ExprType == HB_EO_MULTEQ ||
                      pExpr->ExprType == HB_EO_DIVEQ ||
                      pExpr->ExprType == HB_EO_MODEQ ||
                      pExpr->ExprType == HB_EO_EXPEQ )
                     fNeedParen = HB_TRUE;
                  else if( ( pLeft && pLeft->ExprType >= HB_EO_ASSIGN &&
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

   if( pNode->iLine > 0 )
      s_iCurrentStmtLine = pNode->iLine;

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
      {
         PHB_EXPR pStmtExpr = pNode->value.asExprStmt.pExpr;
         /* `HB_SYMBOL_UNUSED(x)` expands via std.ch to `((x))` — purely
            an "I know this is unused, don't warn" pragma in Harbour.
            By emit time it's an expression statement whose expression
            is a bare variable / literal / message access / array index,
            none of which are valid statement forms in C# (CS0201:
            "Only assignment, call, increment, decrement, await, and
            new object expressions can be used as a statement"). Skip
            these — the Harbour intent is "no-op", and that's exactly
            what emitting nothing produces. A bare `obj:member` (SEND
            with no parms) falls in this bucket too; a SEND with parms
            is a real method call and stays. */
         if( pStmtExpr )
         {
            HB_BOOL fValueless = HB_FALSE;
            /* Peel one-element HB_ET_LIST wrappers — `((lNarrow))` from
               the HB_SYMBOL_UNUSED macro arrives as a LIST around the
               variable, not the variable directly. */
            PHB_EXPR pInner = pStmtExpr;
            while( pInner &&
                   ( pInner->ExprType == HB_ET_LIST ||
                     pInner->ExprType == HB_ET_ARGLIST ) &&
                   pInner->value.asList.pExprList &&
                   ! pInner->value.asList.pExprList->pNext )
               pInner = pInner->value.asList.pExprList;
            switch( pInner ? pInner->ExprType : 0 )
            {
               case HB_ET_VARIABLE: case HB_ET_VARREF:
               case HB_ET_NUMERIC:  case HB_ET_STRING:
               case HB_ET_LOGICAL:  case HB_ET_NIL:
               case HB_ET_DATE:     case HB_ET_TIMESTAMP:
               case HB_ET_ARRAYAT:
                  fValueless = HB_TRUE;
                  break;
               case HB_ET_SEND:
                  if( ! pInner->value.asMessage.pParms )
                  {
                     /* In Harbour a paren-less send statement still
                        dispatches the message (SENDSHORT 0 + POP) —
                        executing the METHOD if the name is one. C#
                        can't reproduce that without knowing the member
                        kind (obj.Member; is CS0201, obj.Member(); is
                        wrong for DATA), so the statement is dropped —
                        but audibly, since a side-effecting method call
                        may be vanishing. Harbour itself flags the
                        construct W0027, and -es2 builds reject it. */
                     char szDesc[ 160 ];
                     hb_snprintf( szDesc, sizeof( szDesc ),
                                  "parameterless send statement (:%s) — dispatch dropped",
                                  pInner->value.asMessage.szMessage ?
                                     pInner->value.asMessage.szMessage : "?" );
                     hb_csWarnUnsupported( szDesc );
                     fValueless = HB_TRUE;
                  }
                  break;
               case HB_ET_MACRO:
                  hb_csWarnUnsupported( "macro statement (&name)" );
                  fValueless = HB_TRUE;
                  break;
               case HB_ET_ALIASVAR:
                  /* The parser wraps every unresolved bare identifier
                     — file-scope STATICs, PUBLICs, case-typo'd locals
                     — as an implicit `MEMVAR->name` ALIASVAR. The
                     expression-position handler resolves that shape
                     silently (see the HB_ET_ALIASVAR emit), so as a
                     valueless statement it's just the VARIABLE case
                     in disguise: skip without warning. Only a
                     malformed alias shape earns the W0016. */
                  if( pInner->value.asAlias.pAlias &&
                      pInner->value.asAlias.pAlias->ExprType == HB_ET_ALIAS &&
                      pInner->value.asAlias.pVar &&
                      pInner->value.asAlias.pVar->ExprType == HB_ET_VARIABLE )
                  {
                     fValueless = HB_TRUE;
                     break;
                  }
                  hb_csWarnUnsupported( "ALIAS reference (alias->var)" );
                  fValueless = HB_TRUE;
                  break;
               case HB_ET_ALIASEXPR:
                  /* Workarea-alias statement (`Flags->( dbCloseArea() )`).
                     Emitting `HbRuntime.MacroStub;` here would surface
                     as CS0201 since MacroStub is a static field. Fire
                     the same W0016 the expression-form emit would have
                     raised, then skip the statement — the unsupported
                     expression isn't reachable at compile-time anyway. */
                  hb_csWarnUnsupported( "ALIAS expression (alias->( expr ))" );
                  fValueless = HB_TRUE;
                  break;
               default:
                  break;
            }
            if( fValueless )
            {
               s_iLastLine = pNode->iLine;
               break;
            }
         }
         /* iif(cond, a, b) used as a statement can't be emitted as a
            C# ternary — "(a ? b : c);" is rejected by CS0201. Expand
            it to an if/else so both branches can be call statements
            (the common Harbour idiom: `iif(cond, DoThing(), )`). */
         if( pStmtExpr && pStmtExpr->ExprType == HB_ET_IIF &&
             pStmtExpr->value.asList.pExprList )
         {
            PHB_EXPR pCond = pStmtExpr->value.asList.pExprList;
            PHB_EXPR pTrue = pCond->pNext;
            PHB_EXPR pFalse = pTrue ? pTrue->pNext : NULL;
            HB_BOOL fWrap = hb_csConditionNeedsBoolUnwrap( pCond );

            hb_csEmitIndent( yyc, iIndent );
            fprintf( yyc, "if (" );
            hb_csEmitExpr( pCond, yyc, HB_FALSE );
            if( fWrap )
               fprintf( yyc, " == true" );
            fprintf( yyc, ")\n" );
            hb_csEmitIndent( yyc, iIndent + 1 );
            if( pTrue && pTrue->ExprType != HB_ET_NONE )
            {
               hb_csEmitExpr( pTrue, yyc, HB_FALSE );
               fprintf( yyc, ";\n" );
            }
            else
               fprintf( yyc, ";\n" );
            if( pFalse && pFalse->ExprType != HB_ET_NONE )
            {
               hb_csEmitIndent( yyc, iIndent );
               fprintf( yyc, "else\n" );
               hb_csEmitIndent( yyc, iIndent + 1 );
               hb_csEmitExpr( pFalse, yyc, HB_FALSE );
               fprintf( yyc, ";\n" );
            }
            break;
         }
         /* ref-shim: a call statement passing a typed lvalue by-ref to
            a `ref dynamic` (USUAL) parameter. C# ref invariance rejects
            it directly, so wrap the call in a brace block that copies
            each such lvalue through a `dynamic` temp and back. Covers a
            bare call `Foo(@x)` and a plain `var := Foo(@x)`; richer LHS
            / expression-context calls fall through to the plain emit. */
         {
            PHB_EXPR pCall     = NULL;
            PHB_EXPR pAsgnLeft = NULL;
            if( pStmtExpr && pStmtExpr->ExprType == HB_ET_FUNCALL )
               pCall = pStmtExpr;
            else if( pStmtExpr && pStmtExpr->ExprType == HB_EO_ASSIGN &&
                     pStmtExpr->value.asOperator.pLeft &&
                     pStmtExpr->value.asOperator.pLeft->ExprType == HB_ET_VARIABLE &&
                     pStmtExpr->value.asOperator.pRight &&
                     pStmtExpr->value.asOperator.pRight->ExprType == HB_ET_FUNCALL )
            {
               pCall     = pStmtExpr->value.asOperator.pRight;
               pAsgnLeft = pStmtExpr->value.asOperator.pLeft;
            }
            if( pCall )
            {
               HB_BOOL aShim[ HB_CS_MAXSHIM ];
               int iShims = hb_csCollectRefShims( pCall, aShim, HB_CS_MAXSHIM );
               if( iShims > 0 )
               {
                  const char * szFunc =
                     pCall->value.asFunCall.pFunName->value.asSymbol.name;
                  PHB_EXPR pHead = hb_csFunCallArgHead( pCall );
                  int iBase = s_iShimDepth++;

                  hb_csEmitIndent( yyc, iIndent );
                  fprintf( yyc, "{\n" );
                  /* temps of each parameter's type, seeded from the lvalues */
                  hb_csEmitShimTemps( szFunc, pHead, aShim, iBase, yyc,
                                      iIndent + 1 );
                  /* the call — hb_csEmitCallArgs swaps in `ref _hbref<base>_N` */
                  hb_csEmitIndent( yyc, iIndent + 1 );
                  if( pAsgnLeft )
                  {
                     hb_csEmitExpr( pAsgnLeft, yyc, HB_FALSE );
                     fprintf( yyc, " = " );
                  }
                  s_aRefShim = aShim;
                  s_iRefShimBase = iBase;
                  hb_csEmitExpr( pCall, yyc, HB_FALSE );
                  fprintf( yyc, ";\n" );
                  hb_csEmitShimWriteback( pHead, aShim, iBase, yyc, iIndent + 1 );
                  hb_csEmitIndent( yyc, iIndent );
                  fprintf( yyc, "}\n" );
                  hb_csEndShimBlock();
                  break;
               }
            }
         }
         hb_csEmitIndent( yyc, iIndent );
         hb_csEmitExpr( pStmtExpr, yyc, HB_FALSE );
         fprintf( yyc, ";\n" );
         break;
      }

      case HB_AST_RETURN:
         if( pNode->value.asReturn.pExpr && ! s_fVoidFunc )
         {
            /* Hoist a ref-passing call out of the return expression (C#
               ref invariance can't be shimmed mid-expression). */
            PHB_EXPR pHoist =
               hb_csFindShimCall( pNode->value.asReturn.pExpr, 0 );
            int iRetInd = iIndent;
            if( pHoist )
            {
               hb_csEmitIndent( yyc, iIndent );
               fprintf( yyc, "{\n" );
               hb_csBeginHoist( pHoist, yyc, iIndent + 1 );
               iRetInd = iIndent + 1;
            }
            hb_csEmitIndent( yyc, iRetInd );
            fprintf( yyc, "return " );
            hb_csEmitExpr( pNode->value.asReturn.pExpr, yyc, HB_FALSE );
            fprintf( yyc, ";\n" );
            s_pHoistCall = NULL;
            if( pHoist )
            {
               hb_csEmitIndent( yyc, iIndent );
               fprintf( yyc, "}\n" );
               hb_csEndShimBlock();
            }
         }
         else
         {
            hb_csEmitIndent( yyc, iIndent );
            fprintf( yyc, "return;\n" );
         }
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

            /* Record the local's real type for the ref-shim decision. */
            hb_csLocalTypeSet( pNode->value.asVar.szName, szType );

            /* `LOCAL name[dim1][dim2]...` — emitted by the grammar
               as an HB_AST_LOCAL with fArrayDim + pInit = ARGLIST of
               dims. Allocate a jagged dynamic[] sized by the first
               dim; inner dims get filled lazily (matching Harbour's
               runtime-grown array semantics). Same shape as the
               file-scope STATIC fArrayDim branch above. */
            if( pNode->value.asVar.fArrayDim &&
                pNode->value.asVar.pInit &&
                ( pNode->value.asVar.pInit->ExprType == HB_ET_ARGLIST ||
                  pNode->value.asVar.pInit->ExprType == HB_ET_LIST ) )
            {
               PHB_EXPR pDim =
                  pNode->value.asVar.pInit->value.asList.pExprList;
               hb_csEmitIndent( yyc, iIndent );
               fprintf( yyc, "dynamic[] %s = new dynamic[",
                        pNode->value.asVar.szName );
               hb_csEmitArrayDim( pDim, yyc );
               fprintf( yyc, "];\n" );
               break;
            }

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
               {
                  /* Point empty-hash literals in the init at the
                     declared key type (HASHN → decimal); coerce
                     initializers of int-typed locals. */
                  const char * szSavedKey = s_szHashKeyCs;
                  HB_BOOL fIntCast = szType &&
                     hb_stricmp( szType, "INTEGER" ) == 0 &&
                     hb_csNeedsIntCast( pNode->value.asVar.pInit );
                  s_szHashKeyCs = hb_csHashKeyCsFor( szType );
                  if( fIntCast )
                     fprintf( yyc, "(long)(" );
                  hb_csEmitExpr( pNode->value.asVar.pInit, yyc, HB_FALSE );
                  if( fIntCast )
                     fprintf( yyc, ")" );
                  s_szHashKeyCs = szSavedKey;
               }
               fprintf( yyc, ";\n" );
            }
            else
            {
               /* No initializer — always emit the local. Later FOREACH
                  / catch statements whose loop variable shadows this
                  local are handled in their own emit paths (HB_AST_FOREACH
                  renames via __hb_fe_<name> and assigns to the outer). */
               hb_csEmitIndent( yyc, iIndent );
               fprintf( yyc, "%s %s = default;\n", hb_csTypeMap( szType ),
                        pNode->value.asVar.szName );
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
            const char * szName = pNode->value.asVar.szName;

            /* PUBLIC name[size] — the field is declared in the owning
               .prg's Program-partial, so here we only emit the runtime
               array allocation. A same-name MEMVAR in this file is
               fine: its mangled field is skipped (see the Program-
               partial emit) and all references bind to the bare PUBLIC
               field. */
            if( pNode->type == HB_AST_PUBLIC &&
                pNode->value.asVar.fArrayDim &&
                pNode->value.asVar.pInit &&
                ( pNode->value.asVar.pInit->ExprType == HB_ET_ARGLIST ||
                  pNode->value.asVar.pInit->ExprType == HB_ET_LIST ) &&
                s_pRefTab && hb_refTabIsPublic( s_pRefTab, szName ) )
            {
               PHB_EXPR pDim = pNode->value.asVar.pInit->value.asList.pExprList;
               hb_csEmitIndent( yyc, iIndent );
               fprintf( yyc, "%s = new dynamic[", szName );
               hb_csEmitArrayDim( pDim, yyc );
               fprintf( yyc, "];\n" );
               break;
            }

            /* Plain `PUBLIC name` or `PUBLIC name := expr` on a PUBLIC
               that we registered in the reftab: emit as assignment to
               the shared Program-scope field (no local decl). */
            if( pNode->type == HB_AST_PUBLIC &&
                s_pRefTab && hb_refTabIsPublic( s_pRefTab, szName ) )
            {
               if( pNode->value.asVar.pInit )
               {
                  hb_csEmitIndent( yyc, iIndent );
                  fprintf( yyc, "%s = ", szName );
                  hb_csEmitExpr( pNode->value.asVar.pInit, yyc, HB_FALSE );
                  fprintf( yyc, ";\n" );
               }
               /* No init — the field is already `dynamic = default`. */
               break;
            }

            hb_csEmitIndent( yyc, iIndent );

            if( hb_csIsFileMemvar( szName ) )
            {
               /* PUBLIC/PRIVATE against a file-scope MEMVAR: assign to
                  the class-scope field rather than introducing a local. */
               fprintf( yyc, "%s_%s", s_szFileBase, szName );
               if( pNode->value.asVar.fArrayDim &&
                   pNode->value.asVar.pInit &&
                   ( pNode->value.asVar.pInit->ExprType == HB_ET_ARGLIST ||
                     pNode->value.asVar.pInit->ExprType == HB_ET_LIST ) )
               {
                  /* `name[size]` array-dim declaration: allocate a
                     runtime dynamic[]. Multi-dim (`a[i][j]`) takes only
                     the first dim — nested allocation would need a
                     loop and isn't yet emitted. */
                  PHB_EXPR pDim =
                     pNode->value.asVar.pInit->value.asList.pExprList;
                  fprintf( yyc, " = new dynamic[" );
                  hb_csEmitArrayDim( pDim, yyc );
                  fprintf( yyc, "]" );
               }
               else if( pNode->value.asVar.pInit )
               {
                  fprintf( yyc, " = " );
                  hb_csEmitExpr( pNode->value.asVar.pInit, yyc, HB_FALSE );
               }
               else
                  fprintf( yyc, " = default" );
               fprintf( yyc, ";\n" );
            }
            else
            {
               const char * szType = pNode->value.asVar.szAlias ?
                  pNode->value.asVar.szAlias :
                  hb_astInferType( szName, pNode->value.asVar.pInit );

               /* PUBLIC/PRIVATE with no matching MEMVAR → treat as
                  local for now. Harbour's memvar storage is global, but
                  we don't model that in C# without the explicit MEMVAR
                  hoist. Test16 exercises this path. */
               fprintf( yyc, "%s %s", hb_csTypeMap( szType ), szName );
               if( pNode->value.asVar.pInit )
               {
                  fprintf( yyc, " = " );
                  hb_csEmitExpr( pNode->value.asVar.pInit, yyc, HB_FALSE );
               }
               fprintf( yyc, ";\n" );
            }
         }
         break;

      case HB_AST_IF:
      {
         /* A ref-passing call in the main condition can't be shimmed in
            place (C# ref invariance), so hoist it: open a brace, compute
            the call into a temp, then run the whole if on that temp. Only
            the main condition is hoisted — ELSEIF conditions run
            conditionally and mustn't be pulled out. */
         PHB_EXPR pHoist =
            hb_csFindShimCall( pNode->value.asIf.pCondition, 0 );
         int iIfInd = iIndent;
         if( pHoist )
         {
            hb_csEmitIndent( yyc, iIndent );
            fprintf( yyc, "{\n" );
            hb_csBeginHoist( pHoist, yyc, iIndent + 1 );
            iIfInd = iIndent + 1;
         }
         hb_csEmitIndent( yyc, iIfInd );
         fprintf( yyc, "if (" );
         {
            HB_BOOL fWrap = hb_csConditionNeedsBoolUnwrap(
               pNode->value.asIf.pCondition );
            hb_csEmitExpr( pNode->value.asIf.pCondition, yyc, HB_FALSE );
            if( fWrap )
               fprintf( yyc, " == true" );
         }
         s_pHoistCall = NULL;   /* stop substituting before the bodies */
         fprintf( yyc, ")\n" );
         hb_csEmitIndent( yyc, iIfInd );
         fprintf( yyc, "{\n" );
         if( pNode->value.asIf.pThen )
            hb_csEmitBlock( pNode->value.asIf.pThen, yyc, iIfInd + 1 );
         hb_csEmitIndent( yyc, iIfInd );
         fprintf( yyc, "}\n" );

         /* ELSEIF chain */
         {
            PHB_AST_NODE pElseIf = pNode->value.asIf.pElseIfs;
            while( pElseIf )
            {
               HB_BOOL fWrap = hb_csConditionNeedsBoolUnwrap(
                  pElseIf->value.asElseIf.pCondition );
               hb_csEmitIndent( yyc, iIfInd );
               fprintf( yyc, "else if (" );
               hb_csEmitExpr( pElseIf->value.asElseIf.pCondition, yyc, HB_FALSE );
               if( fWrap )
                  fprintf( yyc, " == true" );
               fprintf( yyc, ")\n" );
               hb_csEmitIndent( yyc, iIfInd );
               fprintf( yyc, "{\n" );
               s_iLastLine = 0;
               if( pElseIf->value.asElseIf.pBody )
                  hb_csEmitBlock( pElseIf->value.asElseIf.pBody, yyc, iIfInd + 1 );
               hb_csEmitIndent( yyc, iIfInd );
               fprintf( yyc, "}\n" );
               pElseIf = pElseIf->pNext;
            }
         }

         if( pNode->value.asIf.pElse )
         {
            hb_csEmitIndent( yyc, iIfInd );
            fprintf( yyc, "else\n" );
            hb_csEmitIndent( yyc, iIfInd );
            fprintf( yyc, "{\n" );
            s_iLastLine = 0;
            hb_csEmitBlock( pNode->value.asIf.pElse, yyc, iIfInd + 1 );
            hb_csEmitIndent( yyc, iIfInd );
            fprintf( yyc, "}\n" );
         }
         if( pHoist )
         {
            hb_csEmitIndent( yyc, iIndent );
            fprintf( yyc, "}\n" );
            hb_csEndShimBlock();
         }
         break;
      }

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
            if( hb_csVarIsInteger( pNode->value.asFor.szVar ) &&
                hb_csNeedsIntCast( pNode->value.asFor.pStart ) )
            {
               fprintf( yyc, "(long)(" );
               hb_csEmitExpr( pNode->value.asFor.pStart, yyc, HB_FALSE );
               fprintf( yyc, ")" );
            }
            else
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
         {
            /* If the loop variable name matches a method-level local,
               emit with a renamed inner iterator and assign-to-outer to
               avoid CS0136 (shadowing a local in a parent scope). */
            PHB_EXPR pFEVar = pNode->value.asForEach.pVar;
            const char * szFEName = NULL;
            if( pFEVar )
            {
               PHB_EXPR pUnwrap = pFEVar;
               if( ( pUnwrap->ExprType == HB_ET_ARGLIST ||
                     pUnwrap->ExprType == HB_ET_LIST ) &&
                   pUnwrap->value.asList.pExprList )
                  pUnwrap = pUnwrap->value.asList.pExprList;
               if( pUnwrap->ExprType == HB_ET_VARIABLE ||
                   pUnwrap->ExprType == HB_ET_VARREF )
                  szFEName = pUnwrap->value.asSymbol.name;
            }
            if( szFEName && hb_csIsMethodLocal( szFEName ) )
            {
               hb_csEmitIndent( yyc, iIndent );
               fprintf( yyc, "foreach (dynamic __hb_fe_%s in ", szFEName );
               hb_csEmitExpr( pNode->value.asForEach.pEnum, yyc, HB_FALSE );
               if( pNode->value.asForEach.iDir < 0 )
                  fprintf( yyc, ".Reverse()" );
               fprintf( yyc, ")\n" );
               hb_csEmitIndent( yyc, iIndent );
               fprintf( yyc, "{\n" );
               hb_csEmitIndent( yyc, iIndent + 1 );
               fprintf( yyc, "%s = __hb_fe_%s;\n", szFEName, szFEName );
               if( pNode->value.asForEach.pBody )
                  hb_csEmitBlock( pNode->value.asForEach.pBody, yyc, iIndent + 1 );
               hb_csEmitIndent( yyc, iIndent );
               fprintf( yyc, "}\n" );
               break;
            }
         }
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
            const char * szRecVar = pNode->value.asSeq.szRecoverVar;
            HB_BOOL fShadow = szRecVar && hb_csIsMethodLocal( szRecVar );
            hb_csEmitIndent( yyc, iIndent );
            if( szRecVar )
               fprintf( yyc, "catch (Exception %s%s)\n",
                        fShadow ? "__hb_rec_" : "", szRecVar );
            else
               fprintf( yyc, "catch\n" );
            hb_csEmitIndent( yyc, iIndent );
            fprintf( yyc, "{\n" );
            s_iLastLine = 0;
            /* Shadow-case: assign the caught exception to the outer
               method-level local so its references resolve. */
            if( fShadow )
            {
               hb_csEmitIndent( yyc, iIndent + 1 );
               fprintf( yyc, "%s = __hb_rec_%s;\n", szRecVar, szRecVar );
            }
            hb_csEmitBlock( pNode->value.asSeq.pRecover, yyc, iIndent + 1 );
            hb_csEmitIndent( yyc, iIndent );
            fprintf( yyc, "}\n" );
         }
         else if( ! pNode->value.asSeq.pAlways )
         {
            /* BEGIN SEQUENCE / END SEQUENCE with neither RECOVER nor
               ALWAYS is a Harbour idiom that silently swallows any
               runtime error inside the body. C# requires every try to
               have a catch or finally, so emit an empty catch to keep
               semantics (errors absorbed) and satisfy the compiler.
               Warn so the source gets audited — most real uses turn
               out to be a RECOVER clause that the author forgot to
               write. */
            if( s_pCompCtx )
               hb_compGenWarning( s_pCompCtx, hb_comp_szWarnings, 'W',
                                  HB_COMP_WARN_MEANINGLESS,
                                  "BEGIN SEQUENCE with no RECOVER/ALWAYS — errors silently swallowed",
                                  NULL );
            hb_csEmitIndent( yyc, iIndent );
            fprintf( yyc, "catch\n" );
            hb_csEmitIndent( yyc, iIndent );
            fprintf( yyc, "{\n" );
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
         {
            const char * szText = pNode->value.asComment.szText;
            /* The by-ref convention marker (a block comment
               containing just `@`) tags a parameter for the reftab
               scanner at parse time. By the C# emit pass the reftab
               has already consumed the signal and the canonical
               signature carries `ref` on the right slots, so the
               comment has no residual purpose. Drop it rather than
               forwarding an orphan marker into the emitted method
               body where it's just noise. The .hb emitter keeps
               the marker because the Harbour baseline scanner
               re-reads it on round-trip. szText arrives with the
               block-comment delimiters intact; char-by-char test
               below so we don't embed them literally in the
               enclosing block comment. */
            if( szText[ 0 ] == '/' && szText[ 1 ] == '*' &&
                szText[ 2 ] == '@' && szText[ 3 ] == '*' &&
                szText[ 4 ] == '/' && szText[ 5 ] == '\0' )
               break;
            hb_csEmitIndent( yyc, iIndent );
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

            /* When gendefines has harvested this file's defines into
               a per-source Const class, the name is already declared
               there; a second inline const would collide. The runtime
               qualifier in hb_csEmitExpr rewrites references to use
               the Const class, so the inline form is redundant too. */
            if( hb_defineMapIsLocalOwned( szName ) )
               break;

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
   HB_BOOL               fDynamic;     /* class uses ::&(name) — needs DynamicObject */
   struct _HB_CS_CLASS * pNext;
} HB_CS_CLASS;

/* Check if an expression tree contains obj:&(name) macro send.
   Used to flag classes whose instances need DynamicObject. */
static HB_BOOL hb_csExprHasMacroSend( PHB_EXPR pExpr )
{
   if( ! pExpr ) return HB_FALSE;
   switch( pExpr->ExprType )
   {
      case HB_ET_SEND:
         if( pExpr->value.asMessage.pMessage &&
             pExpr->value.asMessage.pMessage->ExprType == HB_ET_MACRO )
            return HB_TRUE;
         if( hb_csExprHasMacroSend( pExpr->value.asMessage.pObject ) )
            return HB_TRUE;
         if( hb_csExprHasMacroSend( pExpr->value.asMessage.pParms ) )
            return HB_TRUE;
         break;
      case HB_ET_LIST: case HB_ET_ARGLIST: case HB_ET_MACROARGLIST:
      {
         PHB_EXPR p = pExpr->value.asList.pExprList;
         while( p ) { if( hb_csExprHasMacroSend( p ) ) return HB_TRUE; p = p->pNext; }
         break;
      }
      default:
         if( pExpr->ExprType >= HB_EO_ASSIGN && pExpr->ExprType <= HB_EO_PREDEC )
         {
            if( hb_csExprHasMacroSend( pExpr->value.asOperator.pLeft ) ) return HB_TRUE;
            if( hb_csExprHasMacroSend( pExpr->value.asOperator.pRight ) ) return HB_TRUE;
         }
         break;
   }
   return HB_FALSE;
}

static HB_BOOL hb_csBlockHasMacroSend( PHB_AST_NODE pBlock )
{
   PHB_AST_NODE pStmt;
   if( ! pBlock )
      return HB_FALSE;
   if( pBlock->type != HB_AST_BLOCK )
      return HB_FALSE;
   pStmt = pBlock->value.asBlock.pFirst;
   while( pStmt )
   {
      switch( pStmt->type )
      {
         case HB_AST_EXPRSTMT:
            if( hb_csExprHasMacroSend( pStmt->value.asExprStmt.pExpr ) )
               return HB_TRUE;
            break;
         case HB_AST_RETURN:
            if( hb_csExprHasMacroSend( pStmt->value.asReturn.pExpr ) )
               return HB_TRUE;
            break;
         case HB_AST_IF:
            if( hb_csExprHasMacroSend( pStmt->value.asIf.pCondition ) )
               return HB_TRUE;
            if( hb_csBlockHasMacroSend( pStmt->value.asIf.pThen ) )
               return HB_TRUE;
            break;
         case HB_AST_DOWHILE:
            if( hb_csBlockHasMacroSend( pStmt->value.asWhile.pBody ) )
               return HB_TRUE;
            break;
         case HB_AST_FOR:
            if( hb_csBlockHasMacroSend( pStmt->value.asFor.pBody ) )
               return HB_TRUE;
            break;
         case HB_AST_FOREACH:
            if( hb_csBlockHasMacroSend( pStmt->value.asForEach.pBody ) )
               return HB_TRUE;
            break;
         default:
            break;
      }
      pStmt = pStmt->pNext;
   }
   return HB_FALSE;
}

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
   HB_BOOL fMethodSpread = HB_FALSE;

   /* Run type propagation */
   if( pFunc->value.asFunc.pBody )
      szRetType = hb_astPropagate( pFunc->value.asFunc.pBody, s_pClassList, s_pRefTab, NULL,
                                   s_pCompCtx ? s_pCompCtx->currModule : NULL );

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
      const char * szClass =
         ( pFirstStmt && pFirstStmt->type == HB_AST_CLASSMETHOD )
            ? pFirstStmt->value.asClassMethod.szClass
            : NULL;
      /* The reftab keys methods as `<Class>::<Class>__<Method>` — its
         Pass-1 scan builds the key from the AST function's mangled
         name (hbreftab.c). Build the lookup key the same way: using
         the marker's *real* method name yields `<Class>::<Method>`,
         which misses the method's own entry and can case-insensitively
         collide with a same-named file-static free function. */
      const char * szKey =
         hb_refTabMethodKey( szClass, pFunc->value.asFunc.szName );
      hb_csLocalTypeReset();
      s_iShimDepth = 0;
      hb_strncpy( s_szCurrentFunc, szKey, sizeof( s_szCurrentFunc ) - 1 );
      hb_strncpy( s_szCurrentClass, szClass ? szClass : "",
                  sizeof( s_szCurrentClass ) - 1 );
   }
   s_pCurrentFuncNode = pFunc;
   /* Method-body STATICs are scoped to the method's function. */
   s_szStaticScope = pFunc->value.asFunc.szName;

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
      /* Method entries are keyed `<Class>::<Class>__<Method>`; build
         the key from the AST function's mangled name to match the
         reftab Pass-1 scan (see the szKey note above). */
      const char * szClassName =
         ( pFirstStmt && pFirstStmt->type == HB_AST_CLASSMETHOD )
            ? pFirstStmt->value.asClassMethod.szClass
            : NULL;
      char szKeyBuf[ 256 ];
      const char * szMethName =
         hb_refTabMethodKey( szClassName, pFunc->value.asFunc.szName );
      hb_strncpy( szKeyBuf, szMethName, sizeof( szKeyBuf ) - 1 );
      szMethName = szKeyBuf;

      {
         int iPos = 0;
         int iLastRef = -1;
         int k;

         for( k = 0; k < ( int ) pCompFunc->wParamCount; k++ )
            if( hb_refTabIsRef( s_pRefTab, szMethName, k ) )
               iLastRef = k;

         /* Variadic method (`METHOD Foo(...)`, or a body that uses
            `{ ... }` / bare `...`): widen the signature to `params
            dynamic[] hbva`, and re-bind any named params from the
            array in the body below. Skipped when a slot is by-ref —
            C# `params` can't combine with `ref`. Mirrors hb_csEmitFunc. */
         fMethodSpread =
            ( hb_refTabIsCalledVarargs( s_pRefTab, szMethName ) ||
              hb_refTabIsVariadic      ( s_pRefTab, szMethName ) ) &&
            iLastRef < 0;

         if( fMethodSpread )
            fprintf( yyc, "params dynamic[] hbva" );
         else while( pVar && nParam < pCompFunc->wParamCount )
         {
            HB_BOOL fThisRef     = hb_refTabIsRef( s_pRefTab, szMethName, iPos );
            HB_BOOL fThisNilable = hb_refTabIsNilable( s_pRefTab, szMethName, iPos );
            const HB_REFPARAM * pP =
               hb_refTabParam( s_pRefTab, szMethName, iPos );
            /* By-ref array slot the callee never reassigns → plain dynamic[]. */
            if( fThisRef && hb_csParamElidesArrayRef( szMethName, iPos ) )
               fThisRef = HB_FALSE;
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
   /* Variadic method widened to `params dynamic[] hbva`: re-bind any
      named params declared before `...` from the array so body
      references keep working. A pure `(...)` method has no named
      params, so this emits nothing. */
   if( fMethodSpread )
   {
      PHB_HVAR pSlot = pFunc->value.asFunc.pParams;
      int k = 0;
      while( pSlot && k < ( int ) pCompFunc->wParamCount )
      {
         hb_csEmitIndent( yyc, iIndent + 1 );
         fprintf( yyc, "dynamic %s = hbva.Length > %d ? hbva[%d] : null;\n",
                  pSlot->szName, k, k );
         pSlot = pSlot->pNext;
         k++;
      }
   }
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
   s_szCurrentClass[ 0 ] = '\0';
   s_pCurrentFuncNode = NULL;
   s_szStaticScope = NULL;
}

/* Emit a complete C# class definition */
static void hb_csEmitClass( HB_CS_CLASS * pClass, FILE * yyc )
{
   PHB_AST_NODE pClassNode = pClass->pClassNode;
   PHB_AST_NODE pMember;
   HB_CS_METHOD * pMethod;

   fprintf( yyc, "public class %s", pClassNode->value.asClass.szName );
   if( pClass->fDynamic && ! pClassNode->value.asClass.szParent )
      fprintf( yyc, " : HbDynamicObject" );
   else if( pClassNode->value.asClass.szParent )
      fprintf( yyc, " : %s", pClassNode->value.asClass.szParent );
   fprintf( yyc, "\n{\n" );

   /* Track the class for `Self:classvar` resolution. INLINE method
      bodies and INIT values below are translated textually before any
      hb_csEmitMethodBody runs, so set it here too — not just in the
      method-body emitter. */
   hb_strncpy( s_szCurrentClass, pClassNode->value.asClass.szName,
               sizeof( s_szCurrentClass ) - 1 );
   /* Mirrors the `: HbDynamicObject` condition above: only a dynamic
      class with no parent gets the DynamicObject base, so only there
      can `((dynamic)this)` reach a dictionary-backed member. */
   s_fCurrentClassDynamic =
      pClass->fDynamic && ! pClassNode->value.asClass.szParent;

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

         /* fField: this branch emits a plain field (no `{ get; set; }`).
            The post-switch terminator block appends `;` and uses the
            field syntax for init. ACCESS/ASSIGN and readonly stay as
            properties — they have method-like behaviour. */
         {
         HB_BOOL fField = HB_FALSE;

         switch( pMember->value.asClassData.iKind )
         {
            case HB_AST_DATA_CLASS:
               /* CLASSDATA — emit as plain static field. Auto-properties
                  ({ get; set; }) can't be passed by-ref (CS0206) which
                  bit objmeminit's bulk LoadStaticField(..., ref X) calls. */
               fField = HB_TRUE;
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
               /* Instance DATA — emit as plain field for the same
                  ref-passability reason as CLASSDATA. Readonly DATA
                  keeps the `{ get; }` shape: a `readonly` field would
                  reject all assignments outside the constructor, while
                  Harbour readonly only blocks the source `:=` syntax. */
               if( pMember->value.asClassData.fReadOnly )
                  fprintf( yyc, "%s %s %s { get; }",
                           szScope,
                           hb_csTypeMap( szType ),
                           pMember->value.asClassData.szName );
               else
               {
                  fField = HB_TRUE;
                  fprintf( yyc, "%s %s %s",
                           szScope,
                           hb_csTypeMap( szType ),
                           pMember->value.asClassData.szName );
               }
               break;
         }

         /* INIT value → default. Field branches need the `;` terminator;
            property branches end at `}` and only need a `;` after the
            init suffix. An `as int` member (source-declared) with a
            non-literal init — typically a `const decimal` define like
            DLGNONE — needs the (int) coercion, same as int-typed
            locals. */
         if( pMember->value.asClassData.szInit &&
             pMember->value.asClassData.iKind != HB_AST_DATA_ACCESS &&
             pMember->value.asClassData.iKind != HB_AST_DATA_ASSIGN )
         {
            const char * szInitCs =
               hb_csTranslateInit( pMember->value.asClassData.szInit );
            HB_BOOL fIntCast = HB_FALSE;
            if( szType && hb_stricmp( hb_csTypeMap( szType ), "long" ) == 0 )
            {
               const char * q = szInitCs;
               if( *q == '-' || *q == '+' )
                  q++;
               fIntCast = ! ( *q >= '0' && *q <= '9' );
               for( ; ! fIntCast && *q; q++ )
                  if( ! ( *q >= '0' && *q <= '9' ) )
                     fIntCast = HB_TRUE;
            }
            if( fIntCast )
               fprintf( yyc, " = (long)(%s);", szInitCs );
            else
               fprintf( yyc, " = %s;", szInitCs );
         }
         else if( fField )
            fprintf( yyc, ";" );

         fprintf( yyc, "\n" );
         }
      }
      pMember = pMember->pNext;
   }

   /* Blank line between properties and methods */
   fprintf( yyc, "\n" );

   /* Emit INLINE method declarations. These are METHOD ... INLINE (expr)
      entries from inside the CLASS...ENDCLASS block; no separate
      implementation appears later. Translate the captured expression
      text into C# and emit as expression-body or block-body depending
      on whether the source uses Harbour's comma-sequence form. */
   pMember = pClassNode->value.asClass.pMembers;
   while( pMember )
   {
      if( pMember->type == HB_AST_CLASSMETHOD &&
          pMember->value.asClassMethod.szInline )
      {
         const char * szScope = hb_csScopeStr(
            pMember->value.asClassMethod.iScope );
         const char * szName  = pMember->value.asClassMethod.szName;
         const char * szParms = pMember->value.asClassMethod.szParams;
         /* Translate first, then check top-level comma on the
            translated output — the raw text still has the outer
            parens that mask the real top-level of the expression.
            The param list rides along so the identifier rewriter
            leaves parameter references untouched. */
         const char * szExpr  =
            hb_csTranslateInline( pMember->value.asClassMethod.szInline,
                                  szParms );
         HB_BOOL      fBlock  = hb_csInlineHasTopLevelComma( szExpr );

         hb_csEmitIndent( yyc, 1 );
         fprintf( yyc, "%s dynamic %s(", szScope, szName );
         if( szParms && *szParms )
         {
            /* szParams is a comma-separated bare identifier list; emit
               each as `dynamic name = default`. */
            const char * q = szParms;
            HB_BOOL fFirst = HB_TRUE;
            while( *q )
            {
               const char * pStart;
               while( *q == ' ' || *q == ',' )
                  q++;
               if( ! *q )
                  break;
               pStart = q;
               while( *q && *q != ',' && *q != ' ' )
                  q++;
               if( ! fFirst )
                  fprintf( yyc, ", " );
               fprintf( yyc, "dynamic %.*s = default",
                        ( int ) ( q - pStart ), pStart );
               fFirst = HB_FALSE;
            }
         }
         fprintf( yyc, ")" );
         if( fBlock )
         {
            /* Sequence expression: Harbour's `(a, b, c)` evaluates a,
               b, c in order and returns c. Emit as a block that runs
               each as a statement and returns the last. The translator
               already gave us a comma-separated C# expression list. */
            fprintf( yyc, " { " );
            {
               const char * q = szExpr;
               int          depth = 0;
               HB_BOOL      fInStr = HB_FALSE;
               char         cStrQ = '\0';
               const char * pStart = q;
               HB_BOOL      fLast = HB_FALSE;
               while( ! fLast )
               {
                  char c = *q;
                  if( c == '\0' )
                     fLast = HB_TRUE;
                  if( fInStr )
                  {
                     if( c == cStrQ )
                        fInStr = HB_FALSE;
                  }
                  else if( c == '"' || c == '\'' )
                  {
                     fInStr = HB_TRUE;
                     cStrQ = c;
                  }
                  else if( c == '(' || c == '[' || c == '{' )
                     depth++;
                  else if( c == ')' || c == ']' || c == '}' )
                     depth--;
                  if( fLast || ( c == ',' && depth == 0 && ! fInStr ) )
                  {
                     int nLen = ( int ) ( q - pStart );
                     while( nLen > 0 && ( pStart[ 0 ] == ' ' || pStart[ 0 ] == '\t' ) )
                     {
                        pStart++;
                        nLen--;
                     }
                     if( fLast )
                        fprintf( yyc, "return %.*s; ", nLen, pStart );
                     else
                        fprintf( yyc, "%.*s; ", nLen, pStart );
                     pStart = q + 1;
                  }
                  if( ! fLast )
                     q++;
               }
            }
            fprintf( yyc, "}\n" );
         }
         else
            fprintf( yyc, " => %s;\n", szExpr );
      }
      pMember = pMember->pNext;
   }

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
   s_szCurrentClass[ 0 ] = '\0';
   s_fCurrentClassDynamic = HB_FALSE;
}

/* Emit a standalone function as a static method */
/* ================================================================
 * Hash key-type emit pre-pass
 *
 * File statics live outside any function's type env, so the Pass-2
 * observation in hbtypes.c can't retype their declarations. This
 * pre-pass walks every function body before anything emits:
 *   1. a subscript `FileStatic[idx]` with a known index type upgrades
 *      the static's registry entry (weak HASH → HASHN/HASHC);
 *   2. an assignment `FileStatic := SameFileFunc(...)` where the
 *      static ends up key-typed records a return-key override for the
 *      callee — consulted by hb_csEmitFunc so the factory's C# return
 *      type (and its returned weak-HASH locals) match the field.
 * Runs to a fixed point so evidence found late still feeds statics
 * declared early. Index types are resolved cheaply (literals and
 * Hungarian prefixes) — the full env isn't built here.
 * ================================================================ */
#define HB_CS_RETKEY_MAX 32
static struct { const char * szFunc; const char * szType; }
   s_aRetKeyOvr[ HB_CS_RETKEY_MAX ];
static int s_iRetKeyOvr = 0;

static const char * hb_csRetKeyOverride( const char * szFunc )
{
   int i;
   for( i = 0; i < s_iRetKeyOvr; i++ )
      if( hb_stricmp( s_aRetKeyOvr[ i ].szFunc, szFunc ) == 0 )
         return s_aRetKeyOvr[ i ].szType;
   return NULL;
}

static void hb_csRetKeySet( const char * szFunc, const char * szType )
{
   if( ! szFunc || hb_csRetKeyOverride( szFunc ) ||
       s_iRetKeyOvr >= HB_CS_RETKEY_MAX )
      return;
   s_aRetKeyOvr[ s_iRetKeyOvr ].szFunc = szFunc;
   s_aRetKeyOvr[ s_iRetKeyOvr ].szType = szType;
   s_iRetKeyOvr++;
}

/* Literal or Hungarian-prefix type of a subscript index — no env. */
static const char * hb_csCheapExprType( PHB_EXPR pExpr )
{
   if( ! pExpr )
      return NULL;
   if( pExpr->ExprType == HB_ET_NUMERIC )
      return "NUMERIC";
   if( pExpr->ExprType == HB_ET_STRING )
      return "STRING";
   if( pExpr->ExprType == HB_ET_VARIABLE )
      return hb_astInferType( pExpr->value.asSymbol.name, NULL );
   return NULL;
}

static void hb_csHashScanExpr( PHB_EXPR pExpr, HB_BOOL * pfChanged )
{
   if( ! pExpr )
      return;

   switch( pExpr->ExprType )
   {
      case HB_ET_ARRAYAT:
      {
         PHB_EXPR pBase = pExpr->value.asList.pExprList;
         PHB_EXPR pIdx  = pExpr->value.asList.pIndex;
         const char * szName = NULL;
         if( pBase && pBase->ExprType == HB_ET_VARIABLE )
            szName = pBase->value.asSymbol.name;
         else if( pBase && pBase->ExprType == HB_ET_ALIASVAR &&
                  pBase->value.asAlias.pVar &&
                  pBase->value.asAlias.pVar->ExprType == HB_ET_VARIABLE )
            /* implicit MEMVAR->name wrap of a file-static reference */
            szName = pBase->value.asAlias.pVar->value.asSymbol.name;
         if( szName && hb_csIsFileStatic( szName ) )
         {
            const char * szCur = hb_csFileStaticType( szName );
            if( ! szCur )
               szCur = hb_astInferType( szName, NULL );
            if( szCur && hb_stricmp( szCur, "HASH" ) == 0 )
            {
               const char * szIdx = hb_csCheapExprType( pIdx );
               const char * szKeyed = NULL;
               if( szIdx && hb_stricmp( szIdx, "NUMERIC" ) == 0 )
                  szKeyed = "HASHN";
               else if( szIdx && hb_stricmp( szIdx, "STRING" ) == 0 )
                  szKeyed = "HASHC";
               if( szKeyed )
               {
                  hb_csSetFileStaticType( szName, szKeyed );
                  *pfChanged = HB_TRUE;
               }
            }
         }
         hb_csHashScanExpr( pBase, pfChanged );
         hb_csHashScanExpr( pIdx,  pfChanged );
         break;
      }

      case HB_EO_ASSIGN:
      {
         PHB_EXPR pLhs = pExpr->value.asOperator.pLeft;
         PHB_EXPR pRhs = pExpr->value.asOperator.pRight;
         if( pLhs && pLhs->ExprType == HB_ET_VARIABLE &&
             pRhs && pRhs->ExprType == HB_ET_FUNCALL &&
             pRhs->value.asFunCall.pFunName &&
             pRhs->value.asFunCall.pFunName->ExprType == HB_ET_FUNNAME &&
             hb_csIsFileStatic( pLhs->value.asSymbol.name ) )
         {
            const char * szT =
               hb_csFileStaticType( pLhs->value.asSymbol.name );
            if( szT && hb_astIsHashFamily( szT ) &&
                hb_stricmp( szT, "HASH" ) != 0 )
               hb_csRetKeySet(
                  pRhs->value.asFunCall.pFunName->value.asSymbol.name,
                  szT );
         }
         hb_csHashScanExpr( pLhs, pfChanged );
         hb_csHashScanExpr( pRhs, pfChanged );
         break;
      }

      case HB_ET_FUNCALL:
         hb_csHashScanExpr( pExpr->value.asFunCall.pParms, pfChanged );
         break;

      case HB_ET_SEND:
         hb_csHashScanExpr( pExpr->value.asMessage.pObject, pfChanged );
         hb_csHashScanExpr( pExpr->value.asMessage.pParms,  pfChanged );
         break;

      case HB_ET_LIST:
      case HB_ET_ARGLIST:
      case HB_ET_MACROARGLIST:
      case HB_ET_ARRAY:
      case HB_ET_HASH:
      case HB_ET_IIF:
      {
         PHB_EXPR p = pExpr->value.asList.pExprList;
         while( p )
         {
            hb_csHashScanExpr( p, pfChanged );
            p = p->pNext;
         }
         break;
      }

      case HB_ET_CODEBLOCK:
      {
         PHB_EXPR p = pExpr->value.asCodeblock.pExprList;
         while( p )
         {
            hb_csHashScanExpr( p, pfChanged );
            p = p->pNext;
         }
         break;
      }

      default:
         if( pExpr->ExprType >= HB_EO_POSTINC )
         {
            hb_csHashScanExpr( pExpr->value.asOperator.pLeft,  pfChanged );
            hb_csHashScanExpr( pExpr->value.asOperator.pRight, pfChanged );
         }
         break;
   }
}

static void hb_csHashScanBlock( PHB_AST_NODE pNode, HB_BOOL * pfChanged )
{
   PHB_AST_NODE pStmt;

   if( ! pNode || pNode->type != HB_AST_BLOCK )
      return;

   pStmt = pNode->value.asBlock.pFirst;
   while( pStmt )
   {
      switch( pStmt->type )
      {
         case HB_AST_EXPRSTMT:
            hb_csHashScanExpr( pStmt->value.asExprStmt.pExpr, pfChanged );
            break;
         case HB_AST_RETURN:
            hb_csHashScanExpr( pStmt->value.asReturn.pExpr, pfChanged );
            break;
         case HB_AST_QOUT:
         case HB_AST_QQOUT:
            hb_csHashScanExpr( pStmt->value.asQOut.pExprList, pfChanged );
            break;
         case HB_AST_LOCAL:
         case HB_AST_STATIC:
         case HB_AST_PUBLIC:
         case HB_AST_PRIVATE:
            hb_csHashScanExpr( pStmt->value.asVar.pInit, pfChanged );
            break;
         case HB_AST_IF:
            hb_csHashScanExpr( pStmt->value.asIf.pCondition, pfChanged );
            hb_csHashScanBlock( pStmt->value.asIf.pThen, pfChanged );
            {
               PHB_AST_NODE p = pStmt->value.asIf.pElseIfs;
               while( p )
               {
                  hb_csHashScanExpr( p->value.asElseIf.pCondition, pfChanged );
                  hb_csHashScanBlock( p->value.asElseIf.pBody, pfChanged );
                  p = p->pNext;
               }
            }
            hb_csHashScanBlock( pStmt->value.asIf.pElse, pfChanged );
            break;
         case HB_AST_DOWHILE:
            hb_csHashScanExpr( pStmt->value.asWhile.pCondition, pfChanged );
            hb_csHashScanBlock( pStmt->value.asWhile.pBody, pfChanged );
            break;
         case HB_AST_FOR:
            hb_csHashScanExpr( pStmt->value.asFor.pStart, pfChanged );
            hb_csHashScanExpr( pStmt->value.asFor.pEnd,   pfChanged );
            hb_csHashScanExpr( pStmt->value.asFor.pStep,  pfChanged );
            hb_csHashScanBlock( pStmt->value.asFor.pBody, pfChanged );
            break;
         case HB_AST_FOREACH:
            hb_csHashScanExpr( pStmt->value.asForEach.pEnum, pfChanged );
            hb_csHashScanBlock( pStmt->value.asForEach.pBody, pfChanged );
            break;
         case HB_AST_DOCASE:
         {
            PHB_AST_NODE p = pStmt->value.asDoCase.pCases;
            while( p )
            {
               hb_csHashScanExpr( p->value.asCase.pCondition, pfChanged );
               hb_csHashScanBlock( p->value.asCase.pBody, pfChanged );
               p = p->pNext;
            }
            hb_csHashScanBlock( pStmt->value.asDoCase.pOtherwise, pfChanged );
            break;
         }
         case HB_AST_SWITCH:
         {
            PHB_AST_NODE p = pStmt->value.asSwitch.pCases;
            hb_csHashScanExpr( pStmt->value.asSwitch.pSwitch, pfChanged );
            while( p )
            {
               hb_csHashScanExpr( p->value.asCase.pCondition, pfChanged );
               hb_csHashScanBlock( p->value.asCase.pBody, pfChanged );
               p = p->pNext;
            }
            hb_csHashScanBlock( pStmt->value.asSwitch.pDefault, pfChanged );
            break;
         }
         case HB_AST_BEGINSEQ:
            hb_csHashScanBlock( pStmt->value.asSeq.pBody, pfChanged );
            hb_csHashScanBlock( pStmt->value.asSeq.pRecover, pfChanged );
            hb_csHashScanBlock( pStmt->value.asSeq.pAlways, pfChanged );
            break;
         case HB_AST_WITHOBJECT:
            hb_csHashScanExpr( pStmt->value.asWithObj.pObject, pfChanged );
            hb_csHashScanBlock( pStmt->value.asWithObj.pBody, pfChanged );
            break;
         default:
            break;
      }
      pStmt = pStmt->pNext;
   }
}

/* Retype the weak-HASH locals a function RETURNs when its return key
   type was overridden — the declaration must match the new signature.
   Nested RETURNs are walked; only bare `RETURN <var>` shapes count. */
static void hb_csCollectReturnVars( PHB_AST_NODE pNode,
                                    const char ** pszNames, int * piCount,
                                    int iMax );

static void hb_csAliasReturnedHashLocals( PHB_AST_NODE pBody,
                                          const char * szKeyed )
{
   const char * aszNames[ 16 ];
   int iCount = 0, i;
   PHB_AST_NODE pStmt;

   hb_csCollectReturnVars( pBody, aszNames, &iCount, 16 );
   if( ! iCount || ! pBody || pBody->type != HB_AST_BLOCK )
      return;

   pStmt = pBody->value.asBlock.pFirst;
   while( pStmt )
   {
      if( ( pStmt->type == HB_AST_LOCAL || pStmt->type == HB_AST_STATIC ) &&
          pStmt->value.asVar.szName )
      {
         for( i = 0; i < iCount; i++ )
         {
            if( hb_stricmp( pStmt->value.asVar.szName, aszNames[ i ] ) == 0 )
            {
               const char * szCur = pStmt->value.asVar.szAlias
                  ? pStmt->value.asVar.szAlias
                  : hb_astInferType( pStmt->value.asVar.szName,
                                     pStmt->value.asVar.pInit );
               if( szCur && hb_stricmp( szCur, "HASH" ) == 0 )
                  pStmt->value.asVar.szAlias = szKeyed;
            }
         }
      }
      pStmt = pStmt->pNext;
   }
}

static void hb_csCollectReturnVars( PHB_AST_NODE pNode,
                                    const char ** pszNames, int * piCount,
                                    int iMax )
{
   PHB_AST_NODE pStmt;

   if( ! pNode || pNode->type != HB_AST_BLOCK )
      return;

   pStmt = pNode->value.asBlock.pFirst;
   while( pStmt && *piCount < iMax )
   {
      switch( pStmt->type )
      {
         case HB_AST_RETURN:
            if( pStmt->value.asReturn.pExpr &&
                pStmt->value.asReturn.pExpr->ExprType == HB_ET_VARIABLE )
               pszNames[ ( *piCount )++ ] =
                  pStmt->value.asReturn.pExpr->value.asSymbol.name;
            break;
         case HB_AST_IF:
            hb_csCollectReturnVars( pStmt->value.asIf.pThen, pszNames, piCount, iMax );
            {
               PHB_AST_NODE p = pStmt->value.asIf.pElseIfs;
               while( p )
               {
                  hb_csCollectReturnVars( p->value.asElseIf.pBody, pszNames, piCount, iMax );
                  p = p->pNext;
               }
            }
            hb_csCollectReturnVars( pStmt->value.asIf.pElse, pszNames, piCount, iMax );
            break;
         case HB_AST_DOWHILE:
            hb_csCollectReturnVars( pStmt->value.asWhile.pBody, pszNames, piCount, iMax );
            break;
         case HB_AST_FOR:
            hb_csCollectReturnVars( pStmt->value.asFor.pBody, pszNames, piCount, iMax );
            break;
         case HB_AST_FOREACH:
            hb_csCollectReturnVars( pStmt->value.asForEach.pBody, pszNames, piCount, iMax );
            break;
         case HB_AST_DOCASE:
         {
            PHB_AST_NODE p = pStmt->value.asDoCase.pCases;
            while( p )
            {
               hb_csCollectReturnVars( p->value.asCase.pBody, pszNames, piCount, iMax );
               p = p->pNext;
            }
            hb_csCollectReturnVars( pStmt->value.asDoCase.pOtherwise, pszNames, piCount, iMax );
            break;
         }
         case HB_AST_SWITCH:
         {
            PHB_AST_NODE p = pStmt->value.asSwitch.pCases;
            while( p )
            {
               hb_csCollectReturnVars( p->value.asCase.pBody, pszNames, piCount, iMax );
               p = p->pNext;
            }
            hb_csCollectReturnVars( pStmt->value.asSwitch.pDefault, pszNames, piCount, iMax );
            break;
         }
         case HB_AST_BEGINSEQ:
            hb_csCollectReturnVars( pStmt->value.asSeq.pBody, pszNames, piCount, iMax );
            hb_csCollectReturnVars( pStmt->value.asSeq.pRecover, pszNames, piCount, iMax );
            hb_csCollectReturnVars( pStmt->value.asSeq.pAlways, pszNames, piCount, iMax );
            break;
         default:
            break;
      }
      pStmt = pStmt->pNext;
   }
}

static void hb_csEmitFunc( PHB_AST_NODE pFunc, PHB_HFUNC pCompFunc,
                            FILE * yyc, int iIndent )
{
   PHB_HVAR pVar;
   HB_USHORT nParam = 0;
   const char * szRetType = NULL;
   HB_BOOL fIsMain = HB_FALSE;

   /* Run type propagation */
   if( pFunc->value.asFunc.pBody )
      szRetType = hb_astPropagate( pFunc->value.asFunc.pBody, s_pClassList, s_pRefTab, NULL,
                                   s_pCompCtx ? s_pCompCtx->currModule : NULL );

   /* Return-key override from the hash pre-pass: this function's
      result lands in a key-typed hash static (e.g. a CreateLangHash-
      style factory whose own keys are macro-built and untypeable).
      The signature and the returned weak-HASH locals both adopt the
      assignment target's key type so the C# assignment compiles. */
   if( szRetType && hb_stricmp( szRetType, "HASH" ) == 0 )
   {
      const char * szOvr =
         hb_csRetKeyOverride( pFunc->value.asFunc.szName );
      if( szOvr )
      {
         szRetType = szOvr;
         hb_csAliasReturnedHashLocals( pFunc->value.asFunc.pBody, szOvr );
      }
   }

   /* Detect Main entry point */
   if( hb_stricmp( pFunc->value.asFunc.szName, "Main" ) == 0 )
      fIsMain = HB_TRUE;

   /* Track current function for nilable-parameter lookups in IF/IIF
      condition emission. A file-static gets its <FileBase>::<Name>
      reftab key so it doesn't read a same-named global's entry. */
   {
      char szKeyBuf[ 256 ];
      hb_csLocalTypeReset();
      s_iShimDepth = 0;
      hb_strncpy( s_szCurrentFunc,
                  hb_csFuncRefKey( pFunc->value.asFunc.szName,
                                   szKeyBuf, sizeof( szKeyBuf ) ),
                  sizeof( s_szCurrentFunc ) - 1 );
   }
   s_pCurrentFuncNode = pFunc;
   /* Static-registry scope: this function's own STATICs resolve ahead
      of the file-scope ones while its body emits. */
   s_szStaticScope = ( pFunc == s_pFileDeclFunc )
      ? NULL : pFunc->value.asFunc.szName;

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

   {
      /* Mangle STATIC FUNCTION / STATIC PROCEDURE names with the file
         base so two files each declaring the same static name don't
         collide under the merged partial class Program. Main is always
         global. Call sites in this file resolve the same mangled name
         via hb_csMangleStaticFunc at HB_ET_FUNCALL emit time. */
      char szMangledBuf[ 256 ];
      const char * szEmitName = fIsMain ? "Main"
         : hb_csMangleStaticFunc( pFunc->value.asFunc.szName,
                                   szMangledBuf, sizeof( szMangledBuf ) );
      fprintf( yyc, " %s(", szEmitName );
   }

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
      char szFnKeyBuf[ 256 ];
      const char * szFnName = fIsMain ? "Main"
         : hb_csFuncRefKey( pFunc->value.asFunc.szName,
                            szFnKeyBuf, sizeof( szFnKeyBuf ) );
      int iPos = 0;
      int iLastRef = -1;
      HB_BOOL fWantDefaults = ! fIsMain;
      /* A callee reached via `...` spread from a codeblock gets its
         signature widened to `params dynamic[] hbva`. Same for a
         self-declared variadic: `function Foo(...)` (with body using
         `{ ... }` or bare `...`) that has no named params. The
         original param names (if any) are re-bound from the array
         inside the body so existing body references keep working.
         Skipped when any slot is by-ref (C# `params` can't combine
         with `ref`) — those calls fall back to reflection. */
      HB_BOOL fSpread = ! fIsMain &&
                        ( hb_refTabIsCalledVarargs( s_pRefTab, szFnName ) ||
                          hb_refTabIsVariadic      ( s_pRefTab, szFnName ) );

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
         /* A by-ref array slot the callee never reassigns emits as a plain
            `dynamic[]` — element mutation propagates without `ref`. */
         if( fThisRef && hb_csParamElidesArrayRef( szFnName, iPos ) )
            fThisRef = HB_FALSE;
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

         /* Register the param in the local-type map (reset just above,
            before the body walk) so member-typed emission — the (long)
            write coercion into AS INTEGER members, ORM field lookups —
            sees a class-typed receiver arriving as a parameter, e.g.
            SetTableHeader(oTransaction, ...). Without this only
            self-constructed objects were typed. */
         hb_csLocalTypeSet( pVar->szName, szSlotType );

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
       ( hb_refTabIsCalledVarargs( s_pRefTab, pFunc->value.asFunc.szName ) ||
         hb_refTabIsVariadic      ( s_pRefTab, pFunc->value.asFunc.szName ) ) )
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

   /* If the canonical signature has any ref param, emit a short
      overload that takes only the prefix of params preceding the
      first ref, all defaulted to null. The Harbour idiom is
      `FUNCTION Foo(a, @b) hb_default(@b, 0)` (the `@` on b marks it
      by-ref) where most callers pass Foo(a) -- leaving b NIL
      inside the callee -- and only a few pass Foo(a, @b). In C#,
      ref params are required; the short overload lets the
      majority of sites compile by supplying dummy storage for the
      tail and forwarding to the canonical. When the first ref is
      param 0 the prefix is empty and the short overload is
      parameterless -- exactly the `GetQty()` idiom. No-`@` callers
      with arity between iFirstRef and wParamCount still fail (they
      would need an even shorter overload per arity, skipped here
      to avoid method-bloat). Main and spread-receivers don't
      participate.

      We only emit when the reftab's observed call-arity bitmap shows
      at least one caller actually passed fewer than iFirstRef args.
      Without the guard, every ref-taking function gets a dead short
      overload (e.g. LoadAFlag, whose callers all pass the full
      arity). The bitmap is populated during the -GF scan pass; a
      transpile that skipped scanning falls back to always emitting,
      since bitCallArities == 0 can't distinguish "never called" from
      "never scanned". */
   if( ! fIsMain )
   {
      char szFnKeyBuf[ 256 ];
      const char * szFnName = hb_csFuncRefKey( pFunc->value.asFunc.szName,
                                               szFnKeyBuf, sizeof( szFnKeyBuf ) );
      HB_BOOL fSpreadCallee = hb_refTabIsCalledVarargs( s_pRefTab, szFnName );
      int iFirstRef = -1;
      int iMax = ( int ) pCompFunc->wParamCount;
      int k;
      for( k = 0; k < iMax; k++ )
      {
         if( hb_csParamEmitsRef( szFnName, k ) )
         {
            iFirstRef = k;
            break;
         }
      }
      {
         HB_U64 bitArities = hb_refTabCallArities( s_pRefTab, szFnName );
         /* Short overload declares iFirstRef params, each with `=
            default`, so it accepts every arity from 0 through
            iFirstRef inclusive. Mask covers bits 0..iFirstRef. When
            the reftab recorded any such arity, at least one caller
            needs the short overload; otherwise skip it to avoid
            emitting dead methods. */
         HB_U64 shortMask = iFirstRef >= 0
            ? ( ( ( HB_U64 ) 1 ) << ( iFirstRef + 1 ) ) - 1 : 0;
         HB_BOOL fHasShortCaller =
            bitArities == 0 || ( bitArities & shortMask ) != 0;
      if( iFirstRef >= 0 && ! fSpreadCallee && fHasShortCaller )
      {
         char szMangledBuf[ 256 ];
         /* The emitted name mangles the bare name; szFnName may be the
            <FileBase>::<Name> reftab key, which must not reach here. */
         const char * szEmitName =
            hb_csMangleStaticFunc( pFunc->value.asFunc.szName,
                                   szMangledBuf, sizeof( szMangledBuf ) );
         PHB_HVAR pP;

         fprintf( yyc, "\n" );
         hb_csEmitIndent( yyc, iIndent );
         fprintf( yyc, "public static " );
         fprintf( yyc, "%s",
                  pFunc->value.asFunc.fProcedure ? "void" : "dynamic" );
         fprintf( yyc, " %s(", szEmitName );
         pP = pFunc->value.asFunc.pParams;
         for( k = 0; pP && k < iFirstRef; k++ )
         {
            /* Match the canonical signature's typing per slot:
               reftab's refined type first, then Hungarian inference,
               then dynamic fallback. Using `dynamic %s = null` here
               (the original emission) collapsed every typed slot back
               to dynamic, surprising readers of the generated code
               and hiding type mismatches the canonical would have
               caught. */
            const HB_REFPARAM * pRefSlot =
               hb_refTabParam( s_pRefTab, szFnName, k );
            const char * szSlotType = NULL;
            const char * szCsType;
            if( pRefSlot && pRefSlot->szType &&
                hb_stricmp( pRefSlot->szType, "USUAL" ) != 0 )
               szSlotType = pRefSlot->szType;
            if( ! szSlotType )
               szSlotType = hb_astInferType( pP->szName, NULL );
            szCsType = hb_csTypeMap( szSlotType );
            if( k > 0 )
               fprintf( yyc, ", " );
            fprintf( yyc, "%s %s = default", szCsType, pP->szName );
            pP = pP->pNext;
         }
         fprintf( yyc, ")\n" );
         hb_csEmitIndent( yyc, iIndent );
         fprintf( yyc, "{\n" );
         /* Dummy locals for every slot the short overload elides.
            Typed to match the canonical signature so `ref _argK`
            binds — C# `ref` is invariant, a `ref dynamic` cell
            won't satisfy a `ref bool` parameter. Same slot-type
            lookup the canonical-emit loop used. */
         {
            PHB_HVAR pQ = pFunc->value.asFunc.pParams;
            int j;
            for( j = 0; pQ && j < iFirstRef; j++ )
               pQ = pQ->pNext;
            for( k = iFirstRef; k < iMax; k++ )
            {
               const HB_REFPARAM * pP =
                  hb_refTabParam( s_pRefTab, szFnName, k );
               const char * szSlotType = NULL;
               if( pP && pP->szType &&
                   hb_stricmp( pP->szType, "USUAL" ) != 0 )
                  szSlotType = pP->szType;
               if( ! szSlotType && pQ )
                  szSlotType = hb_astInferType( pQ->szName, NULL );
               hb_csEmitIndent( yyc, iIndent + 1 );
               fprintf( yyc, "%s _arg%d = default;\n",
                        hb_csTypeMap( szSlotType ), k );
               if( pQ )
                  pQ = pQ->pNext;
            }
         }
         hb_csEmitIndent( yyc, iIndent + 1 );
         if( ! pFunc->value.asFunc.fProcedure )
            fprintf( yyc, "return " );
         fprintf( yyc, "%s(", szEmitName );
         pP = pFunc->value.asFunc.pParams;
         for( k = 0; pP && k < iFirstRef; k++ )
         {
            if( k > 0 )
               fprintf( yyc, ", " );
            fprintf( yyc, "%s", pP->szName );
            pP = pP->pNext;
         }
         for( k = iFirstRef; k < iMax; k++ )
         {
            if( k > 0 )
               fprintf( yyc, ", " );
            if( hb_csParamEmitsRef( szFnName, k ) )
               fprintf( yyc, "ref _arg%d", k );
            else
               fprintf( yyc, "_arg%d", k );
         }
         fprintf( yyc, ");\n" );
         hb_csEmitIndent( yyc, iIndent );
         fprintf( yyc, "}\n" );
      }
      }
   }

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

   /* Expose pComp to deeply-nested static emitters so they can call
      hb_compGenError without each receiving HB_COMP_DECL explicitly. */
   s_pCompCtx = HB_COMP_PARAM;

   /* Build output filename with .cs extension, and capture the base
      name (without path or extension) for use as a STATIC-var name
      prefix. See hb_csIsFileStatic for the collision rationale. */
   hb_csResetFileStatics();
   /* The head of ast.pFuncList is the file-decl container (includes,
      classes, file-level STATICs live in its body). STATICs found
      there are file-scope; STATICs in any later function are private
      to that function. */
   s_pFileDeclFunc = HB_COMP_PARAM->ast.pFuncList;
   {
      PHB_FNAME pOut = hb_fsFNameSplit( HB_COMP_PARAM->szFile );
      pFileName->szExtension = ".cs";
      if( HB_COMP_PARAM->pOutPath && HB_COMP_PARAM->pOutPath->szPath )
         pFileName->szPath = HB_COMP_PARAM->pOutPath->szPath;
      else if( pOut->szPath )
         pFileName->szPath = pOut->szPath;
      hb_fsFNameMerge( szFileName, pFileName );
      if( pOut->szName )
      {
         /* Project may override with a curated CamelCase via
            --filename-casing. Falls back to the on-disk stem casing. */
         const char * szCanon = hb_fileCaseLookup( pOut->szName );
         hb_strncpy( s_szFileBase,
                     szCanon ? szCanon : pOut->szName,
                     sizeof( s_szFileBase ) - 1 );
      }
      /* Hand the current file's full basename (stem + original
         extension) to the defines map so per-file local rows can
         shadow the globals during lookup. */
      if( pOut->szName )
      {
         char szBasename[ HB_PATH_MAX ];
         hb_snprintf( szBasename, sizeof( szBasename ), "%s%s",
                      pOut->szName,
                      pOut->szExtension ? pOut->szExtension : ".prg" );
         hb_defineMapSetCurrentFile( szBasename );
      }
      else
         hb_defineMapSetCurrentFile( NULL );
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

   /* Publish the reftab to hb_astInferFromPrefix so `o<ClassName>` /
      `so<ClassName>` variable names resolve to the specific class
      during emit too — nested hb_astPropagate calls save/restore
      around this, so the reftab stays live for the whole pass. */
   hb_astSetPrefixReftab( s_pRefTab );

   /* Collect STATIC function/procedure names so intra-file call sites
      can be mangled consistently with the declaration. Cross-file
      callers can't reach these (that's the language rule) so we don't
      need to track them in hbreftab. */
   {
      PHB_AST_NODE pF = HB_COMP_PARAM->ast.pFuncList;
      while( pF )
      {
         if( pF->type == HB_AST_FUNCTION &&
             pF->value.asFunc.szName &&
             ( pF->value.asFunc.cScope & HB_FS_STATIC ) != 0 )
            hb_csAddFileStaticFunc( pF->value.asFunc.szName );
         pF = pF->pNext;
      }
   }

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
               pClass->fDynamic = HB_FALSE;
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
                  {
                     hb_csAddMethod( pClass, pFunc, pCompFunc );
                     if( ! pClass->fDynamic &&
                         pFunc->value.asFunc.pBody &&
                         hb_csBlockHasMacroSend( pFunc->value.asFunc.pBody ) )
                        pClass->fDynamic = HB_TRUE;
                  }
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
   fprintf( yyc, "using static HbRuntime;\n" );
   /* `using static Program;` pulls the merged partial-class static
      members into file scope so class methods (which live in their
      own `public class Foo {}` and don't inherit Program's member
      lookup) can call cross-file free functions like SetErrorCode,
      ConstructORMTable, hb_eol, etc. without qualification. Self-
      references from the Program partial resolve directly, so no
      ambiguity. Ambiguity between Program and HbRuntime members
      (same bare name in both) would surface as CS0229 — none in the
      current corpus but worth noting if it happens later. */
   fprintf( yyc, "using static Program;\n" );
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

   /* Pre-pass: register every file-scope STATIC name into the
      csFileStatic table *before* any class method body emits. The
      main walker (below — the one that also emits the
      `static dynamic <file>_<name>` class fields) runs AFTER the
      classes are emitted, so method-local statics weren't in the
      registry yet when their body references were walked by
      HB_ET_VARIABLE. Body refs then fell through to the bare-name
      branch while the field declaration ultimately emitted with the
      `<file>_<name>` mangling, and the two stopped matching.
      MEMVAR intentionally stays out of this pre-pass: the main
      walker's MEMVAR field-emit is guarded by
      `! hb_csIsFileMemvar(name)` as its dedup check, so a pre-pass
      registration would make the main walker think the field was
      already declared and skip it (test16 exercises this). MEMVAR
      body-ref ordering isn't affected because MEMVARs are captured
      at the function-body's MEMVAR directive, not at method-local
      statics. */
   {
      PHB_AST_NODE pF = HB_COMP_PARAM->ast.pFuncList;
      while( pF )
      {
         if( pF->type == HB_AST_FUNCTION && pF->value.asFunc.pBody &&
             pF->value.asFunc.pBody->type == HB_AST_BLOCK )
         {
            PHB_AST_NODE pStmt = pF->value.asFunc.pBody->value.asBlock.pFirst;
            /* STATICs in the file-decl head are file-scope (owner
               NULL); those inside a real function body are private to
               that function per Harbour's rule. */
            const char * szOwner = ( pF == s_pFileDeclFunc )
               ? NULL : pF->value.asFunc.szName;
            s_szStaticScope = szOwner;
            while( pStmt )
            {
               if( pStmt->type == HB_AST_STATIC )
               {
                  hb_csAddFileStatic( pStmt->value.asVar.szName, szOwner );
                  /* Seed hash-family statics with their declaration-
                     derived type so the key-type pre-pass below has a
                     starting point to upgrade. Other types keep the
                     lazy set-at-field-emit behavior. */
                  {
                     const char * szT = hb_astInferType(
                        pStmt->value.asVar.szName,
                        pStmt->value.asVar.pInit );
                     if( hb_astIsHashFamily( szT ) )
                        hb_csSetFileStaticType(
                           pStmt->value.asVar.szName, szT );
                  }
               }
               pStmt = pStmt->pNext;
            }
         }
         pF = pF->pNext;
      }
      s_szStaticScope = NULL;
   }

   /* Hash key-type pre-pass: observe subscripts on hash statics and
      factory-assignment shapes to a fixed point, before anything
      emits. See the block comment at hb_csHashScanExpr. */
   s_iRetKeyOvr = 0;
   {
      HB_BOOL fChg;
      do
      {
         PHB_AST_NODE pF = HB_COMP_PARAM->ast.pFuncList;
         fChg = HB_FALSE;
         while( pF )
         {
            if( pF->type == HB_AST_FUNCTION )
            {
               s_szStaticScope = ( pF == s_pFileDeclFunc )
                  ? NULL : pF->value.asFunc.szName;
               hb_csHashScanBlock( pF->value.asFunc.pBody, &fChg );
            }
            pF = pF->pNext;
         }
      }
      while( fChg );
      s_szStaticScope = NULL;
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

   /* Force the Program partial to open when there are file-scope
      statics to declare, even if every function in the file is a
      class method (fHasStandalone would otherwise stay false and
      the partial block — which is where the
      `static <type> <file>_<name>` field declarations go — never
      opens). Happens in library files like ccio.prg where every
      function is `METHOD ... CLASS ...` and a `static aErrs := {...}`
      inside a method still needs a class-level field to hold it.
      Harmless when no file-statics exist — the pre-pass leaves
      s_iFileStaticCount at 0 and this short-circuits. */
   if( s_iFileStaticCount > 0 )
      fHasStandalone = HB_TRUE;

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
               const char * szOwner = ( pF == s_pFileDeclFunc )
                  ? NULL : pF->value.asFunc.szName;
               s_szStaticScope = szOwner;
               while( pStmt )
               {
                  if( pStmt->type == HB_AST_STATIC )
                  {
                     char szFld[ 256 ];
                     const char * szType = pStmt->value.asVar.szAlias ?
                        pStmt->value.asVar.szAlias :
                        hb_astInferType( pStmt->value.asVar.szName,
                                          pStmt->value.asVar.pInit );
                     HB_BOOL fArrayDim = pStmt->value.asVar.fArrayDim &&
                        pStmt->value.asVar.pInit &&
                        ( pStmt->value.asVar.pInit->ExprType == HB_ET_ARGLIST ||
                          pStmt->value.asVar.pInit->ExprType == HB_ET_LIST );
                     hb_csAddFileStatic( pStmt->value.asVar.szName, szOwner );
                     hb_csStaticFieldName(
                        hb_csFileStaticIdx( pStmt->value.asVar.szName ),
                        szFld, sizeof( szFld ) );
                     hb_csEmitIndent( yyc, 1 );
                     if( fArrayDim )
                     {
                        /* `STATIC name[dim1][dim2]...` — allocate a
                           jagged dynamic[] sized by the first dim.
                           Inner dims get nulls until assigned, matching
                           Harbour's semantics (callers can grow them
                           via ASize or by direct index assignment into
                           a resizable list). szType from Hungarian is
                           usually "ARRAY"; force `dynamic[]` so the
                           C# field type matches `new dynamic[N]`.
                           `public static` (not just `static`) so
                           methods of sibling classes in the same file
                           — which are emitted outside the Program
                           partial — can qualify as `Program.<field>`.
                           Harbour STATIC is file-scope private, but
                           since the transpiler's file-base mangling
                           already makes the name unique across the
                           whole project, widening to `public` only
                           affects IDE-level lookup and costs nothing
                           at runtime. */
                        PHB_EXPR pDim =
                           pStmt->value.asVar.pInit->value.asList.pExprList;
                        fprintf( yyc, "public static dynamic[] %s = new dynamic[",
                                 szFld );
                        hb_csEmitArrayDim( pDim, yyc );
                        fprintf( yyc, "];\n" );
                     }
                     else
                     {
                        /* Strict-typed emit: do NOT silently rewrite
                           NIL inits to `default` for value types. A
                           Hungarian-typed value-type STATIC initialised
                           to NIL produces `decimal x = null` which C#
                           rejects (CS0037). Each such site is a source
                           bug to be cleaned up — pick a real default
                           (`:= 0`, `:= .F.`) or drop the init. */
                        /* The hash key-type pre-pass may have upgraded
                           this static's registry entry (HASH → HASHN /
                           HASHC) from subscript usage inside function
                           bodies — that evidence outranks the weak
                           declaration-derived HASH. */
                        {
                           const char * szReg = hb_csFileStaticType(
                              pStmt->value.asVar.szName );
                           if( szReg && szType &&
                               hb_stricmp( szType, "HASH" ) == 0 &&
                               hb_astIsHashFamily( szReg ) &&
                               hb_stricmp( szReg, "HASH" ) != 0 )
                              szType = szReg;
                        }
                        hb_csSetFileStaticType(
                           pStmt->value.asVar.szName, szType );
                        fprintf( yyc, "public static %s %s",
                                 hb_csTypeMap( szType ), szFld );
                        if( pStmt->value.asVar.pInit )
                        {
                           const char * szSavedKey = s_szHashKeyCs;
                           fprintf( yyc, " = " );
                           /* The field is indented 4 spaces (class body
                              level 1); a complex hash/array literal's
                              children should sit one level deeper at 8.
                              hb_csEmitExpr reads this and resets it on
                              the way out. */
                           s_iExprIndent = 8;
                           s_szHashKeyCs = hb_csHashKeyCsFor( szType );
                           hb_csEmitExpr( pStmt->value.asVar.pInit, yyc, HB_FALSE );
                           s_szHashKeyCs = szSavedKey;
                           s_iExprIndent = 0;
                        }
                        fprintf( yyc, ";\n" );
                     }
                  }
                  else if( pStmt->type == HB_AST_MEMVAR )
                  {
                     /* File-scope MEMVAR: emit a shared `dynamic` field
                        under the partial class. Mangled with file base
                        to avoid CS0102 when another .prg also MEMVARs
                        the same name. Skipped when the name is a
                        registered PUBLIC — the PUBLIC owner emits a
                        bare `public static dynamic <name>;` field and
                        every file's references bind to that instead
                        (see HB_ET_VARIABLE emission). Mangled MEMVAR
                        fields shadowed by PUBLICs would leave half the
                        references undefined. */
                     const char * szMName = pStmt->value.asVar.szName;
                     if( ! hb_csIsFileMemvar( szMName ) &&
                         ! ( s_pRefTab && hb_refTabIsPublic( s_pRefTab, szMName ) ) )
                     {
                        hb_csAddFileMemvar( szMName );
                        hb_csEmitIndent( yyc, 1 );
                        /* `public static` so sibling-class methods
                           can qualify as `Program.<field>`; see the
                           STATIC emit above for rationale. */
                        fprintf( yyc, "public static dynamic %s_%s;\n",
                                 s_szFileBase, szMName );
                     }
                  }
                  else if( pStmt->type == HB_AST_PUBLIC )
                  {
                     /* PUBLIC variable declaration. If this file is the
                        registered owner in the reftab (first file to
                        declare wins), emit a Program-partial static
                        field using the source's original-case name so
                        references in THIS file and across files all
                        resolve. We dedupe per-file via
                        hb_csIsFileMemvar (reusing the file-memvar set —
                        PUBLIC and MEMVAR never want two fields for the
                        same name). */
                     const char * szPName = pStmt->value.asVar.szName;
                     if( s_pRefTab && szPName &&
                         ! hb_csIsFileMemvar( szPName ) )
                     {
                        const char * szOwner = hb_refTabPublicOwner(
                           s_pRefTab, szPName );
                        if( szOwner && s_szFileBase &&
                            hb_stricmp( szOwner, s_szFileBase ) == 0 )
                        {
                           /* Sized-array form (`PUBLIC name[size]`) —
                              emit as `dynamic[]` so cross-file callers
                              passing `ref aXxx` to a callee declared
                              `ref dynamic[]` (the LoadAFlag shape) bind
                              cleanly. The runtime allocation is still
                              emitted at the PUBLIC statement site. */
                           HB_BOOL fArr = pStmt->value.asVar.fArrayDim;
                           hb_csAddFileMemvar( szPName );
                           hb_csEmitIndent( yyc, 1 );
                           fprintf( yyc, "public static dynamic%s %s;\n",
                                    fArr ? "[]" : "", szPName );
                        }
                     }
                  }
                  pStmt = pStmt->pNext;
               }
            }
            pF = pF->pNext;
         }
         s_szStaticScope = NULL;
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
   hb_astSetPrefixReftab( NULL );
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
