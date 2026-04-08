/*
 * Harbour Transpiler - AST builder implementation
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

/* Allocate a new AST node */
PHB_AST_NODE hb_astNew( HB_AST_TYPE type, int iLine )
{
   PHB_AST_NODE pNode = ( PHB_AST_NODE ) hb_xgrabz( sizeof( HB_AST_NODE ) );

   pNode->type  = type;
   pNode->iLine = iLine;
   pNode->pNext = NULL;

   return pNode;
}

/* Free a single AST node (not recursive - caller manages children) */
void hb_astFree( PHB_AST_NODE pNode )
{
   if( pNode )
      hb_xfree( pNode );
}

/* Free a linked list of AST nodes (follows pNext) */
void hb_astFreeList( PHB_AST_NODE pNode )
{
   while( pNode )
   {
      PHB_AST_NODE pNext = pNode->pNext;
      /* TODO: recursively free children based on type */
      hb_xfree( pNode );
      pNode = pNext;
   }
}

/* Initialize AST builder state */
void hb_astInit( HB_COMP_DECL )
{
   HB_COMP_PARAM->ast.pFuncList  = NULL;
   HB_COMP_PARAM->ast.pFuncLast  = NULL;
   HB_COMP_PARAM->ast.pCurrFunc  = NULL;
   HB_COMP_PARAM->ast.pCurrBlock = NULL;
   HB_COMP_PARAM->ast.iBlockTop  = -1;
}

/* Cleanup AST builder state and free all nodes */
void hb_astCleanup( HB_COMP_DECL )
{
   hb_astFreeList( ( PHB_AST_NODE ) HB_COMP_PARAM->ast.pFuncList );
   HB_COMP_PARAM->ast.pFuncList  = NULL;
   HB_COMP_PARAM->ast.pFuncLast  = NULL;
   HB_COMP_PARAM->ast.pCurrFunc  = NULL;
   HB_COMP_PARAM->ast.pCurrBlock = NULL;
   HB_COMP_PARAM->ast.iBlockTop  = -1;
}

/* Push current block onto stack, start a new empty block */
void hb_astPushBlock( HB_COMP_DECL )
{
   PHB_AST_NODE pBlock;

   if( HB_COMP_PARAM->ast.iBlockTop >= 63 )
      return;

   HB_COMP_PARAM->ast.iBlockTop++;
   HB_COMP_PARAM->ast.aBlockStack[ HB_COMP_PARAM->ast.iBlockTop ] =
      HB_COMP_PARAM->ast.pCurrBlock;

   pBlock = hb_astNew( HB_AST_BLOCK, 0 );
   HB_COMP_PARAM->ast.pCurrBlock = ( struct _HB_AST_NODE * ) pBlock;
}

/* Pop block from stack, return the collected block node */
PHB_AST_NODE hb_astPopBlock( HB_COMP_DECL )
{
   PHB_AST_NODE pBlock;

   if( HB_COMP_PARAM->ast.iBlockTop < 0 )
      return NULL;

   pBlock = ( PHB_AST_NODE ) HB_COMP_PARAM->ast.pCurrBlock;
   HB_COMP_PARAM->ast.pCurrBlock =
      HB_COMP_PARAM->ast.aBlockStack[ HB_COMP_PARAM->ast.iBlockTop ];
   HB_COMP_PARAM->ast.iBlockTop--;

   return pBlock;
}

/* Append a statement node to the current block */
void hb_astAppend( HB_COMP_DECL, PHB_AST_NODE pNode )
{
   PHB_AST_NODE pBlock = ( PHB_AST_NODE ) HB_COMP_PARAM->ast.pCurrBlock;

   if( ! pBlock || ! pNode )
      return;

   if( pBlock->value.asBlock.pLast )
   {
      pBlock->value.asBlock.pLast->pNext = pNode;
   }
   else
   {
      pBlock->value.asBlock.pFirst = pNode;
   }
   pBlock->value.asBlock.pLast = pNode;
   pNode->pNext = NULL;
}

/* Append a node to the startup function's body (top-level scope).
   Used for CLASS nodes which must always be at file scope regardless
   of which function body is currently being parsed. */
void hb_astAppendToStartup( HB_COMP_DECL, PHB_AST_NODE pNode )
{
   PHB_AST_NODE pStartup = ( PHB_AST_NODE ) HB_COMP_PARAM->ast.pFuncList;
   PHB_AST_NODE pBody;

   if( ! pStartup || ! pNode )
      return;

   pBody = pStartup->value.asFunc.pBody;
   if( ! pBody )
   {
      /* Startup function body hasn't been created yet — use current block */
      hb_astAppend( HB_COMP_PARAM, pNode );
      return;
   }

   if( pBody->value.asBlock.pLast )
      pBody->value.asBlock.pLast->pNext = pNode;
   else
      pBody->value.asBlock.pFirst = pNode;
   pBody->value.asBlock.pLast = pNode;
   pNode->pNext = NULL;
}

/* Begin a new function definition */
PHB_AST_NODE hb_astBeginFunc( HB_COMP_DECL,
                              const char * szName,
                              HB_BOOL fProcedure,
                              HB_SYMBOLSCOPE cScope,
                              int iLine )
{
   PHB_AST_NODE pFunc = hb_astNew( HB_AST_FUNCTION, iLine );

   pFunc->value.asFunc.szName     = szName;
   pFunc->value.asFunc.fProcedure = fProcedure;
   pFunc->value.asFunc.cScope     = cScope;
   pFunc->value.asFunc.pParams    = NULL;
   pFunc->value.asFunc.pBody      = NULL;

   /* Add to function list */
   if( HB_COMP_PARAM->ast.pFuncLast )
      ( ( PHB_AST_NODE ) HB_COMP_PARAM->ast.pFuncLast )->pNext = pFunc;
   else
      HB_COMP_PARAM->ast.pFuncList = ( struct _HB_AST_NODE * ) pFunc;
   HB_COMP_PARAM->ast.pFuncLast = ( struct _HB_AST_NODE * ) pFunc;
   HB_COMP_PARAM->ast.pCurrFunc = ( struct _HB_AST_NODE * ) pFunc;

   /* Push a new block for the function body */
   hb_astPushBlock( HB_COMP_PARAM );

   return pFunc;
}

/* End the current function definition */
void hb_astEndFunc( HB_COMP_DECL )
{
   PHB_AST_NODE pCurrFunc = ( PHB_AST_NODE ) HB_COMP_PARAM->ast.pCurrFunc;

   if( pCurrFunc )
   {
      PHB_AST_NODE pBody = hb_astPopBlock( HB_COMP_PARAM );
      pCurrFunc->value.asFunc.pBody = pBody;

      /* Capture parameter and local variable lists from compiler's function */
      if( HB_COMP_PARAM->functions.pLast )
         pCurrFunc->value.asFunc.pParams = HB_COMP_PARAM->functions.pLast->pLocals;

      HB_COMP_PARAM->ast.pCurrFunc = NULL;
   }
}

/* Add an expression statement to the current block */
void hb_astAddExprStmt( HB_COMP_DECL, PHB_EXPR pExpr, int iLine )
{
   if( HB_COMP_ISAST( HB_COMP_PARAM ) && pExpr )
   {
      /* Check suppression flag (set when VarDef with initializer is captured) */
      if( HB_COMP_PARAM->ast.fSuppressExprStmt )
      {
         HB_COMP_PARAM->ast.fSuppressExprStmt = HB_FALSE;
         return;
      }
      {
         PHB_AST_NODE pNode = hb_astNew( HB_AST_EXPRSTMT, iLine );
         pNode->value.asExprStmt.pExpr = pExpr;
         hb_astAppend( HB_COMP_PARAM, pNode );
      }
   }
}

/* Add a RETURN statement to the current block */
void hb_astAddReturn( HB_COMP_DECL, PHB_EXPR pExpr, int iLine )
{
   if( HB_COMP_ISAST( HB_COMP_PARAM ) )
   {
      PHB_AST_NODE pNode = hb_astNew( HB_AST_RETURN, iLine );
      pNode->value.asReturn.pExpr = pExpr;
      hb_astAppend( HB_COMP_PARAM, pNode );
   }
}

/* Add a LOCAL variable declaration to the current block */
void hb_astAddLocal( HB_COMP_DECL, const char * szName,
                     PHB_EXPR pInit, int iLine )
{
   if( HB_COMP_ISAST( HB_COMP_PARAM ) )
   {
      PHB_AST_NODE pNode = hb_astNew( HB_AST_LOCAL, iLine );
      pNode->value.asVar.szName = szName;
      pNode->value.asVar.pInit  = pInit;
      pNode->value.asVar.szAlias = NULL;
      hb_astAppend( HB_COMP_PARAM, pNode );
   }
}

/* Add a STATIC variable declaration to the current block */
void hb_astAddStatic( HB_COMP_DECL, const char * szName,
                      PHB_EXPR pInit, int iLine )
{
   if( HB_COMP_ISAST( HB_COMP_PARAM ) )
   {
      PHB_AST_NODE pNode = hb_astNew( HB_AST_STATIC, iLine );
      pNode->value.asVar.szName = szName;
      pNode->value.asVar.pInit  = pInit;
      pNode->value.asVar.szAlias = NULL;
      hb_astAppend( HB_COMP_PARAM, pNode );
   }
}

/* === IF / ELSEIF / ELSE / ENDIF === */

/* Called at IfBegin: save condition, push block for then-body */
void hb_astBeginIf( HB_COMP_DECL, PHB_EXPR pCondition, int iLine )
{
   if( HB_COMP_ISAST( HB_COMP_PARAM ) )
   {
      PHB_AST_NODE pNode = hb_astNew( HB_AST_IF, iLine );
      pNode->value.asIf.pCondition = pCondition;
      pNode->value.asIf.pThen      = NULL;
      pNode->value.asIf.pElseIfs   = NULL;
      pNode->value.asIf.pElse      = NULL;

      /* Push the IF node itself onto the stack so we can find it later,
         then push a new block for the then-body */
      hb_astPushBlock( HB_COMP_PARAM );
      /* Store the IF node as the first item in this placeholder block */
      hb_astAppend( HB_COMP_PARAM, pNode );
      /* Push another block for the actual then-body statements */
      hb_astPushBlock( HB_COMP_PARAM );
   }
}

/* Called at IfElse: pop then-body, push block for else-body */
void hb_astBeginElse( HB_COMP_DECL )
{
   if( HB_COMP_ISAST( HB_COMP_PARAM ) )
   {
      PHB_AST_NODE pIfBlock, pIfNode;
      PHB_AST_NODE pBody = hb_astPopBlock( HB_COMP_PARAM );

      /* Peek at the IF node in the placeholder block */
      pIfBlock = ( PHB_AST_NODE ) HB_COMP_PARAM->ast.pCurrBlock;
      pIfNode = pIfBlock->value.asBlock.pFirst;

      /* Assign the popped body: to then-block or last elseif body */
      if( ! pIfNode->value.asIf.pThen )
      {
         pIfNode->value.asIf.pThen = pBody;
      }
      else if( pIfNode->value.asIf.pElseIfs )
      {
         PHB_AST_NODE pLast = pIfNode->value.asIf.pElseIfs;
         while( pLast->pNext )
            pLast = pLast->pNext;
         pLast->value.asElseIf.pBody = pBody;
      }

      /* Push block for else-body */
      hb_astPushBlock( HB_COMP_PARAM );
   }
}

/* Called at IfElseIf: pop current body, start new elseif */
void hb_astBeginElseIf( HB_COMP_DECL, PHB_EXPR pCondition, int iLine )
{
   if( HB_COMP_ISAST( HB_COMP_PARAM ) )
   {
      PHB_AST_NODE pIfBlock, pIfNode, pElseIf;
      PHB_AST_NODE pBody = hb_astPopBlock( HB_COMP_PARAM );

      /* Get the IF node */
      pIfBlock = ( PHB_AST_NODE ) HB_COMP_PARAM->ast.pCurrBlock;
      pIfNode = pIfBlock->value.asBlock.pFirst;

      /* Assign the popped body to the right place */
      if( ! pIfNode->value.asIf.pThen )
      {
         /* First ELSEIF: popped body is the then-body */
         pIfNode->value.asIf.pThen = pBody;
      }
      else
      {
         /* Subsequent ELSEIF: popped body belongs to previous ELSEIF */
         PHB_AST_NODE pLast = pIfNode->value.asIf.pElseIfs;
         while( pLast && pLast->pNext )
            pLast = pLast->pNext;
         if( pLast )
            pLast->value.asElseIf.pBody = pBody;
      }

      /* Create ELSEIF node and append to chain */
      pElseIf = hb_astNew( HB_AST_ELSEIF, iLine );
      pElseIf->value.asElseIf.pCondition = pCondition;
      pElseIf->value.asElseIf.pBody = NULL;

      if( pIfNode->value.asIf.pElseIfs )
      {
         PHB_AST_NODE pLast = pIfNode->value.asIf.pElseIfs;
         while( pLast->pNext )
            pLast = pLast->pNext;
         pLast->pNext = pElseIf;
      }
      else
         pIfNode->value.asIf.pElseIfs = pElseIf;

      /* Push block for this elseif's body */
      hb_astPushBlock( HB_COMP_PARAM );
   }
}

/* Called at EndIf: pop final body, assemble complete IF node, append to parent */
void hb_astEndIf( HB_COMP_DECL, HB_BOOL fElse, HB_BOOL fElseIf )
{
   if( HB_COMP_ISAST( HB_COMP_PARAM ) )
   {
      PHB_AST_NODE pIfBlock, pIfNode;
      PHB_AST_NODE pBody = hb_astPopBlock( HB_COMP_PARAM );

      /* Get the IF node from the placeholder block */
      pIfBlock = ( PHB_AST_NODE ) HB_COMP_PARAM->ast.pCurrBlock;
      pIfNode = pIfBlock->value.asBlock.pFirst;

      if( fElse )
      {
         pIfNode->value.asIf.pElse = pBody;
      }
      else if( fElseIf )
      {
         /* Set the body of the last ELSEIF */
         PHB_AST_NODE pLast = pIfNode->value.asIf.pElseIfs;
         if( pLast )
         {
            while( pLast->pNext )
               pLast = pLast->pNext;
            pLast->value.asElseIf.pBody = pBody;
         }
      }
      else
      {
         /* Simple IF...ENDIF — pBody is the then-body */
         pIfNode->value.asIf.pThen = pBody;
      }

      /* Pop the placeholder block containing the IF node */
      hb_astPopBlock( HB_COMP_PARAM );

      /* Append the completed IF node to the parent block */
      hb_astAppend( HB_COMP_PARAM, pIfNode );
   }
}

/* === DO WHILE / ENDDO === */

void hb_astBeginWhile( HB_COMP_DECL, PHB_EXPR pCondition, int iLine )
{
   if( HB_COMP_ISAST( HB_COMP_PARAM ) )
   {
      PHB_AST_NODE pNode = hb_astNew( HB_AST_DOWHILE, iLine );
      pNode->value.asWhile.pCondition = pCondition;
      pNode->value.asWhile.pBody = NULL;

      hb_astPushBlock( HB_COMP_PARAM );
      hb_astAppend( HB_COMP_PARAM, pNode );
      hb_astPushBlock( HB_COMP_PARAM );
   }
}

void hb_astEndWhile( HB_COMP_DECL )
{
   if( HB_COMP_ISAST( HB_COMP_PARAM ) )
   {
      PHB_AST_NODE pWhileBlock, pWhileNode;
      PHB_AST_NODE pBody = hb_astPopBlock( HB_COMP_PARAM );

      pWhileBlock = ( PHB_AST_NODE ) HB_COMP_PARAM->ast.pCurrBlock;
      pWhileNode = pWhileBlock->value.asBlock.pFirst;
      pWhileNode->value.asWhile.pBody = pBody;

      hb_astPopBlock( HB_COMP_PARAM );
      hb_astAppend( HB_COMP_PARAM, pWhileNode );
   }
}

/* === FOR / NEXT === */

void hb_astBeginFor( HB_COMP_DECL, const char * szVar,
                     PHB_EXPR pStart, PHB_EXPR pEnd, PHB_EXPR pStep, int iLine )
{
   if( HB_COMP_ISAST( HB_COMP_PARAM ) )
   {
      PHB_AST_NODE pNode = hb_astNew( HB_AST_FOR, iLine );
      pNode->value.asFor.szVar  = szVar;
      pNode->value.asFor.pStart = pStart;
      pNode->value.asFor.pEnd   = pEnd;
      pNode->value.asFor.pStep  = pStep;
      pNode->value.asFor.pBody  = NULL;

      hb_astPushBlock( HB_COMP_PARAM );
      hb_astAppend( HB_COMP_PARAM, pNode );
      hb_astPushBlock( HB_COMP_PARAM );
   }
}

void hb_astEndFor( HB_COMP_DECL )
{
   if( HB_COMP_ISAST( HB_COMP_PARAM ) )
   {
      PHB_AST_NODE pForBlock, pForNode;
      PHB_AST_NODE pBody = hb_astPopBlock( HB_COMP_PARAM );

      pForBlock = ( PHB_AST_NODE ) HB_COMP_PARAM->ast.pCurrBlock;
      pForNode = pForBlock->value.asBlock.pFirst;
      pForNode->value.asFor.pBody = pBody;

      hb_astPopBlock( HB_COMP_PARAM );
      hb_astAppend( HB_COMP_PARAM, pForNode );
   }
}

/* === SWITCH / CASE / DEFAULT / ENDSWITCH === */

void hb_astBeginSwitch( HB_COMP_DECL, PHB_EXPR pSwitch, int iLine )
{
   if( HB_COMP_ISAST( HB_COMP_PARAM ) )
   {
      PHB_AST_NODE pNode = hb_astNew( HB_AST_SWITCH, iLine );
      pNode->value.asSwitch.pSwitch  = pSwitch;
      pNode->value.asSwitch.pCases   = NULL;
      pNode->value.asSwitch.pDefault = NULL;

      hb_astPushBlock( HB_COMP_PARAM );
      hb_astAppend( HB_COMP_PARAM, pNode );
   }
}

void hb_astAddSwitchCase( HB_COMP_DECL, PHB_EXPR pValue, int iLine )
{
   if( HB_COMP_ISAST( HB_COMP_PARAM ) )
   {
      PHB_AST_NODE pSwitchBlock, pSwitchNode, pCase, pLast;

      /* Close previous case body if any */
      if( HB_COMP_PARAM->ast.iBlockTop >= 0 )
      {
         PHB_AST_NODE pParent = ( PHB_AST_NODE )
            HB_COMP_PARAM->ast.aBlockStack[ HB_COMP_PARAM->ast.iBlockTop ];
         if( pParent && pParent->value.asBlock.pFirst &&
             pParent->value.asBlock.pFirst->type == HB_AST_SWITCH )
         {
            PHB_AST_NODE pBody = hb_astPopBlock( HB_COMP_PARAM );
            pSwitchBlock = ( PHB_AST_NODE ) HB_COMP_PARAM->ast.pCurrBlock;
            pSwitchNode = pSwitchBlock->value.asBlock.pFirst;
            pLast = pSwitchNode->value.asSwitch.pCases;
            if( pLast )
            {
               while( pLast->pNext )
                  pLast = pLast->pNext;
               pLast->value.asCase.pBody = pBody;
            }
         }
      }

      /* Create CASE node */
      pCase = hb_astNew( HB_AST_CASE, iLine );
      pCase->value.asCase.pCondition = pValue;
      pCase->value.asCase.pBody = NULL;

      /* Get the SWITCH node and add case */
      pSwitchBlock = ( PHB_AST_NODE ) HB_COMP_PARAM->ast.pCurrBlock;
      pSwitchNode = pSwitchBlock->value.asBlock.pFirst;
      if( pSwitchNode->value.asSwitch.pCases )
      {
         pLast = pSwitchNode->value.asSwitch.pCases;
         while( pLast->pNext )
            pLast = pLast->pNext;
         pLast->pNext = pCase;
      }
      else
         pSwitchNode->value.asSwitch.pCases = pCase;

      /* Push block for this case's body */
      hb_astPushBlock( HB_COMP_PARAM );
   }
}

void hb_astAddSwitchDefault( HB_COMP_DECL, int iLine )
{
   hb_astAddSwitchCase( HB_COMP_PARAM, NULL, iLine );
}

void hb_astEndSwitch( HB_COMP_DECL )
{
   if( HB_COMP_ISAST( HB_COMP_PARAM ) )
   {
      PHB_AST_NODE pSwitchBlock, pSwitchNode, pLast;
      PHB_AST_NODE pBody = hb_astPopBlock( HB_COMP_PARAM );

      pSwitchBlock = ( PHB_AST_NODE ) HB_COMP_PARAM->ast.pCurrBlock;
      pSwitchNode = pSwitchBlock->value.asBlock.pFirst;

      pLast = pSwitchNode->value.asSwitch.pCases;
      if( pLast )
      {
         while( pLast->pNext )
            pLast = pLast->pNext;
         if( pLast->value.asCase.pCondition == NULL )
            pSwitchNode->value.asSwitch.pDefault = pBody;
         else
            pLast->value.asCase.pBody = pBody;
      }

      hb_astPopBlock( HB_COMP_PARAM );
      hb_astAppend( HB_COMP_PARAM, pSwitchNode );
   }
}

/* === WITH OBJECT / END WITH === */

void hb_astBeginWithObject( HB_COMP_DECL, PHB_EXPR pObject, int iLine )
{
   if( HB_COMP_ISAST( HB_COMP_PARAM ) )
   {
      PHB_AST_NODE pNode = hb_astNew( HB_AST_WITHOBJECT, iLine );
      pNode->value.asWithObj.pObject = pObject;
      pNode->value.asWithObj.pBody   = NULL;

      hb_astPushBlock( HB_COMP_PARAM );
      hb_astAppend( HB_COMP_PARAM, pNode );
      hb_astPushBlock( HB_COMP_PARAM );
   }
}

void hb_astEndWithObject( HB_COMP_DECL )
{
   if( HB_COMP_ISAST( HB_COMP_PARAM ) )
   {
      PHB_AST_NODE pBlock, pNode;
      PHB_AST_NODE pBody = hb_astPopBlock( HB_COMP_PARAM );

      pBlock = ( PHB_AST_NODE ) HB_COMP_PARAM->ast.pCurrBlock;
      pNode = pBlock->value.asBlock.pFirst;
      pNode->value.asWithObj.pBody = pBody;

      hb_astPopBlock( HB_COMP_PARAM );
      hb_astAppend( HB_COMP_PARAM, pNode );
   }
}

/* === EXIT / LOOP === */

void hb_astAddExit( HB_COMP_DECL, int iLine )
{
   if( HB_COMP_ISAST( HB_COMP_PARAM ) )
      hb_astAppend( HB_COMP_PARAM, hb_astNew( HB_AST_EXIT, iLine ) );
}

void hb_astAddLoop( HB_COMP_DECL, int iLine )
{
   if( HB_COMP_ISAST( HB_COMP_PARAM ) )
      hb_astAppend( HB_COMP_PARAM, hb_astNew( HB_AST_LOOP, iLine ) );
}

/* === DO CASE / CASE / OTHERWISE / ENDCASE === */

void hb_astBeginDoCase( HB_COMP_DECL, int iLine )
{
   if( HB_COMP_ISAST( HB_COMP_PARAM ) )
   {
      PHB_AST_NODE pNode = hb_astNew( HB_AST_DOCASE, iLine );
      pNode->value.asDoCase.pCases = NULL;
      pNode->value.asDoCase.pOtherwise = NULL;

      hb_astPushBlock( HB_COMP_PARAM );
      hb_astAppend( HB_COMP_PARAM, pNode );
   }
}

void hb_astAddCase( HB_COMP_DECL, PHB_EXPR pCondition, int iLine )
{
   if( HB_COMP_ISAST( HB_COMP_PARAM ) )
   {
      PHB_AST_NODE pDoCaseBlock, pDoCaseNode, pCase, pLast;

      /* Close previous case body if any */
      if( HB_COMP_PARAM->ast.iBlockTop >= 0 )
      {
         PHB_AST_NODE pCurrBlock = ( PHB_AST_NODE ) HB_COMP_PARAM->ast.pCurrBlock;
         /* Check if we're inside a case body block (not the docase placeholder) */
         pDoCaseBlock = ( PHB_AST_NODE ) HB_COMP_PARAM->ast.aBlockStack[ HB_COMP_PARAM->ast.iBlockTop ];
         pDoCaseNode = pDoCaseBlock->value.asBlock.pFirst;
         if( pDoCaseNode && pDoCaseNode->type == HB_AST_DOCASE )
         {
            /* Find last case and set its body */
            pLast = pDoCaseNode->value.asDoCase.pCases;
            if( pLast )
            {
               while( pLast->pNext )
                  pLast = pLast->pNext;
               pLast->value.asCase.pBody = pCurrBlock;
               /* Pop old case body block */
               hb_astPopBlock( HB_COMP_PARAM );
            }
         }
      }

      /* Create CASE node */
      pCase = hb_astNew( HB_AST_CASE, iLine );
      pCase->value.asCase.pCondition = pCondition;
      pCase->value.asCase.pBody = NULL;

      /* Add to case chain */
      pDoCaseBlock = ( PHB_AST_NODE ) HB_COMP_PARAM->ast.pCurrBlock;
      pDoCaseNode = pDoCaseBlock->value.asBlock.pFirst;
      if( pDoCaseNode->value.asDoCase.pCases )
      {
         pLast = pDoCaseNode->value.asDoCase.pCases;
         while( pLast->pNext )
            pLast = pLast->pNext;
         pLast->pNext = pCase;
      }
      else
         pDoCaseNode->value.asDoCase.pCases = pCase;

      /* Push block for this case's body */
      hb_astPushBlock( HB_COMP_PARAM );
   }
}

void hb_astBeginOtherwise( HB_COMP_DECL )
{
   if( HB_COMP_ISAST( HB_COMP_PARAM ) )
   {
      PHB_AST_NODE pDoCaseBlock, pDoCaseNode, pLast;

      /* Close previous case body */
      pDoCaseBlock = ( PHB_AST_NODE ) HB_COMP_PARAM->ast.aBlockStack[ HB_COMP_PARAM->ast.iBlockTop ];
      pDoCaseNode = pDoCaseBlock->value.asBlock.pFirst;
      if( pDoCaseNode && pDoCaseNode->type == HB_AST_DOCASE )
      {
         pLast = pDoCaseNode->value.asDoCase.pCases;
         if( pLast )
         {
            while( pLast->pNext )
               pLast = pLast->pNext;
            pLast->value.asCase.pBody = ( PHB_AST_NODE ) HB_COMP_PARAM->ast.pCurrBlock;
            hb_astPopBlock( HB_COMP_PARAM );
         }
      }

      /* Push block for otherwise body */
      hb_astPushBlock( HB_COMP_PARAM );
   }
}

void hb_astEndDoCase( HB_COMP_DECL, HB_BOOL fOtherwise )
{
   if( HB_COMP_ISAST( HB_COMP_PARAM ) )
   {
      PHB_AST_NODE pDoCaseBlock, pDoCaseNode;
      PHB_AST_NODE pBody = hb_astPopBlock( HB_COMP_PARAM );

      /* Get the DOCASE node */
      pDoCaseBlock = ( PHB_AST_NODE ) HB_COMP_PARAM->ast.pCurrBlock;
      pDoCaseNode = pDoCaseBlock->value.asBlock.pFirst;

      if( fOtherwise )
      {
         pDoCaseNode->value.asDoCase.pOtherwise = pBody;
      }
      else if( pDoCaseNode->value.asDoCase.pCases )
      {
         /* Set body of last CASE */
         PHB_AST_NODE pLast = pDoCaseNode->value.asDoCase.pCases;
         while( pLast->pNext )
            pLast = pLast->pNext;
         pLast->value.asCase.pBody = pBody;
      }

      /* Pop the DOCASE placeholder block */
      hb_astPopBlock( HB_COMP_PARAM );
      hb_astAppend( HB_COMP_PARAM, pDoCaseNode );
   }
}

/* === BEGIN SEQUENCE / RECOVER / ALWAYS / END === */

void hb_astBeginSeq( HB_COMP_DECL, int iLine )
{
   if( HB_COMP_ISAST( HB_COMP_PARAM ) )
   {
      PHB_AST_NODE pNode = hb_astNew( HB_AST_BEGINSEQ, iLine );
      pNode->value.asSeq.pBody       = NULL;
      pNode->value.asSeq.pRecover    = NULL;
      pNode->value.asSeq.szRecoverVar = NULL;
      pNode->value.asSeq.pAlways     = NULL;

      hb_astPushBlock( HB_COMP_PARAM );
      hb_astAppend( HB_COMP_PARAM, pNode );
      hb_astPushBlock( HB_COMP_PARAM );
   }
}

void hb_astBeginRecover( HB_COMP_DECL, const char * szVar )
{
   if( HB_COMP_ISAST( HB_COMP_PARAM ) )
   {
      PHB_AST_NODE pSeqBlock, pSeqNode;
      PHB_AST_NODE pBody = hb_astPopBlock( HB_COMP_PARAM );

      pSeqBlock = ( PHB_AST_NODE ) HB_COMP_PARAM->ast.pCurrBlock;
      pSeqNode = pSeqBlock->value.asBlock.pFirst;
      pSeqNode->value.asSeq.pBody = pBody;
      pSeqNode->value.asSeq.szRecoverVar = szVar;

      hb_astPushBlock( HB_COMP_PARAM );
   }
}

void hb_astBeginAlways( HB_COMP_DECL )
{
   if( HB_COMP_ISAST( HB_COMP_PARAM ) )
   {
      PHB_AST_NODE pSeqBlock, pSeqNode;
      PHB_AST_NODE pBody = hb_astPopBlock( HB_COMP_PARAM );

      pSeqBlock = ( PHB_AST_NODE ) HB_COMP_PARAM->ast.pCurrBlock;
      pSeqNode = pSeqBlock->value.asBlock.pFirst;
      if( ! pSeqNode->value.asSeq.pBody )
         pSeqNode->value.asSeq.pBody = pBody;
      else
         pSeqNode->value.asSeq.pRecover = pBody;

      hb_astPushBlock( HB_COMP_PARAM );
   }
}

void hb_astEndSeq( HB_COMP_DECL, HB_BOOL fRecover, HB_BOOL fAlways )
{
   if( HB_COMP_ISAST( HB_COMP_PARAM ) )
   {
      PHB_AST_NODE pSeqBlock, pSeqNode;
      PHB_AST_NODE pBody = hb_astPopBlock( HB_COMP_PARAM );

      pSeqBlock = ( PHB_AST_NODE ) HB_COMP_PARAM->ast.pCurrBlock;
      pSeqNode = pSeqBlock->value.asBlock.pFirst;

      if( fAlways )
         pSeqNode->value.asSeq.pAlways = pBody;
      else if( fRecover )
         pSeqNode->value.asSeq.pRecover = pBody;
      else
         pSeqNode->value.asSeq.pBody = pBody;

      hb_astPopBlock( HB_COMP_PARAM );
      hb_astAppend( HB_COMP_PARAM, pSeqNode );
   }
}

/* === BREAK === */

void hb_astAddBreak( HB_COMP_DECL, PHB_EXPR pExpr, int iLine )
{
   if( HB_COMP_ISAST( HB_COMP_PARAM ) )
   {
      PHB_AST_NODE pNode = hb_astNew( HB_AST_BREAK, iLine );
      pNode->value.asBreak.pExpr = pExpr;
      hb_astAppend( HB_COMP_PARAM, pNode );
   }
}

/* === FOR EACH === */

void hb_astBeginForEach( HB_COMP_DECL, PHB_EXPR pVar, PHB_EXPR pEnum,
                         int iDir, int iLine )
{
   if( HB_COMP_ISAST( HB_COMP_PARAM ) )
   {
      PHB_AST_NODE pNode = hb_astNew( HB_AST_FOREACH, iLine );
      pNode->value.asForEach.pVar  = pVar;
      pNode->value.asForEach.pEnum = pEnum;
      pNode->value.asForEach.iDir  = iDir;
      pNode->value.asForEach.pBody = NULL;

      hb_astPushBlock( HB_COMP_PARAM );
      hb_astAppend( HB_COMP_PARAM, pNode );
      hb_astPushBlock( HB_COMP_PARAM );
   }
}

void hb_astEndForEach( HB_COMP_DECL )
{
   if( HB_COMP_ISAST( HB_COMP_PARAM ) )
   {
      PHB_AST_NODE pBlock, pNode;
      PHB_AST_NODE pBody = hb_astPopBlock( HB_COMP_PARAM );

      pBlock = ( PHB_AST_NODE ) HB_COMP_PARAM->ast.pCurrBlock;
      pNode = pBlock->value.asBlock.pFirst;
      pNode->value.asForEach.pBody = pBody;

      hb_astPopBlock( HB_COMP_PARAM );
      hb_astAppend( HB_COMP_PARAM, pNode );
   }
}
