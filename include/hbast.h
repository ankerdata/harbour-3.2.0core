/*
 * Harbour Transpiler - Abstract Syntax Tree definitions
 *
 * Copyright 2026 harbour.github.io
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation; either version 2, or (at your option)
 * any later version.
 */

#ifndef HB_AST_H_
#define HB_AST_H_

#include "hbcompdf.h"

HB_EXTERN_BEGIN

/* AST node types */
typedef enum
{
   HB_AST_FUNCTION = 0,   /* function/procedure definition */
   HB_AST_LOCAL,          /* LOCAL declaration */
   HB_AST_STATIC,         /* STATIC declaration */
   HB_AST_FIELD,          /* FIELD declaration */
   HB_AST_MEMVAR,         /* MEMVAR declaration */
   HB_AST_PRIVATE,        /* PRIVATE declaration */
   HB_AST_PUBLIC,         /* PUBLIC declaration */
   HB_AST_PARAMETERS,     /* PARAMETERS declaration */
   HB_AST_EXPRSTMT,       /* expression used as statement */
   HB_AST_RETURN,         /* RETURN [expr] */
   HB_AST_QOUT,           /* ? expr, expr, ... */
   HB_AST_QQOUT,          /* ?? expr, expr, ... */
   HB_AST_IF,             /* IF/ELSEIF/ELSE/ENDIF */
   HB_AST_ELSEIF,         /* ELSEIF clause (used in chain) */
   HB_AST_DOWHILE,        /* DO WHILE/ENDDO */
   HB_AST_FOR,            /* FOR/NEXT */
   HB_AST_FOREACH,        /* FOR EACH/NEXT */
   HB_AST_DOCASE,         /* DO CASE/ENDCASE */
   HB_AST_CASE,           /* CASE clause (used in chain) */
   HB_AST_SWITCH,         /* SWITCH/ENDSWITCH */
   HB_AST_BEGINSEQ,       /* BEGIN SEQUENCE/END */
   HB_AST_WITHOBJECT,     /* WITH OBJECT/END WITH */
   HB_AST_EXIT,           /* EXIT */
   HB_AST_LOOP,           /* LOOP */
   HB_AST_BREAK,          /* BREAK [expr] */
   HB_AST_BLOCK,          /* compound statement list */
   HB_AST_CLASS,          /* CLASS ... ENDCLASS definition */
   HB_AST_CLASSDATA,      /* DATA declaration inside class */
   HB_AST_CLASSMETHOD,    /* METHOD declaration inside class */
   HB_AST_INCLUDE,        /* #include passthrough */
   HB_AST_PPDEFINE,       /* #define passthrough */
   HB_AST_COMMENT         /* comment passthrough */
} HB_AST_TYPE;

/* Forward declaration */
typedef struct _HB_AST_NODE   HB_AST_NODE, * PHB_AST_NODE;

/* AST node structure */
struct _HB_AST_NODE
{
   HB_AST_TYPE    type;
   int            iLine;           /* source line number */
   PHB_AST_NODE   pNext;           /* next sibling statement */

   union
   {
      /* HB_AST_FUNCTION */
      struct
      {
         const char *   szName;
         HB_BOOL        fProcedure;    /* PROCEDURE vs FUNCTION */
         HB_SYMBOLSCOPE cScope;        /* HB_FS_PUBLIC, HB_FS_STATIC, etc. */
         PHB_HVAR       pParams;       /* parameter list (reuse compiler's var list) */
         PHB_AST_NODE   pBody;         /* statement list */
      } asFunc;

      /* HB_AST_LOCAL, HB_AST_STATIC, HB_AST_FIELD, HB_AST_MEMVAR,
         HB_AST_PRIVATE, HB_AST_PUBLIC */
      struct
      {
         const char *   szName;
         PHB_EXPR       pInit;         /* initializer expression (NULL if none) */
         const char *   szAlias;       /* FIELD alias or AS CLASS name */
         PHB_AST_NODE   pNext;         /* next variable in same declaration */
      } asVar;

      /* HB_AST_PARAMETERS */
      struct
      {
         PHB_AST_NODE   pVars;         /* list of var nodes */
      } asParams;

      /* HB_AST_EXPRSTMT */
      struct
      {
         PHB_EXPR       pExpr;
      } asExprStmt;

      /* HB_AST_RETURN */
      struct
      {
         PHB_EXPR       pExpr;         /* return value (NULL for void) */
      } asReturn;

      /* HB_AST_QOUT, HB_AST_QQOUT */
      struct
      {
         PHB_EXPR       pExprList;     /* expression list */
      } asQOut;

      /* HB_AST_IF */
      struct
      {
         PHB_EXPR       pCondition;
         PHB_AST_NODE   pThen;         /* then block */
         PHB_AST_NODE   pElseIfs;      /* chain of HB_AST_ELSEIF nodes */
         PHB_AST_NODE   pElse;         /* else block (NULL if no ELSE) */
      } asIf;

      /* HB_AST_ELSEIF */
      struct
      {
         PHB_EXPR       pCondition;
         PHB_AST_NODE   pBody;
      } asElseIf;

      /* HB_AST_DOWHILE */
      struct
      {
         PHB_EXPR       pCondition;
         PHB_AST_NODE   pBody;
      } asWhile;

      /* HB_AST_FOR */
      struct
      {
         const char *   szVar;         /* loop variable name */
         PHB_EXPR       pStart;        /* start expression */
         PHB_EXPR       pEnd;          /* TO expression */
         PHB_EXPR       pStep;         /* STEP expression (NULL if default) */
         PHB_AST_NODE   pBody;
      } asFor;

      /* HB_AST_FOREACH */
      struct
      {
         PHB_EXPR       pVar;          /* loop variable */
         PHB_EXPR       pEnum;         /* IN expression */
         int            iDir;          /* 0=ascending, 1=descending */
         PHB_AST_NODE   pBody;
      } asForEach;

      /* HB_AST_DOCASE */
      struct
      {
         PHB_AST_NODE   pCases;        /* chain of HB_AST_CASE nodes */
         PHB_AST_NODE   pOtherwise;    /* OTHERWISE block (NULL if none) */
      } asDoCase;

      /* HB_AST_CASE */
      struct
      {
         PHB_EXPR       pCondition;
         PHB_AST_NODE   pBody;
      } asCase;

      /* HB_AST_SWITCH */
      struct
      {
         PHB_EXPR       pSwitch;       /* SWITCH expression */
         PHB_AST_NODE   pCases;        /* case list */
         PHB_AST_NODE   pDefault;      /* DEFAULT block */
      } asSwitch;

      /* HB_AST_BEGINSEQ */
      struct
      {
         PHB_AST_NODE   pBody;
         PHB_AST_NODE   pRecover;      /* RECOVER block */
         const char *   szRecoverVar;  /* USING variable name */
         PHB_AST_NODE   pAlways;       /* ALWAYS block */
      } asSeq;

      /* HB_AST_WITHOBJECT */
      struct
      {
         PHB_EXPR       pObject;
         PHB_AST_NODE   pBody;
      } asWithObj;

      /* HB_AST_BREAK */
      struct
      {
         PHB_EXPR       pExpr;         /* break value (NULL if none) */
      } asBreak;

      /* HB_AST_BLOCK */
      struct
      {
         PHB_AST_NODE   pFirst;        /* first statement in block */
         PHB_AST_NODE   pLast;         /* last statement (for fast append) */
      } asBlock;

      /* HB_AST_CLASS */
      struct
      {
         const char *   szName;        /* class name */
         const char *   szParent;      /* parent class name (NULL if none) */
         PHB_AST_NODE   pMembers;      /* list of DATA/METHOD nodes */
         PHB_AST_NODE   pMembersLast;  /* last member (for fast append) */
      } asClass;

      /* HB_AST_CLASSDATA */
      struct
      {
         const char *   szName;        /* data member name */
         const char *   szType;        /* AS type (NULL if none) */
         const char *   szInit;        /* INIT value as string (NULL if none) */
         int            iScope;        /* HB_AST_SCOPE_* */
         int            iKind;         /* HB_AST_DATA_* */
         HB_BOOL        fReadOnly;     /* READONLY flag */
      } asClassData;

      /* HB_AST_CLASSMETHOD */
      struct
      {
         const char *   szName;        /* method name */
         const char *   szClass;       /* CLASS name (for implementations after ENDCLASS) */
         const char *   szParams;      /* parameter names as string (for declarations) */
         PHB_AST_NODE   pBody;         /* method body (NULL for declarations) */
         int            iScope;        /* HB_AST_SCOPE_* */
         HB_BOOL        fProcedure;    /* PROCEDURE (no return value) vs METHOD */
      } asClassMethod;

      /* HB_AST_INCLUDE */
      struct
      {
         const char *   szFile;        /* included filename */
      } asInclude;

      /* HB_AST_PPDEFINE */
      struct
      {
         const char *   szDefine;      /* full define text (name + value) */
      } asDefine;

      /* HB_AST_COMMENT */
      struct
      {
         const char *   szText;        /* comment text including delimiters */
      } asComment;
   } value;
};

/* Class member scope */
#define HB_AST_SCOPE_EXPORTED    0
#define HB_AST_SCOPE_PROTECTED   1
#define HB_AST_SCOPE_HIDDEN      2

/* Class data kind */
#define HB_AST_DATA_INSTANCE     0  /* DATA (instance variable) */
#define HB_AST_DATA_CLASS        1  /* CLASSDATA (shared class variable) */
#define HB_AST_DATA_ACCESS       2  /* ACCESS (read-only property) */
#define HB_AST_DATA_ASSIGN       3  /* ASSIGN (write property) */

/* === AST builder functions (hbast.c) === */

extern PHB_AST_NODE hb_astNew( HB_AST_TYPE type, int iLine );
extern void         hb_astFree( PHB_AST_NODE pNode );
extern void         hb_astFreeList( PHB_AST_NODE pNode );

extern void         hb_astInit( HB_COMP_DECL );
extern void         hb_astCleanup( HB_COMP_DECL );

extern void         hb_astPushBlock( HB_COMP_DECL );
extern PHB_AST_NODE hb_astPopBlock( HB_COMP_DECL );

extern void         hb_astAppend( HB_COMP_DECL, PHB_AST_NODE pNode );

extern PHB_AST_NODE hb_astBeginFunc( HB_COMP_DECL,
                                     const char * szName,
                                     HB_BOOL fProcedure,
                                     HB_SYMBOLSCOPE cScope,
                                     int iLine );
extern void         hb_astEndFunc( HB_COMP_DECL );

extern void         hb_astAddExprStmt( HB_COMP_DECL, PHB_EXPR pExpr, int iLine );
extern void         hb_astAddReturn( HB_COMP_DECL, PHB_EXPR pExpr, int iLine );
extern void         hb_astAddLocal( HB_COMP_DECL, const char * szName,
                                    PHB_EXPR pInit, int iLine );
extern void         hb_astAddStatic( HB_COMP_DECL, const char * szName,
                                     PHB_EXPR pInit, int iLine );

/* IF / ELSEIF / ELSE / ENDIF */
extern void         hb_astBeginIf( HB_COMP_DECL, PHB_EXPR pCondition, int iLine );
extern void         hb_astBeginElse( HB_COMP_DECL );
extern void         hb_astBeginElseIf( HB_COMP_DECL, PHB_EXPR pCondition, int iLine );
extern void         hb_astEndIf( HB_COMP_DECL, HB_BOOL fElse, HB_BOOL fElseIf );

/* DO WHILE / ENDDO */
extern void         hb_astBeginWhile( HB_COMP_DECL, PHB_EXPR pCondition, int iLine );
extern void         hb_astEndWhile( HB_COMP_DECL );

/* FOR / NEXT */
extern void         hb_astBeginFor( HB_COMP_DECL, const char * szVar,
                                    PHB_EXPR pStart, PHB_EXPR pEnd,
                                    PHB_EXPR pStep, int iLine );
extern void         hb_astEndFor( HB_COMP_DECL );

/* EXIT / LOOP / BREAK */
extern void         hb_astAddExit( HB_COMP_DECL, int iLine );
extern void         hb_astAddLoop( HB_COMP_DECL, int iLine );
extern void         hb_astAddBreak( HB_COMP_DECL, PHB_EXPR pExpr, int iLine );

/* DO CASE / CASE / OTHERWISE / ENDCASE */
extern void         hb_astBeginDoCase( HB_COMP_DECL, int iLine );
extern void         hb_astAddCase( HB_COMP_DECL, PHB_EXPR pCondition, int iLine );
extern void         hb_astBeginOtherwise( HB_COMP_DECL );
extern void         hb_astEndDoCase( HB_COMP_DECL, HB_BOOL fOtherwise );

/* SWITCH / CASE / DEFAULT / ENDSWITCH */
extern void         hb_astBeginSwitch( HB_COMP_DECL, PHB_EXPR pSwitch, int iLine );
extern void         hb_astAddSwitchCase( HB_COMP_DECL, PHB_EXPR pValue, int iLine );
extern void         hb_astAddSwitchDefault( HB_COMP_DECL, int iLine );
extern void         hb_astEndSwitch( HB_COMP_DECL );

/* BEGIN SEQUENCE / RECOVER / ALWAYS / END */
extern void         hb_astBeginSeq( HB_COMP_DECL, int iLine );
extern void         hb_astBeginRecover( HB_COMP_DECL, const char * szVar );
extern void         hb_astBeginAlways( HB_COMP_DECL );
extern void         hb_astEndSeq( HB_COMP_DECL, HB_BOOL fRecover, HB_BOOL fAlways );

/* FOR EACH */
extern void         hb_astBeginForEach( HB_COMP_DECL, PHB_EXPR pVar,
                                        PHB_EXPR pEnum, int iDir, int iLine );
extern void         hb_astEndForEach( HB_COMP_DECL );

/* WITH OBJECT / END WITH */
extern void         hb_astBeginWithObject( HB_COMP_DECL, PHB_EXPR pObject, int iLine );
extern void         hb_astEndWithObject( HB_COMP_DECL );

/* Type inference (hbtypes.c) */
extern const char * hb_astInferType( const char * szName, PHB_EXPR pInit );
extern const char * hb_astInferTypeFromInit( const char * szName, const char * szInit );
extern const char * hb_astPropagate( PHB_AST_NODE pBody, PHB_AST_NODE pClassList );

/* Class pre-parser (hbclsparse.c) */
extern HB_BOOL      hb_compClassParse( HB_COMP_DECL );
extern HB_BOOL      hb_compMethodParse( HB_COMP_DECL, HB_BOOL fProcedure );

/* === Transpiler output (gentr.c) === */

extern void         hb_compGenTranspile( HB_COMP_DECL, PHB_FNAME pFileName );

HB_EXTERN_END

#endif /* HB_AST_H_ */
