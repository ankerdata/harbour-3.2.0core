/*
 * Harbour Transpiler - Type inference from Hungarian notation and initializers
 *
 * Infers types from three sources in priority order:
 * 1. Initializer expression type (most reliable)
 * 2. Hungarian notation prefix (developer convention)
 * 3. Fallback: "USUAL"
 *
 * For numerics, distinguishes INTEGER (long) from DECIMAL (double).
 *
 * Copyright 2026 harbour.github.io
 */

#include "hbcomp.h"
#include "hbast.h"
#include "hbfunctab.h"
#include "hbreftab.h"

/* Active reftab consulted by hb_astInferFromPrefix to resolve
   `o<ClassName>` / `so<ClassName>` variable-name patterns to the
   specific class instead of the generic OBJECT fallback. Set/cleared
   by hb_astSetPrefixReftab; hb_astPropagate save/restores across its
   own pass so nested calls (via hb_astInferExprType → child bodies)
   see the same table. Callers that don't publish a table fall through
   to the prefix-char-only behaviour. */
static PHB_REFTAB s_pPropRefTab = NULL;

void hb_astSetPrefixReftab( void * pRefTab )
{
   s_pPropRefTab = ( PHB_REFTAB ) pRefTab;
}

/* Lexically collapse `<seg>/../` and `./` from a path so warning messages
   read `/a/b/c` instead of `/a/x/../b/c` (scan/gen invoke the transpiler
   with paths like `$ROOT/../easipos/...`). Purely textual — no
   filesystem access, so it works on a path whose `..` segment doesn't
   resolve on disk. Returns a static buffer, or szPath unchanged when
   there is nothing to collapse / it is too long. */
const char * hb_strCollapsePath( const char * szPath )
{
   static char s_szBuf[ 1024 ];
   char        work[ 1024 ];
   const char * segs[ 256 ];
   int         nseg = 0, i;
   char *      p, * q, * o;
   HB_SIZE     rem;
   HB_BOOL     fAbs;

   if( ! szPath || ! strstr( szPath, ".." ) )
      return szPath;
   if( strlen( szPath ) >= sizeof( work ) )
      return szPath;
   hb_strncpy( work, szPath, sizeof( work ) - 1 );
   fAbs = ( work[ 0 ] == '/' );

   for( p = work; *p; )
   {
      while( *p == '/' )
         p++;
      if( ! *p )
         break;
      for( q = p; *q && *q != '/'; q++ )
         ;
      if( *q )
         *q++ = '\0';
      if( strcmp( p, "." ) == 0 )
         { p = q; continue; }
      if( strcmp( p, ".." ) == 0 && nseg > 0 &&
          strcmp( segs[ nseg - 1 ], ".." ) != 0 )
         { nseg--; p = q; continue; }
      if( nseg < 256 )
         segs[ nseg++ ] = p;
      p = q;
   }

   o = s_szBuf;
   rem = sizeof( s_szBuf );
   if( fAbs && rem > 1 )
      { *o++ = '/'; rem--; }
   for( i = 0; i < nseg; i++ )
   {
      HB_SIZE sl = strlen( segs[ i ] );
      if( i > 0 && rem > 1 )
         { *o++ = '/'; rem--; }
      if( sl + 1 >= rem )
         break;
      memcpy( o, segs[ i ], sl );
      o += sl;
      rem -= sl;
   }
   *o = '\0';
   return s_szBuf;
}

/*
 * Infer type from an initializer expression.
 * Returns a type string or NULL if type cannot be determined.
 */
static const char * hb_astInferFromExpr( PHB_EXPR pExpr )
{
   if( ! pExpr )
      return NULL;

   switch( pExpr->ExprType )
   {
      case HB_ET_NUMERIC:
         return "NUMERIC";

      case HB_ET_STRING:
         return "STRING";

      case HB_ET_LOGICAL:
         return "LOGICAL";

      case HB_ET_DATE:
         return "DATE";

      case HB_ET_TIMESTAMP:
         return "TIMESTAMP";

      case HB_ET_NIL:
         return NULL;  /* NIL doesn't tell us the type */

      case HB_ET_ARRAY:
         return "ARRAY";

      case HB_ET_HASH:
         return "HASH";

      case HB_ET_CODEBLOCK:
      case HB_ET_FUNREF:
         /* `@FuncName()` produces a function reference — callable via
            HbRuntime.EVAL or direct DLR dispatch. Same emit shape as
            a codeblock (both are `Func<dynamic[], dynamic>` at the
            edge, `dynamic` after hb_csTypeMap), so share the type. */
         return "BLOCK";

      case HB_ET_SELF:
         return "OBJECT";

      case HB_EO_NEGATE:
         /* Negation of a numeric — check the operand */
         if( pExpr->value.asOperator.pLeft )
            return hb_astInferFromExpr( pExpr->value.asOperator.pLeft );
         return "NUMERIC";

      case HB_ET_FUNCALL:
      {
         /* If the call target is a known HbRuntime function, prefer its
            declared return type over the caller's Hungarian prefix.
            This matters for functions like ErrorNew() whose Hungarian
            would type the LHS as `object` (killing `.severity`-style
            late binding) — a `-` return in hbfuncs.tab flags "dynamic
            on purpose" and we return USUAL here to force that. */
         PHB_EXPR pName = pExpr->value.asFunCall.pFunName;
         if( pName && pName->ExprType == HB_ET_FUNNAME &&
             pName->value.asSymbol.name &&
             hb_funcTabPrefix( pName->value.asSymbol.name ) )
         {
            const char * szRet =
               hb_funcTabReturnType( pName->value.asSymbol.name );
            return szRet ? szRet : "USUAL";
         }
         break;
      }

      default:
         break;
   }

   return NULL;
}

/*
 * Infer type from Hungarian notation prefix.
 * Looks at the first lowercase letter of the variable name.
 * Returns a type string or NULL if no recognized prefix.
 *
 * Prefixes:
 *   n  -> NUMERIC
 *   c  -> STRING
 *   l  -> LOGICAL
 *   a  -> ARRAY
 *   o  -> OBJECT
 *   d  -> DATE
 *   h  -> HASH
 *   b  -> BLOCK
 *   t  -> TIMESTAMP
 *   x  -> USUAL (explicitly variant)
 */
static const char * hb_astTypeForPrefixChar( char c )
{
   switch( c )
   {
      case 'n': return "NUMERIC";
      case 'c': return "STRING";
      case 'l': return "LOGICAL";
      case 'a': return "ARRAY";
      case 'o': return "OBJECT";
      case 'd': return "DATE";
      case 'h': return "HASH";
      case 'b': return "BLOCK";
      case 't': return "TIMESTAMP";
      case 'x': return "USUAL";
      /* `p`/`P` is used for two related concepts in the easipos
         corpus — function pointers (`@Foo()` return values) and
         opaque handles (DB/socket/statement pointers). BLOCK is
         Harbour's callable type; it maps to C# `dynamic` in
         hb_csTypeMap, which supports both calls (via DLR) and
         opaque storage. */
      case 'p': return "BLOCK";
      /* `f<X>` — function pointer / `@FuncName()` reference. Same C#
         shape as a code block (`Func<dynamic[], dynamic>` or just
         `dynamic` after typeMap). */
      case 'f': return "BLOCK";
   }
   return NULL;
}

/* If szName follows `o<ClassName>` (or `so<ClassName>`) and the
   suffix after the prefix matches a registered class, return the
   canonical class name from the reftab. Otherwise NULL. Requires
   s_pPropRefTab to be set — outside a propagation pass we have no
   reftab to consult and the caller falls through to the generic
   OBJECT inference. */
static const char * hb_astClassFromObjectName( const char * szName )
{
   const char * szSuffix = NULL;
   if( ! s_pPropRefTab || ! szName || ! szName[ 0 ] )
      return NULL;
   if( szName[ 0 ] == 'o' &&
       szName[ 1 ] >= 'A' && szName[ 1 ] <= 'Z' )
      szSuffix = szName + 1;
   else if( szName[ 0 ] == 's' && szName[ 1 ] == 'o' &&
            szName[ 2 ] >= 'A' && szName[ 2 ] <= 'Z' )
      szSuffix = szName + 2;
   if( ! szSuffix )
      return NULL;
   return hb_refTabClassCanonName( s_pPropRefTab, szSuffix );
}

/* True when szType is exactly the class hb_astClassFromObjectName
   derives from the variable's own name. Such a type is a weak hint —
   no initializer or assignment proved it — so refinement and the
   W0024 mismatch check must treat it like the generic OBJECT it
   replaces: assignment evidence (a constructor of a different class,
   a typed function return) wins over the name. */
static HB_BOOL hb_astIsNameSeededClass( const char * szName,
                                        const char * szType )
{
   const char * szClass;
   if( ! szType )
      return HB_FALSE;
   szClass = hb_astClassFromObjectName( szName );
   return szClass != NULL && hb_stricmp( szClass, szType ) == 0;
}

static const char * hb_astInferFromPrefix( const char * szName )
{
   if( ! szName || ! szName[ 0 ] )
      return NULL;

   /* `o<ClassName>` / `so<ClassName>` resolves to that specific class
      whenever the suffix matches a registered class — so authors who
      mark the type at the variable name (e.g. `oFcnTranLine`) get the
      static C# binding for free, no `:= FcnTranLine():New()` seed
      needed. Falls through to generic OBJECT for names that don't
      match any registered class (e.g. `oLine`, `oRow`). */
   {
      const char * szClass = hb_astClassFromObjectName( szName );
      if( szClass )
         return szClass;
   }

   /* The prefix is the first character, which should be lowercase.
      If the name starts with uppercase or underscore, no prefix. */
   if( szName[ 0 ] >= 'a' && szName[ 0 ] <= 'z' &&
       szName[ 1 ] != '\0' )
   {
      /* Second character should be uppercase or a digit to confirm
         this is Hungarian notation, not just a short variable name
         (e.g. `c3rdParty`, `n2ndValue`, `a3rdParty`). */
      if( ( szName[ 1 ] >= 'A' && szName[ 1 ] <= 'Z' ) ||
          ( szName[ 1 ] >= '0' && szName[ 1 ] <= '9' ) )
      {
         const char * szType = hb_astTypeForPrefixChar( szName[ 0 ] );
         if( szType )
            return szType;
      }

      /* easipos STATIC convention: `s<H>Name` — the first `s` signals
         file-scope STATIC, the second lowercase letter is the real
         Hungarian prefix (e.g. `snDrawer1Cash` = STATIC NUMERIC,
         `saFixed1Decln` = STATIC ARRAY, `soWindow` = STATIC OBJECT).
         Applied to every type — including value types (NUMERIC /
         LOGICAL / DATE / TIMESTAMP). A Harbour `:= NIL` init on a
         value-typed STATIC will produce `decimal x = null` which C#
         rejects (CS0037). That is intentional: every such site is a
         source bug — the Hungarian prefix promises a value type, the
         author should pick a real default (`:= 0`, `:= .F.`, etc.) or
         drop the init entirely. The same applies to `x := NIL` resets
         and `x == NIL` comparisons elsewhere. */
      if( szName[ 0 ] == 's' &&
          szName[ 1 ] >= 'a' && szName[ 1 ] <= 'z' &&
          szName[ 2 ] >= 'A' && szName[ 2 ] <= 'Z' )
      {
         const char * szType = hb_astTypeForPrefixChar( szName[ 1 ] );
         if( szType )
            return szType;
      }
   }

   return NULL;
}

/*
 * Main type inference function.
 *
 * Returns a type string for use in AS <type> declarations.
 * Priority: initializer expression > Hungarian prefix > "USUAL"
 *
 * For NUMERIC from prefix + INTEGER/DECIMAL from initializer,
 * the initializer wins (more specific).
 */
const char * hb_astInferType( const char * szName, PHB_EXPR pInit )
{
   const char * szType;

   /* 1. Try to infer from initializer expression */
   szType = hb_astInferFromExpr( pInit );
   if( szType )
      return szType;

   /* 2. Try Hungarian notation prefix */
   szType = hb_astInferFromPrefix( szName );
   if( szType )
      return szType;

   /* 3. Fallback */
   return "USUAL";
}

/*
 * Infer type from a string INIT value (used for CLASS DATA).
 * Priority: INIT string analysis > Hungarian prefix > "USUAL"
 */
const char * hb_astInferTypeFromInit( const char * szName, const char * szInit )
{
   if( szInit && szInit[ 0 ] )
   {
      HB_SIZE n = strlen( szInit );

      /* .T. or .F. */
      if( ( n == 3 || n == 4 ) && szInit[ 0 ] == '.' &&
          szInit[ n - 1 ] == '.' )
         return "LOGICAL";

      /* Quoted string */
      if( ( szInit[ 0 ] == '"' && szInit[ n - 1 ] == '"' ) ||
          ( szInit[ 0 ] == '\'' && szInit[ n - 1 ] == '\'' ) )
         return "STRING";

      /* Empty array {} */
      if( szInit[ 0 ] == '{' && szInit[ n - 1 ] == '}' )
         return "ARRAY";

      /* NIL */
      if( hb_stricmp( szInit, "NIL" ) == 0 )
         return NULL;

      /* Numeric literal */
      if( ( szInit[ 0 ] >= '0' && szInit[ 0 ] <= '9' ) ||
          szInit[ 0 ] == '-' || szInit[ 0 ] == '+' )
      {
         HB_SIZE i;
         for( i = ( szInit[ 0 ] == '-' || szInit[ 0 ] == '+' ) ? 1 : 0; i < n; i++ )
         {
            if( szInit[ i ] != '.' && ( szInit[ i ] < '0' || szInit[ i ] > '9' ) )
               break;  /* not a simple number */
         }
         if( i == n )
            return "NUMERIC";
      }
   }

   /* Fall back to Hungarian prefix */
   return hb_astInferType( szName, NULL );
}

/* ================================================================
 * Type propagation — infer types from assignments
 * ================================================================ */

/* Simple type environment: array of name/type pairs */
#define HB_TYPEENV_MAX  1024

typedef struct
{
   const char * szName;
   const char * szType;
} HB_TYPEENV_ENTRY;

typedef struct
{
   HB_TYPEENV_ENTRY entries[ HB_TYPEENV_MAX ];
   int          count;
   PHB_REFTAB   pRefTab;        /* active user-function table, or NULL */
   const char * szFile;         /* source file currently being walked (for warnings) */
} HB_TYPEENV;

static void hb_typeEnvInit( HB_TYPEENV * pEnv, PHB_REFTAB pRefTab,
                            const char * szFile )
{
   pEnv->count   = 0;
   pEnv->pRefTab = pRefTab;
   pEnv->szFile  = szFile;
}

static HB_BOOL hb_typeEnvSet( HB_TYPEENV * pEnv, const char * szName,
                              const char * szType )
{
   int i;

   /* Update existing entry */
   for( i = 0; i < pEnv->count; i++ )
   {
      if( hb_stricmp( pEnv->entries[ i ].szName, szName ) == 0 )
      {
         pEnv->entries[ i ].szType = szType;
         return HB_TRUE;
      }
   }

   /* Add new entry */
   if( pEnv->count < HB_TYPEENV_MAX )
   {
      pEnv->entries[ pEnv->count ].szName = szName;
      pEnv->entries[ pEnv->count ].szType = szType;
      pEnv->count++;
      return HB_TRUE;
   }

   /* Env full. A silent drop would set callers' fChanged flag on every
      fixed-point iteration and spin forever (hb_astPropagate's pass 2
      loops while fChanged). Fail loud instead — if real-world code
      exceeds the limit, raise HB_TYPEENV_MAX rather than papering over
      a missing entry with broken type inference. */
   fprintf( stderr,
            "hbtranspiler: fatal: type environment full at %d entries "
            "(trying to add %s). Raise HB_TYPEENV_MAX in hbtypes.c.\n",
            HB_TYPEENV_MAX, szName );
   exit( 1 );
}

static const char * hb_typeEnvGet( HB_TYPEENV * pEnv, const char * szName )
{
   int i;

   for( i = 0; i < pEnv->count; i++ )
   {
      if( hb_stricmp( pEnv->entries[ i ].szName, szName ) == 0 )
         return pEnv->entries[ i ].szType;
   }
   return NULL;
}

/*
 * Infer the type of an expression using the type environment.
 * This extends hb_astInferFromExpr to handle variables and operators.
 */
static const char * hb_astInferExprType( PHB_EXPR pExpr, HB_TYPEENV * pEnv )
{
   const char * szType;

   if( ! pExpr )
      return NULL;

   /* First try the simple literal-based inference */
   szType = hb_astInferFromExpr( pExpr );
   if( szType )
      return szType;

   switch( pExpr->ExprType )
   {
      case HB_ET_VARIABLE:
         /* Self is always OBJECT */
         if( hb_stricmp( pExpr->value.asSymbol.name, "Self" ) == 0 )
            return "OBJECT";
         /* Look up variable type in environment */
         {
            const char * szVarType = hb_typeEnvGet( pEnv, pExpr->value.asSymbol.name );
            if( szVarType )
               return szVarType;
            /* Fall back to Hungarian prefix for unknown variables (e.g. parameters) */
            return hb_astInferFromPrefix( pExpr->value.asSymbol.name );
         }

      case HB_ET_SEND:
         /* Constructor pattern: ClassName():New() / ClassName():Init() →
            return the class name as the inferred type so subsequent
            uses of the resulting variable can be method-resolved
            against the right class. */
         if( pExpr->value.asMessage.szMessage &&
             pExpr->value.asMessage.pObject &&
             pExpr->value.asMessage.pObject->ExprType == HB_ET_FUNCALL )
         {
            PHB_EXPR pCall = pExpr->value.asMessage.pObject;
            const char * szMsg = pExpr->value.asMessage.szMessage;
            if( pCall->value.asFunCall.pFunName &&
                pCall->value.asFunCall.pFunName->ExprType == HB_ET_FUNNAME &&
                ( hb_stricmp( szMsg, "NEW" ) == 0 ||
                  hb_stricmp( szMsg, "INIT" ) == 0 ) )
               return pCall->value.asFunCall.pFunName->value.asSymbol.name;
         }

         /* SELF:member — check type environment first (for class DATA types),
            then fall back to Hungarian prefix */
         if( pExpr->value.asMessage.szMessage )
         {
            const char * szMemberType = hb_typeEnvGet( pEnv, pExpr->value.asMessage.szMessage );
            if( szMemberType )
               return szMemberType;
            return hb_astInferFromPrefix( pExpr->value.asMessage.szMessage );
         }
         return NULL;

      case HB_EO_PLUS:
      {
         /* Result type depends on operand types */
         const char * szLeft = hb_astInferExprType(
            pExpr->value.asOperator.pLeft, pEnv );
         const char * szRight = hb_astInferExprType(
            pExpr->value.asOperator.pRight, pEnv );

         if( ! szLeft || ! szRight )
            return NULL;

         /* STRING + STRING = STRING */
         if( strcmp( szLeft, "STRING" ) == 0 && strcmp( szRight, "STRING" ) == 0 )
            return "STRING";

         /* NUMERIC + NUMERIC = NUMERIC */
         if( strcmp( szLeft, "NUMERIC" ) == 0 && strcmp( szRight, "NUMERIC" ) == 0 )
            return "NUMERIC";

         /* DATE + NUMERIC = DATE */
         if( strcmp( szLeft, "DATE" ) == 0 && strcmp( szRight, "NUMERIC" ) == 0 )
            return "DATE";

         return NULL;
      }

      case HB_EO_MULT:
      case HB_EO_DIV:
      case HB_EO_MOD:
      case HB_EO_POWER:
         /* These four operators are arithmetic-only in Harbour: there
            is no other meaning. The result is always NUMERIC, even when
            one or both operand types haven't been inferred yet. This
            is what lets `n * 2` infer NUMERIC for an untyped parameter
            `n` — the operator itself constrains the result. */
         return "NUMERIC";

      case HB_EO_MINUS:
      {
         /* MINUS is polymorphic between NUMERIC and DATE-DATE. If we
            don't know either operand, we still know the result is one
            of those — but for type-mapping purposes that's not useful.
            Default to NUMERIC since DATE-DATE is rare in practice. */
         const char * szLeft = hb_astInferExprType(
            pExpr->value.asOperator.pLeft, pEnv );
         const char * szRight = hb_astInferExprType(
            pExpr->value.asOperator.pRight, pEnv );

         if( szLeft && szRight &&
             strcmp( szLeft, "DATE" ) == 0 && strcmp( szRight, "DATE" ) == 0 )
            return "NUMERIC";  /* DATE - DATE = days */

         /* All other minus uses (DATE - NUMERIC, NUMERIC - NUMERIC,
            NUMERIC - unknown, unknown - unknown) → NUMERIC. */
         return "NUMERIC";
      }

      case HB_EO_EQUAL:
      case HB_EO_EQ:
      case HB_EO_NE:
      case HB_EO_LT:
      case HB_EO_GT:
      case HB_EO_LE:
      case HB_EO_GE:
      case HB_EO_AND:
      case HB_EO_OR:
      case HB_EO_NOT:
         return "LOGICAL";

      case HB_EO_NEGATE:
      {
         const char * szOp = hb_astInferExprType(
            pExpr->value.asOperator.pLeft, pEnv );
         return szOp;
      }

      case HB_EO_PREINC:
      case HB_EO_PREDEC:
      case HB_EO_POSTINC:
      case HB_EO_POSTDEC:
      {
         const char * szOp = hb_astInferExprType(
            pExpr->value.asOperator.pLeft, pEnv );
         return szOp;
      }

      case HB_EO_PLUSEQ:
      case HB_EO_MINUSEQ:
      case HB_EO_MULTEQ:
      case HB_EO_DIVEQ:
      case HB_EO_MODEQ:
      case HB_EO_EXPEQ:
         /* Compound assignment — type of variable */
         return hb_astInferExprType( pExpr->value.asOperator.pLeft, pEnv );

      case HB_ET_FUNCALL:
         /* Infer return types for known functions:
              1. stdlib   — hbfuncs.tab knowledge file
              2. user     — hbreftab.tab (cross-file pre-scan output) */
         if( pExpr->value.asFunCall.pFunName &&
             pExpr->value.asFunCall.pFunName->ExprType == HB_ET_FUNNAME )
         {
            const char * szFunc =
               pExpr->value.asFunCall.pFunName->value.asSymbol.name;
            const char * szRet = hb_funcTabReturnType( szFunc );
            if( szRet )
               return szRet;
            if( pEnv && pEnv->pRefTab )
               return hb_refTabReturnType( pEnv->pRefTab, szFunc );
         }
         return NULL;

      default:
         break;
   }

   return NULL;
}

/* Try to propagate type for a variable assignment */
static void hb_astPropagateVar( const char * szVarName, PHB_EXPR pRHS,
                                HB_TYPEENV * pEnv, HB_BOOL * pfChanged )
{
   const char * szCurType = hb_typeEnvGet( pEnv, szVarName );

   /* `x` (and easipos `sx`) is the explicit USUAL marker: the author
      chose it because the variable legitimately holds different types
      across arms (`local xResult` in a polymorphic dispatcher). Locking
      it to whichever arm Pass 2 visits first misrepresents the function's
      return type and then W0024 fires at every other-typed caller. Treat
      the seeded USUAL as final for these names. */
   if( szCurType && strcmp( szCurType, "USUAL" ) == 0 )
   {
      const char * szPrefixType = hb_astInferFromPrefix( szVarName );
      if( szPrefixType && strcmp( szPrefixType, "USUAL" ) == 0 )
         return;
   }

   /* Refine when: unknown, USUAL (untyped), OBJECT (generic Hungarian
      `o` prefix), or a class the variable's own name seeded (weak hint
      — an `oServer := TCPClient():New()` assignment must be able to
      override the name-derived Server). OBJECT is upgradeable to a
      specific class name when the RHS is a constructor like
      `Transaction():New()`. */
   if( ! szCurType || strcmp( szCurType, "USUAL" ) == 0 ||
       strcmp( szCurType, "OBJECT" ) == 0 ||
       hb_astIsNameSeededClass( szVarName, szCurType ) )
   {
      const char * szNewType = hb_astInferExprType( pRHS, pEnv );
      if( szNewType && ( ! szCurType || strcmp( szCurType, szNewType ) != 0 ) )
      {
         /* Only mark the fixed-point loop dirty when the env actually
            stored the new binding. Without this guard an overflowed
            env would report "changed" every iteration and the outer
            loop in hb_astPropagate would never terminate. */
         if( hb_typeEnvSet( pEnv, szVarName, szNewType ) )
            *pfChanged = HB_TRUE;
      }
   }
}

/* Recursively walk a block and its nested structures for assignments */
static void hb_astPropagateBlock( PHB_AST_NODE pBlock, HB_TYPEENV * pEnv,
                                  HB_BOOL * pfChanged )
{
   PHB_AST_NODE pStmt;

   if( ! pBlock )
      return;

   if( pBlock->type == HB_AST_BLOCK )
      pStmt = pBlock->value.asBlock.pFirst;
   else
      return;

   while( pStmt )
   {
      switch( pStmt->type )
      {
         case HB_AST_EXPRSTMT:
            if( pStmt->value.asExprStmt.pExpr )
            {
               PHB_EXPR pExpr = pStmt->value.asExprStmt.pExpr;
               if( pExpr->ExprType == HB_EO_ASSIGN &&
                   pExpr->value.asOperator.pLeft &&
                   pExpr->value.asOperator.pLeft->ExprType == HB_ET_VARIABLE )
               {
                  hb_astPropagateVar(
                     pExpr->value.asOperator.pLeft->value.asSymbol.name,
                     pExpr->value.asOperator.pRight, pEnv, pfChanged );
               }
            }
            break;

         case HB_AST_FOR:
            /* FOR variable gets type from start expression */
            if( pStmt->value.asFor.szVar && pStmt->value.asFor.pStart )
               hb_astPropagateVar( pStmt->value.asFor.szVar,
                                   pStmt->value.asFor.pStart, pEnv, pfChanged );
            /* Recurse into FOR body */
            hb_astPropagateBlock( pStmt->value.asFor.pBody, pEnv, pfChanged );
            break;

         case HB_AST_FOREACH:
            /* FOR EACH variable — type is USUAL (element type unknown) */
            hb_astPropagateBlock( pStmt->value.asForEach.pBody, pEnv, pfChanged );
            break;

         case HB_AST_DOWHILE:
            hb_astPropagateBlock( pStmt->value.asWhile.pBody, pEnv, pfChanged );
            break;

         case HB_AST_IF:
            hb_astPropagateBlock( pStmt->value.asIf.pThen, pEnv, pfChanged );
            {
               PHB_AST_NODE pElseIf = pStmt->value.asIf.pElseIfs;
               while( pElseIf )
               {
                  hb_astPropagateBlock( pElseIf->value.asElseIf.pBody, pEnv, pfChanged );
                  pElseIf = pElseIf->pNext;
               }
            }
            hb_astPropagateBlock( pStmt->value.asIf.pElse, pEnv, pfChanged );
            break;

         case HB_AST_DOCASE:
            {
               PHB_AST_NODE pCase = pStmt->value.asDoCase.pCases;
               while( pCase )
               {
                  hb_astPropagateBlock( pCase->value.asCase.pBody, pEnv, pfChanged );
                  pCase = pCase->pNext;
               }
            }
            hb_astPropagateBlock( pStmt->value.asDoCase.pOtherwise, pEnv, pfChanged );
            break;

         case HB_AST_SWITCH:
            {
               PHB_AST_NODE pCase = pStmt->value.asSwitch.pCases;
               while( pCase )
               {
                  hb_astPropagateBlock( pCase->value.asCase.pBody, pEnv, pfChanged );
                  pCase = pCase->pNext;
               }
            }
            hb_astPropagateBlock( pStmt->value.asSwitch.pDefault, pEnv, pfChanged );
            break;

         case HB_AST_BEGINSEQ:
            hb_astPropagateBlock( pStmt->value.asSeq.pBody, pEnv, pfChanged );
            hb_astPropagateBlock( pStmt->value.asSeq.pRecover, pEnv, pfChanged );
            hb_astPropagateBlock( pStmt->value.asSeq.pAlways, pEnv, pfChanged );
            break;

         case HB_AST_WITHOBJECT:
            hb_astPropagateBlock( pStmt->value.asWithObj.pBody, pEnv, pfChanged );
            break;

         default:
            break;
      }
      pStmt = pStmt->pNext;
   }
}

/* Recursively collect RETURN expression types from a block. Tracks
   uninferrable returns separately: a function that has *some* known
   return type and *some* unknown one (e.g. `aADTFiles[1]` — array
   subscripts have no static element type) is genuinely polymorphic at
   the call site, and labelling it with only the known branch's type
   misleads W0024 at correctly-typed callers of the other branch. */
static void hb_astCollectReturnTypes( PHB_AST_NODE pBlock, HB_TYPEENV * pEnv,
                                      const char ** pszRetType, HB_BOOL * pfConflict,
                                      HB_BOOL * pfSawUnknown )
{
   PHB_AST_NODE pStmt;

   if( ! pBlock || *pfConflict )
      return;

   if( pBlock->type != HB_AST_BLOCK )
      return;

   pStmt = pBlock->value.asBlock.pFirst;
   while( pStmt )
   {
      if( pStmt->type == HB_AST_RETURN && pStmt->value.asReturn.pExpr )
      {
         const char * szType = hb_astInferExprType( pStmt->value.asReturn.pExpr, pEnv );
         if( szType )
         {
            if( *pszRetType == NULL )
               *pszRetType = szType;
            else if( strcmp( *pszRetType, szType ) != 0 )
            {
               /* Multiple RETURN types disagree — compatible if both numeric */
               if( strcmp( *pszRetType, "NUMERIC" ) == 0 &&
                   strcmp( szType, "NUMERIC" ) == 0 )
                  *pszRetType = "NUMERIC";
               else
                  *pfConflict = HB_TRUE;
            }
         }
         else
            *pfSawUnknown = HB_TRUE;
      }
      /* Recurse into nested structures */
      else if( pStmt->type == HB_AST_IF )
      {
         hb_astCollectReturnTypes( pStmt->value.asIf.pThen, pEnv, pszRetType, pfConflict, pfSawUnknown );
         {
            PHB_AST_NODE pElseIf = pStmt->value.asIf.pElseIfs;
            while( pElseIf )
            {
               hb_astCollectReturnTypes( pElseIf->value.asElseIf.pBody, pEnv, pszRetType, pfConflict, pfSawUnknown );
               pElseIf = pElseIf->pNext;
            }
         }
         hb_astCollectReturnTypes( pStmt->value.asIf.pElse, pEnv, pszRetType, pfConflict, pfSawUnknown );
      }
      else if( pStmt->type == HB_AST_DOWHILE )
         hb_astCollectReturnTypes( pStmt->value.asWhile.pBody, pEnv, pszRetType, pfConflict, pfSawUnknown );
      else if( pStmt->type == HB_AST_FOR )
         hb_astCollectReturnTypes( pStmt->value.asFor.pBody, pEnv, pszRetType, pfConflict, pfSawUnknown );
      else if( pStmt->type == HB_AST_FOREACH )
         hb_astCollectReturnTypes( pStmt->value.asForEach.pBody, pEnv, pszRetType, pfConflict, pfSawUnknown );
      else if( pStmt->type == HB_AST_DOCASE )
      {
         PHB_AST_NODE pCase = pStmt->value.asDoCase.pCases;
         while( pCase )
         {
            hb_astCollectReturnTypes( pCase->value.asCase.pBody, pEnv, pszRetType, pfConflict, pfSawUnknown );
            pCase = pCase->pNext;
         }
         hb_astCollectReturnTypes( pStmt->value.asDoCase.pOtherwise, pEnv, pszRetType, pfConflict, pfSawUnknown );
      }
      else if( pStmt->type == HB_AST_SWITCH )
      {
         PHB_AST_NODE pCase = pStmt->value.asSwitch.pCases;
         while( pCase )
         {
            hb_astCollectReturnTypes( pCase->value.asCase.pBody, pEnv, pszRetType, pfConflict, pfSawUnknown );
            pCase = pCase->pNext;
         }
         hb_astCollectReturnTypes( pStmt->value.asSwitch.pDefault, pEnv, pszRetType, pfConflict, pfSawUnknown );
      }
      else if( pStmt->type == HB_AST_BEGINSEQ )
      {
         hb_astCollectReturnTypes( pStmt->value.asSeq.pBody, pEnv, pszRetType, pfConflict, pfSawUnknown );
         hb_astCollectReturnTypes( pStmt->value.asSeq.pRecover, pEnv, pszRetType, pfConflict, pfSawUnknown );
      }

      pStmt = pStmt->pNext;
   }
}

/* ================================================================
 * Call-site parameter-type refinement
 *
 * After intra-function type propagation has converged, walk the body
 * one more time looking for FUNCALL/SEND nodes. For each argument
 * whose type we can now infer, call hb_refTabRefineParamType on the
 * callee's slot. If that returns HB_REFINE_CONFLICT, emit a one-shot
 * warning to stderr so the user can investigate.
 *
 * Requires the refTab to be present in the env. If pEnv->pRefTab is
 * NULL (e.g. legacy callers that don't have the table), this pass is
 * a no-op.
 * ================================================================ */

static void hb_astRefineArgList( const char * szCallee, PHB_EXPR pParms,
                                 HB_TYPEENV * pEnv, int iLine )
{
   PHB_EXPR pArg;
   int      iPos = 0;
   char     szStaticKey[ 256 ];

   if( ! szCallee || ! pParms || ! pEnv->pRefTab )
      return;

   /* STATIC functions are registered as `<FileBase>::<Name>` in the
      reftab to avoid colliding with free functions of the same name
      in other files. A bare-name call from within that file should
      refine the file-scoped entry; from outside, fall back to the
      bare-name (free) entry. Method-style keys (containing "::"
      already) skip this lookup. */
   if( pEnv->szFile && ! strstr( szCallee, "::" ) )
   {
      PHB_FNAME pSplit = hb_fsFNameSplit( pEnv->szFile );
      if( pSplit && pSplit->szName )
      {
         hb_snprintf( szStaticKey, sizeof( szStaticKey ), "%s::%s",
                      pSplit->szName, szCallee );
         if( hb_refTabParamCount( pEnv->pRefTab, szStaticKey ) > 0 )
            szCallee = szStaticKey;
      }
      if( pSplit )
         hb_xfree( pSplit );
   }

   if( pParms->ExprType == HB_ET_LIST ||
       pParms->ExprType == HB_ET_ARGLIST ||
       pParms->ExprType == HB_ET_MACROARGLIST )
      pArg = pParms->value.asList.pExprList;
   else
      pArg = pParms;

   while( pArg )
   {
      if( pArg->ExprType != HB_ET_NONE )
      {
         /* Unwrap `@var` at the call site so we infer from the
            referent, not the reference. */
         PHB_EXPR pEffective = pArg;
         if( pArg->ExprType == HB_ET_VARREF )
            pEffective = pArg;   /* keep as-is, asSymbol.name already works */
         else if( pArg->ExprType == HB_ET_REFERENCE )
            pEffective = pArg->value.asReference;

         /* By-ref marking. The scanner handles Self:method and free
            functions; here we cover the typed-receiver method calls
            (oCalc:Adjust(@n) and the like) where statically resolving
            the class needs the type env. */
         if( pArg->ExprType == HB_ET_VARREF ||
             pArg->ExprType == HB_ET_REFERENCE )
            hb_refTabMark( pEnv->pRefTab, szCallee, iPos );

         {
            const char * szArgType =
               hb_astInferExprType( pEffective, pEnv );
            HB_REFINE_RESULT r = hb_refTabRefineParamType(
               pEnv->pRefTab, szCallee, iPos, szArgType );

            if( r == HB_REFINE_CONFLICT )
            {
               /* Emitted on the first call site that disagrees with
                  the slot's prior type. Subsequent ALREADY_CONFLICT
                  results stay silent — every non-USUAL caller against
                  an already-widened slot would otherwise re-warn
                  including the type that's actually "right", flooding
                  the gate with noise. scan.sh accumulates warnings
                  across passes so the pass-1 emission survives to
                  convergence.

                  Format matches W0020/W0021 etc. so scan.sh's
                  warning-capture grep picks it up. */
               const HB_REFPARAM * pP =
                  hb_refTabParam( pEnv->pRefTab, szCallee, iPos );
               const char * szPName = ( pP && pP->szName ) ? pP->szName : "?";
               const char * szF =
                  pEnv->szFile ? hb_strCollapsePath( pEnv->szFile ) : "?";
               fprintf( stderr,
                  "hbtranspiler: %s(%d): warning W0022  "
                  "Call to '%s' passes %s for parameter '%s' but earlier "
                  "sites had a different type — downgrading to USUAL\n",
                  szF, iLine, szCallee,
                  szArgType ? szArgType : "?", szPName );
            }
         }
      }
      pArg = pArg->pNext;
      iPos++;
   }
}

static void hb_astRefineExpr( PHB_EXPR pExpr, HB_TYPEENV * pEnv, int iLine );

static void hb_astRefineBlock( PHB_AST_NODE pNode, HB_TYPEENV * pEnv )
{
   PHB_AST_NODE pStmt;

   if( ! pNode || pNode->type != HB_AST_BLOCK )
      return;

   pStmt = pNode->value.asBlock.pFirst;
   while( pStmt )
   {
      int iLine = pStmt->iLine;
      switch( pStmt->type )
      {
         case HB_AST_EXPRSTMT:
            hb_astRefineExpr( pStmt->value.asExprStmt.pExpr, pEnv, iLine );
            break;
         case HB_AST_RETURN:
            hb_astRefineExpr( pStmt->value.asReturn.pExpr, pEnv, iLine );
            break;
         case HB_AST_QOUT:
         case HB_AST_QQOUT:
            hb_astRefineExpr( pStmt->value.asQOut.pExprList, pEnv, iLine );
            break;
         case HB_AST_IF:
            hb_astRefineExpr( pStmt->value.asIf.pCondition, pEnv, iLine );
            hb_astRefineBlock( pStmt->value.asIf.pThen, pEnv );
            {
               PHB_AST_NODE p = pStmt->value.asIf.pElseIfs;
               while( p )
               {
                  hb_astRefineExpr( p->value.asElseIf.pCondition, pEnv, p->iLine );
                  hb_astRefineBlock( p->value.asElseIf.pBody, pEnv );
                  p = p->pNext;
               }
            }
            hb_astRefineBlock( pStmt->value.asIf.pElse, pEnv );
            break;
         case HB_AST_DOWHILE:
            hb_astRefineExpr( pStmt->value.asWhile.pCondition, pEnv, iLine );
            hb_astRefineBlock( pStmt->value.asWhile.pBody, pEnv );
            break;
         case HB_AST_FOR:
            hb_astRefineExpr( pStmt->value.asFor.pStart, pEnv, iLine );
            hb_astRefineExpr( pStmt->value.asFor.pEnd,   pEnv, iLine );
            hb_astRefineExpr( pStmt->value.asFor.pStep,  pEnv, iLine );
            hb_astRefineBlock( pStmt->value.asFor.pBody, pEnv );
            break;
         case HB_AST_FOREACH:
            hb_astRefineExpr( pStmt->value.asForEach.pEnum, pEnv, iLine );
            hb_astRefineBlock( pStmt->value.asForEach.pBody, pEnv );
            break;
         case HB_AST_DOCASE:
         {
            PHB_AST_NODE p = pStmt->value.asDoCase.pCases;
            while( p )
            {
               hb_astRefineExpr( p->value.asCase.pCondition, pEnv, p->iLine );
               hb_astRefineBlock( p->value.asCase.pBody, pEnv );
               p = p->pNext;
            }
            hb_astRefineBlock( pStmt->value.asDoCase.pOtherwise, pEnv );
            break;
         }
         case HB_AST_SWITCH:
         {
            PHB_AST_NODE p = pStmt->value.asSwitch.pCases;
            hb_astRefineExpr( pStmt->value.asSwitch.pSwitch, pEnv, iLine );
            while( p )
            {
               hb_astRefineExpr( p->value.asCase.pCondition, pEnv, p->iLine );
               hb_astRefineBlock( p->value.asCase.pBody, pEnv );
               p = p->pNext;
            }
            hb_astRefineBlock( pStmt->value.asSwitch.pDefault, pEnv );
            break;
         }
         case HB_AST_BEGINSEQ:
            hb_astRefineBlock( pStmt->value.asSeq.pBody, pEnv );
            hb_astRefineBlock( pStmt->value.asSeq.pRecover, pEnv );
            hb_astRefineBlock( pStmt->value.asSeq.pAlways, pEnv );
            break;
         case HB_AST_WITHOBJECT:
            hb_astRefineExpr( pStmt->value.asWithObj.pObject, pEnv, iLine );
            hb_astRefineBlock( pStmt->value.asWithObj.pBody, pEnv );
            break;
         case HB_AST_BREAK:
            hb_astRefineExpr( pStmt->value.asBreak.pExpr, pEnv, iLine );
            break;
         case HB_AST_LOCAL:
         case HB_AST_STATIC:
         case HB_AST_PUBLIC:
         case HB_AST_PRIVATE:
            hb_astRefineExpr( pStmt->value.asVar.pInit, pEnv, iLine );
            break;
         default:
            break;
      }
      pStmt = pStmt->pNext;
   }
}

static void hb_astRefineExpr( PHB_EXPR pExpr, HB_TYPEENV * pEnv, int iLine )
{
   if( ! pExpr )
      return;

   switch( pExpr->ExprType )
   {
      case HB_ET_FUNCALL:
      {
         const char * szName = NULL;
         if( pExpr->value.asFunCall.pFunName &&
             pExpr->value.asFunCall.pFunName->ExprType == HB_ET_FUNNAME )
            szName = pExpr->value.asFunCall.pFunName->value.asSymbol.name;

         hb_astRefineArgList( szName, pExpr->value.asFunCall.pParms,
                              pEnv, iLine );
         /* Recurse into args too (nested calls). */
         {
            PHB_EXPR pParms = pExpr->value.asFunCall.pParms;
            if( pParms && ( pParms->ExprType == HB_ET_LIST ||
                            pParms->ExprType == HB_ET_ARGLIST ) )
            {
               PHB_EXPR p = pParms->value.asList.pExprList;
               while( p )
               {
                  hb_astRefineExpr( p, pEnv, iLine );
                  p = p->pNext;
               }
            }
         }
         break;
      }

      case HB_ET_SEND:
      {
         /* Method calls — try to compute the receiver's class so the
            refinement keys on Class::Method instead of bare Method.
            When the receiver's class is unknown (Hungarian `o` only
            tells us OBJECT, not which class) we DON'T fall back to
            bare-name refinement: a free function with the same name
            as the method would otherwise have its slot types poisoned
            by the method's call-site args (e.g. method
            EasiCdS:PostLoyalty(aTakenBenefits, cBase64PDF) and free
            PostLoyalty(oTransaction, oPOSStatus) share the bare
            `PostLoyalty` key, so STRING args bleed into the OBJECT
            slots). Under-refining is safer than mis-refining. */
         const char * szMethod = pExpr->value.asMessage.szMessage;
         const char * szRecvType = NULL;
         char szKey[ 256 ];
         const char * szLookup = NULL;

         if( pExpr->value.asMessage.pObject )
            szRecvType = hb_astInferExprType(
               pExpr->value.asMessage.pObject, pEnv );

         if( szRecvType && szMethod )
         {
            /* Reject the standard non-class type tags. Anything that
               survives is treated as a class name. */
            if( hb_stricmp( szRecvType, "USUAL"   ) != 0 &&
                hb_stricmp( szRecvType, "NUMERIC" ) != 0 &&
                hb_stricmp( szRecvType, "STRING"  ) != 0 &&
                hb_stricmp( szRecvType, "LOGICAL" ) != 0 &&
                hb_stricmp( szRecvType, "DATE"    ) != 0 &&
                hb_stricmp( szRecvType, "ARRAY"   ) != 0 &&
                hb_stricmp( szRecvType, "HASH"    ) != 0 &&
                hb_stricmp( szRecvType, "BLOCK"   ) != 0 &&
                hb_stricmp( szRecvType, "OBJECT"  ) != 0 )
            {
               /* Canonical method key `<Class>::<Class>__<Method>` —
                  the mangled member matches the definition scan's key
                  (hbreftab.c Pass 1), so refinement (by-ref marking,
                  type narrowing) lands on the method's own entry. */
               hb_snprintf( szKey, sizeof( szKey ), "%s::%s__%s",
                            szRecvType, szRecvType, szMethod );
               szLookup = szKey;
            }
         }

         if( szLookup )
            hb_astRefineArgList( szLookup, pExpr->value.asMessage.pParms,
                                 pEnv, iLine );
         hb_astRefineExpr( pExpr->value.asMessage.pObject, pEnv, iLine );
         {
            PHB_EXPR pParms = pExpr->value.asMessage.pParms;
            if( pParms && ( pParms->ExprType == HB_ET_LIST ||
                            pParms->ExprType == HB_ET_ARGLIST ) )
            {
               PHB_EXPR p = pParms->value.asList.pExprList;
               while( p )
               {
                  hb_astRefineExpr( p, pEnv, iLine );
                  p = p->pNext;
               }
            }
         }
         break;
      }

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
            hb_astRefineExpr( p, pEnv, iLine );
            p = p->pNext;
         }
         break;
      }

      case HB_ET_CODEBLOCK:
      {
         PHB_EXPR p = pExpr->value.asCodeblock.pExprList;
         while( p )
         {
            hb_astRefineExpr( p, pEnv, iLine );
            p = p->pNext;
         }
         break;
      }

      case HB_ET_ARRAYAT:
         hb_astRefineExpr( pExpr->value.asList.pExprList, pEnv, iLine );
         hb_astRefineExpr( pExpr->value.asList.pIndex,    pEnv, iLine );
         break;

      default:
         if( pExpr->ExprType >= HB_EO_POSTINC )
         {
            hb_astRefineExpr( pExpr->value.asOperator.pLeft,  pEnv, iLine );
            hb_astRefineExpr( pExpr->value.asOperator.pRight, pEnv, iLine );
         }
         break;
   }
}

/*
 * Run type propagation on a function's AST body.
 *
 * Pass 1: Seed the type environment from LOCAL/STATIC declarations
 * Pass 2: Walk expression statements and infer types for USUAL variables
 * Pass 3: Update LOCAL/STATIC AST nodes with propagated types
 * Pass 4: Infer function return type from RETURN statements
 * Pass 5: Refine callee parameter types from the types we see at call
 *         sites in this body (requires pRefTab to be non-NULL).
 *
 * Returns the inferred return type string, or NULL if it can't be determined.
 */
/* Emit W0024 when an assignment's RHS conflicts with the lvalue's
   Hungarian prefix. The LHS type comes from the prefix alone (the strict
   contract — "if you spelled it `nFoo` you committed to numeric"); the
   env is consulted only for the RHS, where inference may have refined it
   via the initializer / function returns / operator types. `x` and
   unprefixed names yield USUAL and are skipped, as does OBJECT on either
   side (a generic-object lvalue can legitimately be assigned a specific
   class, etc.). Non-halting — codegen continues. */
/* Dedup table for W0024 — hb_astPropagate runs during both the scan and
   the emit, so without this each conflict would fire twice. One process
   per .prg, so a small per-process table is plenty. */
#define HB_HUNG_DEDUP 1024
static struct { int iLine; char szName[ 64 ]; } s_aHungWarned[ HB_HUNG_DEDUP ];
static int s_iHungWarned = 0;

static HB_BOOL hb_astHungSeen( int iLine, const char * szName )
{
   int i;
   if( ! szName )
      return HB_TRUE;
   for( i = 0; i < s_iHungWarned; i++ )
      if( s_aHungWarned[ i ].iLine == iLine &&
          strcmp( s_aHungWarned[ i ].szName, szName ) == 0 )
         return HB_TRUE;
   if( s_iHungWarned < HB_HUNG_DEDUP )
   {
      s_aHungWarned[ s_iHungWarned ].iLine = iLine;
      hb_strncpy( s_aHungWarned[ s_iHungWarned ].szName, szName,
                  sizeof( s_aHungWarned[ s_iHungWarned ].szName ) - 1 );
      s_iHungWarned++;
   }
   return HB_FALSE;
}

static void hb_astCheckOneAssign( const char * szName, PHB_EXPR pRHS,
                                  HB_TYPEENV * pEnv, const char * szFile,
                                  int iLine )
{
   const char * szLhs;
   const char * szRhs;

   if( ! szName || ! pRHS )
      return;
   szLhs = hb_astInferType( szName, NULL );    /* prefix only */
   szRhs = hb_astInferExprType( pRHS, pEnv );
   if( ! szLhs || ! szRhs )
      return;
   if( hb_stricmp( szLhs, "USUAL" ) == 0 ||
       hb_stricmp( szLhs, "OBJECT" ) == 0 )
      return;
   /* A class derived from the variable's own name (`o<ClassName>`) is
      a weak hint, not a declaration — assigning a subclass or factory
      result to it is normal, and the reftab records no INHERIT edges
      to tell relatives apart. Treat it like the OBJECT it replaced. */
   if( hb_astIsNameSeededClass( szName, szLhs ) )
      return;
   if( hb_stricmp( szRhs, "USUAL" ) == 0 ||
       hb_stricmp( szRhs, "OBJECT" ) == 0 )
      return;
   if( hb_stricmp( szLhs, szRhs ) == 0 )
      return;
   if( hb_astHungSeen( iLine, szName ) )
      return;
   fprintf( stderr,
            "hbtranspiler: %s(%d): warning W0024  "
            "assigning %s to '%s' contradicts its Hungarian-prefix type %s\n",
            hb_strCollapsePath( szFile ? szFile : "?" ),
            iLine, szRhs, szName, szLhs );
}

static void hb_astCheckHungarianMismatch( PHB_AST_NODE pBlock,
                                          HB_TYPEENV * pEnv,
                                          const char * szFile )
{
   PHB_AST_NODE pStmt;

   if( ! pBlock || pBlock->type != HB_AST_BLOCK )
      return;
   pStmt = pBlock->value.asBlock.pFirst;
   while( pStmt )
   {
      switch( pStmt->type )
      {
         case HB_AST_EXPRSTMT:
            if( pStmt->value.asExprStmt.pExpr )
            {
               PHB_EXPR pExpr = pStmt->value.asExprStmt.pExpr;
               if( pExpr->ExprType == HB_EO_ASSIGN &&
                   pExpr->value.asOperator.pLeft &&
                   pExpr->value.asOperator.pLeft->ExprType == HB_ET_VARIABLE )
               {
                  hb_astCheckOneAssign(
                     pExpr->value.asOperator.pLeft->value.asSymbol.name,
                     pExpr->value.asOperator.pRight,
                     pEnv, szFile, pStmt->iLine );
               }
            }
            break;
         case HB_AST_FOR:
            if( pStmt->value.asFor.szVar && pStmt->value.asFor.pStart )
               hb_astCheckOneAssign( pStmt->value.asFor.szVar,
                                     pStmt->value.asFor.pStart,
                                     pEnv, szFile, pStmt->iLine );
            hb_astCheckHungarianMismatch( pStmt->value.asFor.pBody, pEnv, szFile );
            break;
         case HB_AST_FOREACH:
            hb_astCheckHungarianMismatch( pStmt->value.asForEach.pBody, pEnv, szFile );
            break;
         case HB_AST_DOWHILE:
            hb_astCheckHungarianMismatch( pStmt->value.asWhile.pBody, pEnv, szFile );
            break;
         case HB_AST_IF:
            hb_astCheckHungarianMismatch( pStmt->value.asIf.pThen, pEnv, szFile );
            {
               PHB_AST_NODE pElseIf = pStmt->value.asIf.pElseIfs;
               while( pElseIf )
               {
                  hb_astCheckHungarianMismatch( pElseIf->value.asElseIf.pBody,
                                                pEnv, szFile );
                  pElseIf = pElseIf->pNext;
               }
            }
            hb_astCheckHungarianMismatch( pStmt->value.asIf.pElse, pEnv, szFile );
            break;
         case HB_AST_DOCASE:
            {
               PHB_AST_NODE pCase = pStmt->value.asDoCase.pCases;
               while( pCase )
               {
                  hb_astCheckHungarianMismatch( pCase->value.asCase.pBody,
                                                pEnv, szFile );
                  pCase = pCase->pNext;
               }
            }
            break;
         case HB_AST_WITHOBJECT:
            hb_astCheckHungarianMismatch( pStmt->value.asWithObj.pBody,
                                          pEnv, szFile );
            break;
         default:
            break;
      }
      pStmt = pStmt->pNext;
   }
}

const char * hb_astPropagate( PHB_AST_NODE pBody, PHB_AST_NODE pClassList,
                              void * pRefTab, const char * szFuncKey,
                              const char * szFile )
{
   HB_TYPEENV env;
   PHB_AST_NODE pStmt;
   HB_BOOL fChanged;
   const char * szRetType = NULL;
   HB_BOOL fConflict = HB_FALSE;
   HB_BOOL fSawUnknown = HB_FALSE;
   PHB_REFTAB pSavedPropRefTab;

   if( ! pBody || pBody->type != HB_AST_BLOCK )
      return NULL;

   /* Publish the reftab to hb_astInferFromPrefix's class-from-name
      check for the duration of this pass. Save/restore rather than
      clear-to-NULL so codegen callers (which publish the reftab for
      the whole emit pass via hb_astSetPrefixReftab) keep it in scope
      once nested propagate calls return. */
   pSavedPropRefTab = s_pPropRefTab;
   s_pPropRefTab = ( PHB_REFTAB ) pRefTab;

   hb_typeEnvInit( &env, ( PHB_REFTAB ) pRefTab, szFile );

   /* Seed parameter types from the reftab. When a previous scan pass
      refined a callee parameter from OBJECT to a specific class (e.g.
      ecr.prg pushes Transaction for ECRTran:oTransaction), this
      function's env needs to see that refined type so downstream call
      sites propagate it further. Without this, each intermediate
      function re-seeds from Hungarian → OBJECT, breaking the chain. */
   if( szFuncKey && pRefTab )
   {
      int nParams = hb_refTabParamCount( ( PHB_REFTAB ) pRefTab, szFuncKey );
      int i;
      for( i = 0; i < nParams; i++ )
      {
         const HB_REFPARAM * p = hb_refTabParam( ( PHB_REFTAB ) pRefTab,
                                                   szFuncKey, i );
         if( p && p->szName && p->szType &&
             hb_stricmp( p->szType, "USUAL" ) != 0 )
            hb_typeEnvSet( &env, p->szName, p->szType );
      }
   }

   /* Pass 0: Seed class DATA member types from all class definitions.
      This allows SELF:member expressions to resolve to specific types. */
   {
      PHB_AST_NODE pClass = pClassList;
      while( pClass )
      {
         if( pClass->type == HB_AST_CLASS )
         {
            PHB_AST_NODE pMember = pClass->value.asClass.pMembers;
            while( pMember )
            {
               if( pMember->type == HB_AST_CLASSDATA )
               {
                  const char * szType;
                  if( pMember->value.asClassData.szType )
                     szType = pMember->value.asClassData.szType;
                  else
                     szType = hb_astInferTypeFromInit(
                        pMember->value.asClassData.szName,
                        pMember->value.asClassData.szInit );
                  if( szType )
                     hb_typeEnvSet( &env, pMember->value.asClassData.szName, szType );
               }
               pMember = pMember->pNext;
            }
         }
         pClass = pClass->pNext;
      }
   }

   /* Pass 1: Seed from declarations.
      First try the env-aware inferencer on the initializer expression
      (so e.g. constructor patterns `LOCAL oCalc := Foo():New()` give
      the actual class name). Fall back to the simpler Hungarian-only
      inferencer for cases the env-aware one can't decide. */
   pStmt = pBody->value.asBlock.pFirst;
   while( pStmt )
   {
      if( pStmt->type == HB_AST_LOCAL || pStmt->type == HB_AST_STATIC || pStmt->type == HB_AST_PUBLIC || pStmt->type == HB_AST_PRIVATE )
      {
         const char * szType = NULL;
         /* W0024: an initializer whose type contradicts the variable's
            Hungarian prefix — `local aFoo := { => }` (init is a HASH but
            `a` says ARRAY). Same dedup as the assignment-statement check
            below, so the warning fires once per (line, name). */
         if( pStmt->value.asVar.pInit )
            hb_astCheckOneAssign( pStmt->value.asVar.szName,
                                  pStmt->value.asVar.pInit, &env, szFile,
                                  pStmt->iLine );
         if( pStmt->value.asVar.pInit )
            szType = hb_astInferExprType( pStmt->value.asVar.pInit, &env );
         if( ! szType )
            szType = hb_astInferType( pStmt->value.asVar.szName,
                                      pStmt->value.asVar.pInit );
         hb_typeEnvSet( &env, pStmt->value.asVar.szName, szType );
      }
      pStmt = pStmt->pNext;
   }

   /* Pass 1.5: emit W0024 where an assignment's RHS contradicts the
      lvalue's Hungarian prefix. Runs once on the Pass-1-seeded env so it
      sees the strict initial typing, not the lenient post-propagation
      one. Non-halting; codegen continues. */
   hb_astCheckHungarianMismatch( pBody, &env, szFile );

   /* Pass 2: Walk assignments and propagate (iterate until stable) */
   do
   {
      fChanged = HB_FALSE;
      hb_astPropagateBlock( pBody, &env, &fChanged );
   }
   while( fChanged );

   /* Pass 3: Update LOCAL/STATIC AST nodes whose propagated type is
      more specific than what Hungarian/initializer inference produced.
      "More specific" covers two cases:
        - declared as USUAL (any specific type wins)
        - declared as OBJECT but the env now has a concrete class name
          (e.g. `LOCAL oCalc := Foo():New()` — Hungarian gives OBJECT,
          but Pass 1's env-aware seeding records `Foo`) */
   pStmt = pBody->value.asBlock.pFirst;
   while( pStmt )
   {
      if( pStmt->type == HB_AST_LOCAL || pStmt->type == HB_AST_STATIC || pStmt->type == HB_AST_PUBLIC || pStmt->type == HB_AST_PRIVATE )
      {
         const char * szCurType = hb_astInferType( pStmt->value.asVar.szName,
                                                   pStmt->value.asVar.pInit );
         const char * szPropType = hb_typeEnvGet( &env, pStmt->value.asVar.szName );
         HB_BOOL fOverride = HB_FALSE;

         if( strcmp( szCurType, "USUAL" ) == 0 &&
             szPropType && strcmp( szPropType, "USUAL" ) != 0 )
            fOverride = HB_TRUE;
         else if( strcmp( szCurType, "OBJECT" ) == 0 &&
                  szPropType && strcmp( szPropType, "OBJECT" ) != 0 &&
                  strcmp( szPropType, "USUAL" ) != 0 )
            fOverride = HB_TRUE;
         /* Name-seeded class (weak hint from `o<ClassName>`): the env
            may hold a different class proved by an assignment — that
            evidence wins over the name-derived guess. */
         else if( hb_astIsNameSeededClass( pStmt->value.asVar.szName,
                                           szCurType ) &&
                  szPropType && strcmp( szPropType, szCurType ) != 0 &&
                  strcmp( szPropType, "USUAL" ) != 0 &&
                  strcmp( szPropType, "OBJECT" ) != 0 )
            fOverride = HB_TRUE;

         if( fOverride )
            pStmt->value.asVar.szAlias = szPropType;
      }
      pStmt = pStmt->pNext;
   }

   /* Pass 4: Infer return type from RETURN statements */
   hb_astCollectReturnTypes( pBody, &env, &szRetType, &fConflict, &fSawUnknown );

   /* Pass 5: Refine callee parameter types from call sites in this
      body. Only runs when the refTab is available (the scanner in
      -GF mode always supplies it; legacy callers that don't have a
      table get this skipped and behave as before). */
   if( env.pRefTab )
      hb_astRefineBlock( pBody, &env );

   {
      const char * szResult;
      if( fConflict )
         szResult = "USUAL";
      /* Mixed: at least one RETURN was uninferrable. The function is
         effectively polymorphic at the call site — don't pin it to the
         one branch we could type, or W0024 mis-fires at correctly-typed
         callers of the other branch (e.g. ADTRange returning either a
         string sentinel or an aADTRange array). Leaves "only
         uninferrable returns" as NULL — no info, reftab retains its
         prior default. */
      else if( fSawUnknown && szRetType )
         szResult = "USUAL";
      else
         szResult = szRetType;
      /* Pair with the entry save so an outer codegen-set reftab (or a
         nesting propagate call) is restored. */
      s_pPropRefTab = pSavedPropRefTab;
      return szResult;
   }
}
