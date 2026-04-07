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
         return "BLOCK";

      case HB_ET_SELF:
         return "OBJECT";

      case HB_EO_NEGATE:
         /* Negation of a numeric — check the operand */
         if( pExpr->value.asOperator.pLeft )
            return hb_astInferFromExpr( pExpr->value.asOperator.pLeft );
         return "NUMERIC";

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
static const char * hb_astInferFromPrefix( const char * szName )
{
   if( ! szName || ! szName[ 0 ] )
      return NULL;

   /* The prefix is the first character, which should be lowercase.
      If the name starts with uppercase or underscore, no prefix. */
   if( szName[ 0 ] >= 'a' && szName[ 0 ] <= 'z' &&
       szName[ 1 ] != '\0' )
   {
      /* Second character should be uppercase or a digit to confirm
         this is Hungarian notation, not just a short variable name */
      if( szName[ 1 ] >= 'A' && szName[ 1 ] <= 'Z' )
      {
         switch( szName[ 0 ] )
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
         }
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
#define HB_TYPEENV_MAX  256

typedef struct
{
   const char * szName;
   const char * szType;
} HB_TYPEENV_ENTRY;

typedef struct
{
   HB_TYPEENV_ENTRY entries[ HB_TYPEENV_MAX ];
   int count;
} HB_TYPEENV;

static void hb_typeEnvInit( HB_TYPEENV * pEnv )
{
   pEnv->count = 0;
}

static void hb_typeEnvSet( HB_TYPEENV * pEnv, const char * szName,
                           const char * szType )
{
   int i;

   /* Update existing entry */
   for( i = 0; i < pEnv->count; i++ )
   {
      if( hb_stricmp( pEnv->entries[ i ].szName, szName ) == 0 )
      {
         pEnv->entries[ i ].szType = szType;
         return;
      }
   }

   /* Add new entry */
   if( pEnv->count < HB_TYPEENV_MAX )
   {
      pEnv->entries[ pEnv->count ].szName = szName;
      pEnv->entries[ pEnv->count ].szType = szType;
      pEnv->count++;
   }
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

      case HB_EO_MINUS:
      case HB_EO_MULT:
      case HB_EO_DIV:
      case HB_EO_MOD:
      case HB_EO_POWER:
      {
         /* Arithmetic operators: infer from operands */
         const char * szLeft = hb_astInferExprType(
            pExpr->value.asOperator.pLeft, pEnv );
         const char * szRight = hb_astInferExprType(
            pExpr->value.asOperator.pRight, pEnv );

         if( ! szLeft || ! szRight )
            return NULL;

         /* DATE - DATE = NUMERIC (number of days) */
         if( pExpr->ExprType == HB_EO_MINUS &&
             strcmp( szLeft, "DATE" ) == 0 && strcmp( szRight, "DATE" ) == 0 )
            return "NUMERIC";

         /* NUMERIC op NUMERIC = NUMERIC */
         if( strcmp( szLeft, "NUMERIC" ) == 0 && strcmp( szRight, "NUMERIC" ) == 0 )
            return "NUMERIC";

         return NULL;
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
         /* Infer return types for well-known Harbour functions */
         if( pExpr->value.asFunCall.pFunName &&
             pExpr->value.asFunCall.pFunName->ExprType == HB_ET_FUNNAME )
         {
            const char * szFunc = pExpr->value.asFunCall.pFunName->value.asSymbol.name;
            /* Functions returning STRING */
            if( hb_stricmp( szFunc, "STR" ) == 0 ||
                hb_stricmp( szFunc, "UPPER" ) == 0 ||
                hb_stricmp( szFunc, "LOWER" ) == 0 ||
                hb_stricmp( szFunc, "TRIM" ) == 0 ||
                hb_stricmp( szFunc, "RTRIM" ) == 0 ||
                hb_stricmp( szFunc, "LTRIM" ) == 0 ||
                hb_stricmp( szFunc, "ALLTRIM" ) == 0 ||
                hb_stricmp( szFunc, "SUBSTR" ) == 0 ||
                hb_stricmp( szFunc, "LEFT" ) == 0 ||
                hb_stricmp( szFunc, "RIGHT" ) == 0 ||
                hb_stricmp( szFunc, "PADC" ) == 0 ||
                hb_stricmp( szFunc, "PADL" ) == 0 ||
                hb_stricmp( szFunc, "PADR" ) == 0 ||
                hb_stricmp( szFunc, "SPACE" ) == 0 ||
                hb_stricmp( szFunc, "REPLICATE" ) == 0 ||
                hb_stricmp( szFunc, "STRTRAN" ) == 0 ||
                hb_stricmp( szFunc, "STUFF" ) == 0 ||
                hb_stricmp( szFunc, "CHR" ) == 0 ||
                hb_stricmp( szFunc, "DTOC" ) == 0 ||
                hb_stricmp( szFunc, "DTOS" ) == 0 ||
                hb_stricmp( szFunc, "TIME" ) == 0 ||
                hb_stricmp( szFunc, "TRANSFORM" ) == 0 ||
                hb_stricmp( szFunc, "STRZERO" ) == 0 )
               return "STRING";
            /* Functions returning NUMERIC */
            if( hb_stricmp( szFunc, "LEN" ) == 0 ||
                hb_stricmp( szFunc, "VAL" ) == 0 ||
                hb_stricmp( szFunc, "ASC" ) == 0 ||
                hb_stricmp( szFunc, "AT" ) == 0 ||
                hb_stricmp( szFunc, "RAT" ) == 0 ||
                hb_stricmp( szFunc, "INT" ) == 0 ||
                hb_stricmp( szFunc, "ROUND" ) == 0 ||
                hb_stricmp( szFunc, "ABS" ) == 0 ||
                hb_stricmp( szFunc, "MAX" ) == 0 ||
                hb_stricmp( szFunc, "MIN" ) == 0 ||
                hb_stricmp( szFunc, "MOD" ) == 0 ||
                hb_stricmp( szFunc, "SQRT" ) == 0 ||
                hb_stricmp( szFunc, "LOG" ) == 0 ||
                hb_stricmp( szFunc, "EXP" ) == 0 )
               return "NUMERIC";
            /* Functions returning LOGICAL */
            if( hb_stricmp( szFunc, "EMPTY" ) == 0 ||
                hb_stricmp( szFunc, "FILE" ) == 0 ||
                hb_stricmp( szFunc, "ISDIGIT" ) == 0 ||
                hb_stricmp( szFunc, "ISALPHA" ) == 0 ||
                hb_stricmp( szFunc, "ISUPPER" ) == 0 ||
                hb_stricmp( szFunc, "ISLOWER" ) == 0 )
               return "LOGICAL";
            /* Functions returning DATE */
            if( hb_stricmp( szFunc, "DATE" ) == 0 ||
                hb_stricmp( szFunc, "CTOD" ) == 0 ||
                hb_stricmp( szFunc, "STOD" ) == 0 )
               return "DATE";
            /* Functions returning ARRAY */
            if( hb_stricmp( szFunc, "ARRAY" ) == 0 ||
                hb_stricmp( szFunc, "ACLONE" ) == 0 ||
                hb_stricmp( szFunc, "ASORT" ) == 0 ||
                hb_stricmp( szFunc, "DIRECTORY" ) == 0 )
               return "ARRAY";
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

   if( ! szCurType || strcmp( szCurType, "USUAL" ) == 0 )
   {
      const char * szNewType = hb_astInferExprType( pRHS, pEnv );
      if( szNewType && ( ! szCurType || strcmp( szCurType, szNewType ) != 0 ) )
      {
         hb_typeEnvSet( pEnv, szVarName, szNewType );
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

/* Recursively collect RETURN expression types from a block */
static void hb_astCollectReturnTypes( PHB_AST_NODE pBlock, HB_TYPEENV * pEnv,
                                      const char ** pszRetType, HB_BOOL * pfConflict )
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
      }
      /* Recurse into nested structures */
      else if( pStmt->type == HB_AST_IF )
      {
         hb_astCollectReturnTypes( pStmt->value.asIf.pThen, pEnv, pszRetType, pfConflict );
         {
            PHB_AST_NODE pElseIf = pStmt->value.asIf.pElseIfs;
            while( pElseIf )
            {
               hb_astCollectReturnTypes( pElseIf->value.asElseIf.pBody, pEnv, pszRetType, pfConflict );
               pElseIf = pElseIf->pNext;
            }
         }
         hb_astCollectReturnTypes( pStmt->value.asIf.pElse, pEnv, pszRetType, pfConflict );
      }
      else if( pStmt->type == HB_AST_DOWHILE )
         hb_astCollectReturnTypes( pStmt->value.asWhile.pBody, pEnv, pszRetType, pfConflict );
      else if( pStmt->type == HB_AST_FOR )
         hb_astCollectReturnTypes( pStmt->value.asFor.pBody, pEnv, pszRetType, pfConflict );
      else if( pStmt->type == HB_AST_FOREACH )
         hb_astCollectReturnTypes( pStmt->value.asForEach.pBody, pEnv, pszRetType, pfConflict );
      else if( pStmt->type == HB_AST_DOCASE )
      {
         PHB_AST_NODE pCase = pStmt->value.asDoCase.pCases;
         while( pCase )
         {
            hb_astCollectReturnTypes( pCase->value.asCase.pBody, pEnv, pszRetType, pfConflict );
            pCase = pCase->pNext;
         }
         hb_astCollectReturnTypes( pStmt->value.asDoCase.pOtherwise, pEnv, pszRetType, pfConflict );
      }
      else if( pStmt->type == HB_AST_SWITCH )
      {
         PHB_AST_NODE pCase = pStmt->value.asSwitch.pCases;
         while( pCase )
         {
            hb_astCollectReturnTypes( pCase->value.asCase.pBody, pEnv, pszRetType, pfConflict );
            pCase = pCase->pNext;
         }
         hb_astCollectReturnTypes( pStmt->value.asSwitch.pDefault, pEnv, pszRetType, pfConflict );
      }
      else if( pStmt->type == HB_AST_BEGINSEQ )
      {
         hb_astCollectReturnTypes( pStmt->value.asSeq.pBody, pEnv, pszRetType, pfConflict );
         hb_astCollectReturnTypes( pStmt->value.asSeq.pRecover, pEnv, pszRetType, pfConflict );
      }

      pStmt = pStmt->pNext;
   }
}

/*
 * Run type propagation on a function's AST body.
 *
 * Pass 1: Seed the type environment from LOCAL/STATIC declarations
 * Pass 2: Walk expression statements and infer types for USUAL variables
 * Pass 3: Update LOCAL/STATIC AST nodes with propagated types
 * Pass 4: Infer function return type from RETURN statements
 *
 * Returns the inferred return type string, or NULL if it can't be determined.
 */
const char * hb_astPropagate( PHB_AST_NODE pBody, PHB_AST_NODE pClassList )
{
   HB_TYPEENV env;
   PHB_AST_NODE pStmt;
   HB_BOOL fChanged;
   const char * szRetType = NULL;
   HB_BOOL fConflict = HB_FALSE;

   if( ! pBody || pBody->type != HB_AST_BLOCK )
      return NULL;

   hb_typeEnvInit( &env );

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

   /* Pass 1: Seed from declarations */
   pStmt = pBody->value.asBlock.pFirst;
   while( pStmt )
   {
      if( pStmt->type == HB_AST_LOCAL || pStmt->type == HB_AST_STATIC )
      {
         const char * szType = hb_astInferType( pStmt->value.asVar.szName,
                                                pStmt->value.asVar.pInit );
         hb_typeEnvSet( &env, pStmt->value.asVar.szName, szType );
      }
      pStmt = pStmt->pNext;
   }

   /* Pass 2: Walk assignments and propagate (iterate until stable) */
   do
   {
      fChanged = HB_FALSE;
      hb_astPropagateBlock( pBody, &env, &fChanged );
   }
   while( fChanged );

   /* Pass 3: Update LOCAL/STATIC AST nodes that were USUAL */
   pStmt = pBody->value.asBlock.pFirst;
   while( pStmt )
   {
      if( pStmt->type == HB_AST_LOCAL || pStmt->type == HB_AST_STATIC )
      {
         const char * szCurType = hb_astInferType( pStmt->value.asVar.szName,
                                                   pStmt->value.asVar.pInit );
         if( strcmp( szCurType, "USUAL" ) == 0 )
         {
            const char * szPropType = hb_typeEnvGet( &env, pStmt->value.asVar.szName );
            if( szPropType && strcmp( szPropType, "USUAL" ) != 0 )
            {
               /* Store the propagated type in the alias field (reuse) */
               pStmt->value.asVar.szAlias = szPropType;
            }
         }
      }
      pStmt = pStmt->pNext;
   }

   /* Pass 4: Infer return type from RETURN statements */
   hb_astCollectReturnTypes( pBody, &env, &szRetType, &fConflict );
   if( fConflict )
      return "USUAL";

   return szRetType;
}
