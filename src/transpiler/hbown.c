/*
 * Harbour Transpiler - ownership: the variables that hold the only
 * reference to a new object (see include/hbown.h)
 *
 * The walk gives every use of a value a context: used and let go (0),
 * kept beyond the routine (HB_OWN_KEEP: stored in a member, an element,
 * a STATIC, another variable, captured by a codeblock, passed by
 * reference, or passed on to a parameter that keeps it), or returned
 * (HB_OWN_RET). A call passes each argument on in the context its
 * parameter's facts give it, and a parameter that is returned takes the
 * call's own context: `x := Echo( o )` keeps o, `Echo( o )` alone does
 * not. A send reads the facts of every method of its name, in any class
 * (for Self, those of the class's family).
 *
 * The facts this records on a routine's row (hb_ownRecordFacts) are read
 * by the next routine's walk. The scan recomputes them on every pass from
 * the body and stops only when nothing moved. A routine of the program
 * not analysed yet counts as keeping nothing, so the facts only grow, pass
 * by pass, until they hold everything these rules derive and nothing
 * more: methods that call one another, as a class's do, would otherwise
 * hold each other at "kept". On a cold scan's first pass the reftab does
 * not yet know the routines of the files after the one being read, so
 * there (--own-first-pass) a name it does not know keeps nothing too; were
 * it taken to keep anything, a cycle through one of them would never let
 * go of it. From the second pass on, a name the reftab does not know is
 * really unknown (a core function outside the list below, a C function
 * --own-facts does not list, the arguments of a message no class
 * answers) and keeps anything, to the end. The receiver of such a
 * message keeps nothing: no routine of the program runs.
 *
 * Copyright 2026 harbour.github.io
 */

#include "hbcomp.h"
#include "hbast.h"
#include "hbreftab.h"
#include "hbfieldtypes.h"
#include "hbown.h"

#define HB_OWN_KEEP        1
#define HB_OWN_RET         2

#define HB_OWN_MAXSHADOW   64
#define HB_OWN_MAXRETVARS  64
#define HB_OWN_MAXARGS     64    /* the reftab's parameter cap */

typedef struct
{
   PHB_REFTAB    pTab;
   const char *  szClass;        /* the method's class, NULL in a function */
   PHB_AST_NODE  pFileFuncs;     /* the file's functions, for its STATIC ones */
   const char *  szFileBase;
   HB_OWNINFO *  pInfo;
   PHB_AST_NODE  pTopStmt;       /* the top-level statement being walked */
   PHB_EXPR      pTopExpr;       /* that statement's expression, if it is one */
   int           iBlockDepth;    /* codeblocks around the expression */
   int           iLoopDepth;     /* loops and sequences around it, where an
                                    assignment may find a value an earlier
                                    iteration, or a BREAK, left */
   int           iCondDepth;     /* IIF() branches and .AND. / .OR. right
                                    sides around it, which may not run */
   const char *  aszShadow[ HB_OWN_MAXSHADOW ];  /* their parameters */
   int           nShadow;
   HB_BOOL       fReturnNew;     /* a RETURN gives a new object */
   HB_BOOL       fReturnOther;   /* a RETURN gives something else */
   HB_OWNVAR *   apReturned[ HB_OWN_MAXRETVARS ];  /* LOCALs returned */
   int           nReturned;
   /* HB_OWN_TRACE=<routine> (or *) prints why a value counts as kept or
      returned: the statement's line and the call it is passed to */
   const char *  szTrace;        /* the routine traced, or NULL */
   int           iLine;          /* the statement being walked */
   const char *  szWhy;          /* the call or send whose argument it is */
} HB_OWNCTX;

static void hb_ownExpr( HB_OWNCTX * c, PHB_EXPR e, int ctx );
static void hb_ownStmts( HB_OWNCTX * c, PHB_AST_NODE pBlock, HB_BOOL fTop );

/* ---- switches ---- */

static HB_BOOL s_fFirstPass = HB_FALSE;

void hb_ownSetFirstPass( HB_BOOL fFirstPass )
{
   s_fFirstPass = fFirstPass;
}

/* --own-facts: the program's C functions */

typedef struct
{
   char * szName;
   HB_U64 kept;      /* bit n: argument n may be kept */
} HB_OWNCFUNC;

static char          s_szFactsPath[ HB_PATH_MAX ] = { 0 };
static HB_OWNCFUNC * s_pCFuncs = NULL;
static int           s_nCFuncs = -1;   /* -1: not read yet */

void hb_ownSetFactsPath( const char * szPath )
{
   hb_strncpy( s_szFactsPath, szPath ? szPath : "", sizeof( s_szFactsPath ) - 1 );
   s_nCFuncs = -1;
}

static void hb_ownLoadCFuncs( void )
{
   FILE * fp;
   char   line[ 512 ];
   int    nCap = 0;

   s_nCFuncs = 0;
   if( ! s_szFactsPath[ 0 ] || ( fp = hb_fopen( s_szFactsPath, "r" ) ) == NULL )
      return;
   while( fgets( line, sizeof( line ), fp ) )
   {
      char * pTab = strchr( line, '\t' );
      char * q;
      HB_U64 kept = 0;
      if( line[ 0 ] == '#' || ! pTab )
         continue;
      *pTab = '\0';
      for( q = pTab + 1; *q; )
      {
         if( HB_ISDIGIT( ( HB_UCHAR ) *q ) )
         {
            int n = atoi( q );
            if( n >= 1 && n <= 64 )
               kept |= ( ( HB_U64 ) 1 ) << ( n - 1 );
            while( HB_ISDIGIT( ( HB_UCHAR ) *q ) )
               q++;
         }
         else
            q++;
      }
      if( s_nCFuncs == nCap )
      {
         nCap = nCap ? nCap * 2 : 16;
         s_pCFuncs = ( HB_OWNCFUNC * ) hb_xrealloc( s_pCFuncs,
                                                    sizeof( HB_OWNCFUNC ) * nCap );
      }
      s_pCFuncs[ s_nCFuncs ].szName = hb_strdup( line );
      s_pCFuncs[ s_nCFuncs ].kept = kept;
      s_nCFuncs++;
   }
   fclose( fp );
}

/* What a listed C function keeps of its arguments; FALSE when it is not
   listed */
static HB_BOOL hb_ownCFuncFacts( const char * szName, HB_U64 * pKept )
{
   int i;
   if( s_nCFuncs < 0 )
      hb_ownLoadCFuncs();
   for( i = 0; i < s_nCFuncs; i++ )
      if( hb_stricmp( s_pCFuncs[ i ].szName, szName ) == 0 )
      {
         *pKept = s_pCFuncs[ i ].kept;
         return HB_TRUE;
      }
   return HB_FALSE;
}

/* ---- variables ---- */

HB_OWNVAR * hb_ownFindVar( HB_OWNINFO * pInfo, const char * szName )
{
   int i;
   if( ! pInfo || ! szName )
      return NULL;
   for( i = 0; i < pInfo->nVars; i++ )
      if( hb_stricmp( pInfo->pVars[ i ].szName, szName ) == 0 )
         return &pInfo->pVars[ i ];
   return NULL;
}

/* The routine's variable a name means here: none inside a codeblock
   whose own parameter has the name */
static HB_OWNVAR * hb_ownVar( HB_OWNCTX * c, const char * szName )
{
   int i;
   if( ! szName )
      return NULL;
   for( i = 0; i < c->nShadow; i++ )
      if( hb_stricmp( c->aszShadow[ i ], szName ) == 0 )
         return NULL;
   return hb_ownFindVar( c->pInfo, szName );
}

static void hb_ownUse( HB_OWNCTX * c, HB_OWNVAR * v )
{
   if( ! v->pFirstUse )
      v->pFirstUse = c->pTopStmt;
}

static void hb_ownTrace( HB_OWNCTX * c, const char * szName, int ctx )
{
   if( ! c->szTrace || ! ctx )
      return;
   fprintf( stderr, "hbown %s(%d): %s %s%s%s%s\n", c->szTrace, c->iLine,
            szName,
            ( ctx & HB_OWN_KEEP ) ? "kept" : "",
            ( ctx & HB_OWN_KEEP ) && ( ctx & HB_OWN_RET ) ? ", " : "",
            ( ctx & HB_OWN_RET ) ? "returned" : "",
            c->iBlockDepth > 0 ? " (in a codeblock)" : "" );
   if( c->szWhy )
      fprintf( stderr, "hbown %s(%d):    by %s\n", c->szTrace, c->iLine,
               c->szWhy );
}

static void hb_ownNoteVar( HB_OWNCTX * c, const char * szName, int ctx )
{
   HB_OWNVAR * v = hb_ownVar( c, szName );
   if( ! v )
      return;
   hb_ownUse( c, v );
   /* a codeblock may run, and hold what it reads, any time */
   if( c->iBlockDepth > 0 )
      ctx |= HB_OWN_KEEP;
   if( ctx & HB_OWN_KEEP )
      v->fKept = HB_TRUE;
   if( ctx & HB_OWN_RET )
      v->fReturned = HB_TRUE;
   hb_ownTrace( c, v->szName, ctx );
}

static void hb_ownNoteSelf( HB_OWNCTX * c, int ctx )
{
   if( c->iBlockDepth > 0 )
      ctx |= HB_OWN_KEEP;
   if( ctx & HB_OWN_KEEP )
      c->pInfo->fSelfKept = HB_TRUE;
   if( ctx & HB_OWN_RET )
      c->pInfo->fSelfReturned = HB_TRUE;
   hb_ownTrace( c, "Self", ctx );
}

/* A variable given a value that is not a new object */
static void hb_ownWritten( HB_OWNCTX * c, PHB_EXPR pLeft )
{
   if( pLeft && pLeft->ExprType == HB_ET_VARIABLE )
   {
      HB_OWNVAR * v = hb_ownVar( c, pLeft->value.asSymbol.name );
      if( v )
      {
         hb_ownUse( c, v );
         v->fOther = HB_TRUE;
         v->fHolds = HB_TRUE;
         v->nHeld++;
      }
   }
}

/* ---- what a local may hold, path by path ----
   An assignment that finds the local holding nothing on every path has no
   old value to dispose (hb_ownFindsEmpty). The walk carries fHolds per
   local: an IF or a DO CASE forks it per branch and joins the branches;
   in a loop or a sequence, where an earlier iteration or a BREAK may have
   left a value, no assignment finds it empty, and after one a local given
   a value inside may hold it. */

HB_BOOL hb_ownFindsEmpty( HB_OWNVAR * v, PHB_EXPR pAssign )
{
   int i;
   if( ! v || ! pAssign )
      return HB_FALSE;
   for( i = 0; i < v->nEmpty; i++ )
      if( v->apEmpty[ i ] == pAssign )
         return HB_TRUE;
   return HB_FALSE;
}

static HB_BOOL * hb_ownHoldsSave( HB_OWNCTX * c )
{
   HB_BOOL * p = ( HB_BOOL * ) hb_xgrabz( sizeof( HB_BOOL ) * ( c->pInfo->nVars + 1 ) );
   int i;
   for( i = 0; i < c->pInfo->nVars; i++ )
      p[ i ] = c->pInfo->pVars[ i ].fHolds;
   return p;
}

static void hb_ownHoldsLoad( HB_OWNCTX * c, const HB_BOOL * p )
{
   int i;
   for( i = 0; i < c->pInfo->nVars; i++ )
      c->pInfo->pVars[ i ].fHolds = p[ i ];
}

static void hb_ownHoldsJoin( HB_OWNCTX * c, HB_BOOL * pJoin )
{
   int i;
   for( i = 0; i < c->pInfo->nVars; i++ )
      if( c->pInfo->pVars[ i ].fHolds )
         pJoin[ i ] = HB_TRUE;
}

static int * hb_ownHeldSave( HB_OWNCTX * c )
{
   int * p = ( int * ) hb_xgrabz( sizeof( int ) * ( c->pInfo->nVars + 1 ) );
   int i;
   for( i = 0; i < c->pInfo->nVars; i++ )
      p[ i ] = c->pInfo->pVars[ i ].nHeld;
   return p;
}

/* After a loop or a sequence: a local given a value inside may hold it */
static void hb_ownHeldJoin( HB_OWNCTX * c, const HB_BOOL * pStart,
                            const int * pHeld )
{
   int i;
   for( i = 0; i < c->pInfo->nVars; i++ )
   {
      HB_OWNVAR * v = &c->pInfo->pVars[ i ];
      if( pStart[ i ] || v->nHeld != pHeld[ i ] )
         v->fHolds = HB_TRUE;
   }
}

static HB_BOOL hb_ownIsSelf( PHB_EXPR e )
{
   return e && ( e->ExprType == HB_ET_SELF ||
                 ( e->ExprType == HB_ET_VARIABLE && e->value.asSymbol.name &&
                   hb_stricmp( e->value.asSymbol.name, "Self" ) == 0 ) );
}

/* Self, or Self seen as its parent: `::Super` and `::<Parent>` */
static HB_BOOL hb_ownIsSelfRecv( HB_OWNCTX * c, PHB_EXPR e )
{
   if( hb_ownIsSelf( e ) )
      return HB_TRUE;
   return e && e->ExprType == HB_ET_SEND && ! e->value.asMessage.pParms &&
          ! e->value.asMessage.pMessage && e->value.asMessage.szMessage &&
          hb_ownIsSelf( e->value.asMessage.pObject ) &&
          ( hb_stricmp( e->value.asMessage.szMessage, "Super" ) == 0 ||
            hb_refTabIsClass( c->pTab, e->value.asMessage.szMessage ) );
}

/* (x) is x */
static PHB_EXPR hb_ownPeel( PHB_EXPR e )
{
   while( e && e->ExprType == HB_ET_LIST && e->value.asList.pExprList &&
          ! e->value.asList.pExprList->pNext )
      e = e->value.asList.pExprList;
   return e;
}

static PHB_EXPR hb_ownFirstArg( PHB_EXPR pParms )
{
   if( ! pParms )
      return NULL;
   if( pParms->ExprType == HB_ET_LIST || pParms->ExprType == HB_ET_ARGLIST )
      return pParms->value.asList.pExprList;
   return pParms;
}

/* `X()`: no arguments, or the empty slot an empty list holds */
static HB_BOOL hb_ownNoArgs( PHB_EXPR pParms )
{
   PHB_EXPR p = hb_ownFirstArg( pParms );
   return ! p || ( p->ExprType == HB_ET_NONE && ! p->pNext );
}

/* ---- routines and methods ---- */

/* The reftab key of a called function: a STATIC function of this file is
   `<file>::<name>`, as the scan registers it */
static const char * hb_ownFuncKey( HB_OWNCTX * c, const char * szName,
                                   char * szBuf, HB_SIZE nSize )
{
   PHB_AST_NODE pF;
   if( ! c->szFileBase )
      return szName;
   for( pF = c->pFileFuncs; pF; pF = pF->pNext )
      if( pF->type == HB_AST_FUNCTION && pF->value.asFunc.szName &&
          ( pF->value.asFunc.cScope & HB_FS_STATIC ) != 0 &&
          hb_stricmp( pF->value.asFunc.szName, szName ) == 0 )
      {
         hb_snprintf( szBuf, nSize, "%s::%s", c->szFileBase, szName );
         return szBuf;
      }
   return szName;
}

/* The method a message reaches on an object of exactly szClass: its own,
   or the nearest ancestor's */
static const char * hb_ownResolveMethod( PHB_REFTAB pTab, const char * szClass,
                                         const char * szMsg,
                                         char * szBuf, HB_SIZE nSize )
{
   int i;
   for( i = 0; szClass && i < 16; i++ )
   {
      hb_snprintf( szBuf, nSize, "%s::%s__%s", szClass, szClass, szMsg );
      if( hb_refTabIsDefinedFunc( pTab, szBuf ) )
         return szBuf;
      szClass = hb_refTabClassParent( pTab, szClass );
   }
   return NULL;
}

HB_BOOL hb_ownClassDisposable( PHB_REFTAB pTab, const char * szClass )
{
   int i;
   if( szClass && hb_stricmp( szClass, "Sqlite3Db" ) == 0 )
      return HB_TRUE;
   for( i = 0; szClass && i < 16; i++ )
   {
      if( hb_refTabClassHasDestructor( pTab, szClass ) )
         return HB_TRUE;
      szClass = hb_refTabClassParent( pTab, szClass );
   }
   return HB_FALSE;
}

/* Does a method that runs on a new object let it go again? It must keep
   nothing of Self; when its result is what the expression gives, that
   must be Self (or NIL). */
static HB_BOOL hb_ownMethodLetsGo( PHB_REFTAB pTab, const char * szKey,
                                   HB_BOOL fResult )
{
   HB_BOOL fSelfKept = HB_TRUE, fReturnsSelf = HB_FALSE;
   if( ! hb_refTabOwnFacts( pTab, szKey, NULL, NULL, &fSelfKept, NULL,
                            NULL, &fReturnsSelf ) )
      return HB_FALSE;
   return ! fSelfKept && ( ! fResult || fReturnsSelf );
}

/* The class of the new object an expression gives, nothing else holding
   it, or NULL when it gives anything else:
     X():New( … )       HBObject's New() runs Init() and answers the
                        object; a New() of X's own must answer Self
     X():Init( … )      sent to the new object, which it must answer
     ConstructORMTable( <Def>() )  the model, a new table (the emitter
                        writes `new <Model>(…)`, whose constructor runs
                        SQLtTable's OpenDefinition())
     sqlite3_open( … )  a connection, "Sqlite3Db"
     F( … )             a function that returns new objects */
static const char * hb_ownNewClass( HB_OWNCTX * c, PHB_EXPR e )
{
   char szKey[ 256 ];

   e = hb_ownPeel( e );
   if( ! e )
      return NULL;

   if( e->ExprType == HB_ET_SEND && ! e->value.asMessage.pMessage &&
       e->value.asMessage.szMessage && e->value.asMessage.pObject &&
       e->value.asMessage.pObject->ExprType == HB_ET_FUNCALL &&
       hb_ownNoArgs( e->value.asMessage.pObject->value.asFunCall.pParms ) )
   {
      PHB_EXPR pFn = e->value.asMessage.pObject->value.asFunCall.pFunName;
      const char * szClass;
      const char * szMsg = e->value.asMessage.szMessage;
      if( ! pFn || pFn->ExprType != HB_ET_FUNNAME ||
          ! hb_refTabIsClass( c->pTab, pFn->value.asSymbol.name ) )
         return NULL;
      szClass = hb_refTabClassCanonName( c->pTab, pFn->value.asSymbol.name );
      if( ! szClass )
         szClass = pFn->value.asSymbol.name;
      if( hb_stricmp( szMsg, "New" ) == 0 )
      {
         const char * szNew = hb_ownResolveMethod( c->pTab, szClass, "New",
                                                    szKey, sizeof( szKey ) );
         if( szNew )
            return hb_ownMethodLetsGo( c->pTab, szNew, HB_TRUE ) ? szClass : NULL;
         szNew = hb_ownResolveMethod( c->pTab, szClass, "Init",
                                      szKey, sizeof( szKey ) );
         return ! szNew || hb_ownMethodLetsGo( c->pTab, szNew, HB_FALSE )
                ? szClass : NULL;
      }
      {
         const char * szM = hb_ownResolveMethod( c->pTab, szClass, szMsg,
                                                 szKey, sizeof( szKey ) );
         return szM && hb_ownMethodLetsGo( c->pTab, szM, HB_TRUE )
                ? szClass : NULL;
      }
   }

   if( e->ExprType == HB_ET_FUNCALL && e->value.asFunCall.pFunName &&
       e->value.asFunCall.pFunName->ExprType == HB_ET_FUNNAME )
   {
      const char * szName = e->value.asFunCall.pFunName->value.asSymbol.name;
      const char * szFKey;
      HB_BOOL fNew = HB_FALSE;

      if( hb_stricmp( szName, "ConstructORMTable" ) == 0 )
      {
         PHB_EXPR pDef = hb_ownPeel( hb_ownFirstArg( e->value.asFunCall.pParms ) );
         const char * szModel = NULL;
         if( ! hb_refTabIsDefinedFunc( c->pTab, "SQLtTable::SQLtTable__OpenDefinition" ) ||
             ! hb_ownMethodLetsGo( c->pTab, "SQLtTable::SQLtTable__OpenDefinition",
                                   HB_FALSE ) )
            return NULL;
         if( pDef && pDef->ExprType == HB_ET_FUNCALL &&
             pDef->value.asFunCall.pFunName &&
             pDef->value.asFunCall.pFunName->ExprType == HB_ET_FUNNAME )
            szModel = hb_fieldTypesModelOf(
               pDef->value.asFunCall.pFunName->value.asSymbol.name );
         return szModel ? szModel : "SQLtTable";
      }
      if( hb_stricmp( szName, "sqlite3_open" ) == 0 &&
          ! hb_refTabIsDefinedFunc( c->pTab, szName ) )
         return "Sqlite3Db";
      szFKey = hb_ownFuncKey( c, szName, szKey, sizeof( szKey ) );
      if( hb_refTabOwnFacts( c->pTab, szFKey, NULL, NULL, NULL, NULL,
                             &fNew, NULL ) && fNew )
      {
         const char * szRet = hb_refTabReturnType( c->pTab, szFKey );
         /* a class the reftab or the models know, else "" (new, of a
            class nothing disposes) */
         if( szRet && ( hb_refTabIsClass( c->pTab, szRet ) ||
                        hb_fieldTypesModelCanon( szRet ) ||
                        hb_stricmp( szRet, "SQLtTable" ) == 0 ) )
            return szRet;
         return "";
      }
   }
   return NULL;
}

/* Core functions that keep none of what they are given. Most only look at
   it; returned is the arguments one may answer (bit n for argument n),
   which go where the call's own value goes: hb_HGetDef( h, k, xDefault ). */
typedef struct
{
   const char * szName;
   HB_U64       returned;
} HB_OWNCORE;

static const HB_OWNCORE s_aCore[] = {
   { "EMPTY", 0 }, { "VALTYPE", 0 }, { "LEN", 0 }, { "TYPE", 0 },
   { "PCOUNT", 0 },
   { "HB_ISOBJECT", 0 }, { "HB_ISNIL", 0 }, { "HB_ISPOINTER", 0 },
   { "HB_ISARRAY", 0 }, { "HB_ISHASH", 0 }, { "HB_ISSTRING", 0 },
   { "HB_ISCHAR", 0 }, { "HB_ISMEMO", 0 }, { "HB_ISNUMERIC", 0 },
   { "HB_ISLOGICAL", 0 }, { "HB_ISDATE", 0 }, { "HB_ISTIMESTAMP", 0 },
   { "HB_ISDATETIME", 0 }, { "HB_ISBLOCK", 0 }, { "HB_ISEVALITEM", 0 },
   { "HB_ISNULL", 0 }, { "HB_ISSYMBOL", 0 },
   { "QOUT", 0 }, { "QQOUT", 0 }, { "OUTSTD", 0 }, { "OUTERR", 0 },
   { "HB_GCALL", 0 }, { "HB_SYMBOL_UNUSED", 0 },
   { "__OBJHASMSG", 0 }, { "__OBJGETCLSNAME", 0 },
   { "HB_HHASKEY", 0 }, { "HB_HPOS", 0 }, { "HB_HGET", 0 },
   { "HB_HKEYS", 0 }, { "HB_HVALUES", 0 },
   { "ASCAN", 0 }, { "HB_ASCAN", 0 },
   { "HB_HGETDEF", 4 }, { "HB_DEFAULTVALUE", 3 },
   { NULL, 0 } };

/* What a core function keeps and returns of its arguments; FALSE for one
   not listed, which keeps anything */
static HB_BOOL hb_ownCoreFacts( const char * szName, HB_U64 * pReturned )
{
   int i;
   *pReturned = 0;
   if( ! szName )
      return HB_FALSE;
   for( i = 0; s_aCore[ i ].szName; i++ )
      if( hb_stricmp( s_aCore[ i ].szName, szName ) == 0 )
      {
         *pReturned = s_aCore[ i ].returned;
         return HB_TRUE;
      }
   /* hbsqlit3's functions read a connection; preparing a statement, a
      backup or a blob ties one to it */
   return hb_strnicmp( szName, "sqlite3_", 8 ) == 0 &&
          hb_strnicmp( szName, "sqlite3_prepare", 15 ) != 0 &&
          hb_stricmp( szName, "sqlite3_backup_init" ) != 0 &&
          hb_stricmp( szName, "sqlite3_blob_open" ) != 0;
}

/* ---- assignments ---- */

/* szName := pRight. pWhole is the assignment (the hb_default() call for
   one that gives the value only to a NIL variable, fIfNil); ctx is what
   becomes of the assignment's own value. */
static void hb_ownAssignVar( HB_OWNCTX * c, HB_OWNVAR * v, PHB_EXPR pRight,
                             PHB_EXPR pWhole, HB_BOOL fIfNil, int ctx )
{
   PHB_EXPR pVal = hb_ownPeel( pRight );
   const char * szNew;

   hb_ownUse( c, v );
   if( c->iBlockDepth > 0 )
      v->fKept = v->fOther = HB_TRUE;
   if( pVal && pVal->ExprType == HB_ET_NIL )
   {
      v->fNil = HB_TRUE;
      if( c->iCondDepth == 0 )
         v->fHolds = HB_FALSE;
   }
   else if( ( szNew = hb_ownNewClass( c, pVal ) ) != NULL )
   {
      if( c->szTrace )
         fprintf( stderr, "hbown %s(%d): %s given a new %s\n", c->szTrace,
                  c->iLine, v->szName, *szNew ? szNew : "object" );
      if( ! hb_ownClassDisposable( c->pTab, szNew ) )
         v->fUndisposable = HB_TRUE;
      if( ++v->nNew == 1 )
      {
         v->pOnlyNew = pWhole;
         v->pOnlyNewStmt = c->pTopStmt;
         v->fOnlyNewTop = ! fIfNil && c->iBlockDepth == 0 &&
                          c->pTopExpr == pWhole;
      }
      if( ! v->fHolds && ! fIfNil && pWhole && c->iLoopDepth == 0 &&
          v->nEmpty < ( int ) HB_SIZEOFARRAY( v->apEmpty ) )
         v->apEmpty[ v->nEmpty++ ] = pWhole;
      v->fHolds = HB_TRUE;
      v->nHeld++;
      /* its arguments, as the constructor takes them */
      hb_ownExpr( c, pVal, 0 );
   }
   else
   {
      if( c->szTrace )
         fprintf( stderr, "hbown %s(%d): %s given a value that is not new\n",
                  c->szTrace, c->iLine, v->szName );
      v->fOther = HB_TRUE;
      v->fHolds = HB_TRUE;
      v->nHeld++;
      hb_ownExpr( c, pRight, HB_OWN_KEEP );
   }
   if( ctx )
      hb_ownNoteVar( c, v->szName, ctx );
}

static void hb_ownAssign( HB_OWNCTX * c, PHB_EXPR e, int ctx )
{
   PHB_EXPR pL = e->value.asOperator.pLeft;
   PHB_EXPR pR = e->value.asOperator.pRight;

   if( pL && pL->ExprType == HB_ET_VARIABLE )
   {
      HB_OWNVAR * v = hb_ownVar( c, pL->value.asSymbol.name );
      if( v )
      {
         hb_ownAssignVar( c, v, pR, e, HB_FALSE, ctx );
         return;
      }
   }
   /* a member, an element, a STATIC, a MEMVAR: the value is stored there */
   if( pL )
   {
      if( pL->ExprType == HB_ET_SEND )
      {
         if( hb_ownIsSelfRecv( c, pL->value.asMessage.pObject ) )
            hb_ownNoteSelf( c, 0 );
         else
            hb_ownExpr( c, pL->value.asMessage.pObject, 0 );
         hb_ownExpr( c, pL->value.asMessage.pMessage, 0 );
      }
      else if( pL->ExprType == HB_ET_ARRAYAT )
      {
         hb_ownExpr( c, pL->value.asList.pExprList, 0 );
         hb_ownExpr( c, pL->value.asList.pIndex, 0 );
      }
      else if( pL->ExprType != HB_ET_VARIABLE )
         hb_ownExpr( c, pL, HB_OWN_KEEP );
   }
   hb_ownExpr( c, pR, HB_OWN_KEEP | ctx );
}

/* ---- calls and sends ---- */

static void hb_ownArgs( HB_OWNCTX * c, PHB_EXPR pParms, int ctx )
{
   PHB_EXPR p;
   for( p = hb_ownFirstArg( pParms ); p; p = p->pNext )
      hb_ownExpr( c, p, ctx );
}

static void hb_ownCall( HB_OWNCTX * c, PHB_EXPR e, int ctx )
{
   PHB_EXPR pName  = e->value.asFunCall.pFunName;
   PHB_EXPR pParms = e->value.asFunCall.pParms;
   PHB_EXPR pArg   = hb_ownFirstArg( pParms );
   const char * szName = pName && pName->ExprType == HB_ET_FUNNAME ?
                         pName->value.asSymbol.name : NULL;
   char szKey[ 256 ];
   const char * szFKey;
   HB_U64 kept = 0, ret = 0;
   HB_BOOL fProgram, fKnown = HB_FALSE, fVariadic = HB_FALSE, fCFunc = HB_FALSE;
   HB_BOOL fCore = HB_FALSE;
   int nParams = 0, i;

   if( ! szName )
   {
      /* &cFunc( … ): anything */
      hb_ownExpr( c, pName, HB_OWN_KEEP );
      hb_ownArgs( c, pParms, HB_OWN_KEEP | ctx );
      return;
   }

   /* hb_default( @x, v ) gives x the value v when x is NIL */
   if( hb_stricmp( szName, "HB_DEFAULT" ) == 0 && pArg &&
       pArg->ExprType == HB_ET_VARREF && pArg->pNext && ! pArg->pNext->pNext )
   {
      HB_OWNVAR * v = hb_ownVar( c, pArg->value.asSymbol.name );
      if( v )
      {
         hb_ownAssignVar( c, v, pArg->pNext, e, HB_TRUE, 0 );
         return;
      }
   }

   szFKey = hb_ownFuncKey( c, szName, szKey, sizeof( szKey ) );
   fProgram = hb_refTabIsDefinedFunc( c->pTab, szFKey );
   if( fProgram )
   {
      fKnown = hb_refTabOwnFacts( c->pTab, szFKey, &kept, &ret,
                                  NULL, NULL, NULL, NULL );
      nParams = hb_refTabParamCount( c->pTab, szFKey );
      fVariadic = hb_refTabIsVariadic( c->pTab, szFKey );
   }
   else if( ! ( fCFunc = hb_ownCFuncFacts( szName, &kept ) ) )
      fCore = hb_ownCoreFacts( szName, &ret );
   c->szWhy = szFKey;
   for( i = 0; pArg; pArg = pArg->pNext, i++ )
   {
      int a;
      if( fCFunc )
         a = i < 64 && ! ( ( kept >> i ) & 1 ) ? 0 : HB_OWN_KEEP | ctx;
      else if( ! fProgram && ! fCore )
         a = s_fFirstPass ? 0 : HB_OWN_KEEP | ctx;
      else if( ! fProgram )
         a = i < 64 && ( ( ret >> i ) & 1 ) ? ctx : 0;
      else if( i >= 64 )
         a = HB_OWN_KEEP | ctx;
      else if( ! fKnown )
         /* not analysed yet: this pass's facts come next pass */
         a = 0;
      else if( i >= nParams )
         /* an argument no parameter takes: dropped, unless the routine
            reads its arguments as a list */
         a = fVariadic ? HB_OWN_KEEP | ctx : 0;
      else
         a = ( ( ( kept >> i ) & 1 ) ? HB_OWN_KEEP : 0 ) |
             ( ( ( ret  >> i ) & 1 ) ? ctx : 0 );
      c->szWhy = szFKey;
      hb_ownExpr( c, pArg, a );
   }
   c->szWhy = NULL;
}

typedef struct
{
   HB_OWNCTX *  c;
   int          ctx;
   const char * szRelated;   /* Self's class: only methods it can reach */
   int          nArgs;
   int          aArgCtx[ HB_OWN_MAXARGS ];
   int          iSelfCtx;
   HB_BOOL      fAny;
} HB_OWNSEND;

/* What one method a send may run does with the receiver and the
   arguments */
static void hb_ownSendRow( const char * szKey, const char * szClass,
                           void * cargo )
{
   HB_OWNSEND * s = ( HB_OWNSEND * ) cargo;
   PHB_REFTAB pTab = s->c->pTab;
   HB_U64 kept = 0, ret = 0;
   HB_BOOL fSelfKept = HB_FALSE, fSelfRet = HB_FALSE;
   int i, nParams;

   if( s->szRelated && ! hb_refTabIsKindOf( pTab, szClass, s->szRelated ) &&
       ! hb_refTabIsKindOf( pTab, s->szRelated, szClass ) )
      return;
   s->fAny = HB_TRUE;
   if( ! hb_refTabOwnFacts( pTab, szKey, &kept, &ret, &fSelfKept, &fSelfRet,
                            NULL, NULL ) )
      return;   /* not analysed yet: its facts come next pass */
   if( fSelfKept )
      s->iSelfCtx |= HB_OWN_KEEP;
   if( fSelfRet )
      s->iSelfCtx |= s->ctx;
   nParams = hb_refTabParamCount( pTab, szKey );
   for( i = 0; i < s->nArgs; i++ )
   {
      if( i >= nParams || i >= 64 )
      {
         if( i >= 64 || hb_refTabIsVariadic( pTab, szKey ) )
            s->aArgCtx[ i ] |= HB_OWN_KEEP | s->ctx;
         continue;
      }
      if( ( kept >> i ) & 1 )
         s->aArgCtx[ i ] |= HB_OWN_KEEP;
      if( ( ret >> i ) & 1 )
         s->aArgCtx[ i ] |= s->ctx;
   }
}

static void hb_ownSend( HB_OWNCTX * c, PHB_EXPR e, int ctx )
{
   PHB_EXPR pObj   = e->value.asMessage.pObject;
   PHB_EXPR pParms = e->value.asMessage.pParms;
   const char * szMsg = e->value.asMessage.szMessage;
   HB_BOOL fSelf = hb_ownIsSelfRecv( c, pObj );
   HB_OWNSEND s;
   PHB_EXPR pArg;
   int i;

   if( e->value.asMessage.pMessage || ! szMsg )
   {
      /* o:&( cMsg ) without parentheses reads a member by its name, which
         keeps nothing of o. o:&( cMsg )( … ) is any method of o: to Self,
         those of its class, its ancestors and its subclasses; to anything
         else, anything. The arguments may go anywhere. */
      hb_ownExpr( c, e->value.asMessage.pMessage, 0 );
      if( ! pParms )
      {
         if( fSelf )
            hb_ownNoteSelf( c, 0 );
         else
            hb_ownExpr( c, pObj, 0 );
      }
      else if( fSelf && c->szClass )
      {
         memset( &s, 0, sizeof( s ) );
         s.c = c;
         s.ctx = ctx;
         s.szRelated = c->szClass;
         hb_refTabForEachAnyMethod( c->pTab, hb_ownSendRow, &s );
         hb_ownNoteSelf( c, s.iSelfCtx );
      }
      else if( fSelf )
         hb_ownNoteSelf( c, HB_OWN_KEEP | ctx );
      else
         hb_ownExpr( c, pObj, HB_OWN_KEEP | ctx );
      hb_ownArgs( c, pParms, HB_OWN_KEEP | ctx );
      return;
   }

   memset( &s, 0, sizeof( s ) );
   s.c = c;
   s.ctx = ctx;
   s.szRelated = fSelf ? c->szClass : NULL;
   for( pArg = hb_ownFirstArg( pParms ); pArg; pArg = pArg->pNext )
      if( s.nArgs < HB_OWN_MAXARGS )
         s.nArgs++;

   /* X():msg( … ) runs X's own method, or its nearest ancestor's; New()
      without one of those runs Init() */
   if( pObj && pObj->ExprType == HB_ET_FUNCALL &&
       hb_ownNoArgs( pObj->value.asFunCall.pParms ) &&
       pObj->value.asFunCall.pFunName &&
       pObj->value.asFunCall.pFunName->ExprType == HB_ET_FUNNAME &&
       hb_refTabIsClass( c->pTab, pObj->value.asFunCall.pFunName->value.asSymbol.name ) )
   {
      char szKey[ 256 ];
      const char * szClass = pObj->value.asFunCall.pFunName->value.asSymbol.name;
      const char * szM = hb_ownResolveMethod( c->pTab, szClass, szMsg,
                                              szKey, sizeof( szKey ) );
      if( ! szM && hb_stricmp( szMsg, "New" ) == 0 )
         szM = hb_ownResolveMethod( c->pTab, szClass, "Init",
                                    szKey, sizeof( szKey ) );
      if( szM )
      {
         char szRowClass[ 256 ];
         const char * szSep = strstr( szM, "::" );
         HB_SIZE n = szSep ? ( HB_SIZE ) ( szSep - szM ) : 0;
         if( n >= sizeof( szRowClass ) )
            n = sizeof( szRowClass ) - 1;
         memcpy( szRowClass, szM, n );
         szRowClass[ n ] = '\0';
         hb_ownSendRow( szM, szRowClass, &s );
      }
      else
         s.fAny = HB_TRUE;   /* nothing of the program's runs */
   }
   else
      hb_refTabForEachMethod( c->pTab, szMsg, hb_ownSendRow, &s );

   if( ! s.fAny )
   {
      /* no class has such a method: a member read or written, or the
         message of a class the program does not define (HBObject's, an
         OLE object's), which keeps nothing of its receiver */
      s.iSelfCtx = 0;
      for( i = 0; i < s.nArgs; i++ )
         s.aArgCtx[ i ] = s_fFirstPass ? 0 : HB_OWN_KEEP | ctx;
   }

   c->szWhy = szMsg;
   if( fSelf )
      hb_ownNoteSelf( c, s.iSelfCtx );
   else
      hb_ownExpr( c, pObj, s.iSelfCtx );
   for( i = 0, pArg = hb_ownFirstArg( pParms ); pArg; pArg = pArg->pNext, i++ )
   {
      c->szWhy = szMsg;
      hb_ownExpr( c, pArg, i < s.nArgs ? s.aArgCtx[ i ] : HB_OWN_KEEP | ctx );
   }
   c->szWhy = NULL;
}

/* ---- expressions ---- */

static void hb_ownList( HB_OWNCTX * c, PHB_EXPR p, int ctx )
{
   for( ; p; p = p->pNext )
      hb_ownExpr( c, p, ctx );
}

static void hb_ownExpr( HB_OWNCTX * c, PHB_EXPR e, int ctx )
{
   if( ! e )
      return;
   switch( e->ExprType )
   {
      case HB_ET_NONE:
      case HB_ET_NIL:
      case HB_ET_NUMERIC:
      case HB_ET_DATE:
      case HB_ET_TIMESTAMP:
      case HB_ET_STRING:
      case HB_ET_LOGICAL:
      case HB_ET_FUNREF:
      case HB_ET_FUNNAME:
      case HB_ET_ALIAS:
      case HB_ET_RTVAR:
         return;

      case HB_ET_SELF:
         hb_ownNoteSelf( c, ctx );
         return;

      case HB_ET_VARIABLE:
         if( hb_ownIsSelf( e ) )
            hb_ownNoteSelf( c, ctx );
         else
            hb_ownNoteVar( c, e->value.asSymbol.name, ctx );
         return;

      case HB_ET_VARREF:
      {
         /* @x: what it is passed to may keep the value, or give the
            variable another */
         HB_OWNVAR * v = hb_ownVar( c, e->value.asSymbol.name );
         if( v )
         {
            hb_ownUse( c, v );
            v->fKept = v->fByRef = v->fOther = HB_TRUE;
            v->fHolds = HB_TRUE;
            v->nHeld++;
         }
         return;
      }

      case HB_ET_REFERENCE:
         hb_ownExpr( c, e->value.asReference, HB_OWN_KEEP );
         return;

      case HB_ET_CODEBLOCK:
      {
         int nSaved = c->nShadow;
         PHB_CBVAR pVar;
         for( pVar = e->value.asCodeblock.pLocals; pVar; pVar = pVar->pNext )
            if( c->nShadow < HB_OWN_MAXSHADOW )
               c->aszShadow[ c->nShadow++ ] = pVar->szName;
         c->iBlockDepth++;
         hb_ownList( c, e->value.asCodeblock.pExprList, HB_OWN_KEEP );
         c->iBlockDepth--;
         c->nShadow = nSaved;
         return;
      }

      case HB_ET_ARRAY:
      case HB_ET_HASH:
      case HB_ET_ARGLIST:
      case HB_ET_MACROARGLIST:
         hb_ownList( c, e->value.asList.pExprList, HB_OWN_KEEP );
         return;

      case HB_ET_IIF:
      {
         PHB_EXPR pCond = e->value.asList.pExprList;
         if( pCond )
         {
            hb_ownExpr( c, pCond, 0 );
            c->iCondDepth++;
            hb_ownList( c, pCond->pNext, ctx );
            c->iCondDepth--;
         }
         return;
      }

      case HB_ET_LIST:
      {
         PHB_EXPR p;
         for( p = e->value.asList.pExprList; p; p = p->pNext )
            hb_ownExpr( c, p, p->pNext ? 0 : ctx );
         return;
      }

      case HB_ET_ARRAYAT:
         hb_ownExpr( c, e->value.asList.pExprList, 0 );
         hb_ownExpr( c, e->value.asList.pIndex, 0 );
         return;

      case HB_ET_MACRO:
         hb_ownList( c, e->value.asMacro.pExprList, 0 );
         return;

      case HB_ET_FUNCALL:
         hb_ownCall( c, e, ctx );
         return;

      case HB_ET_SEND:
         hb_ownSend( c, e, ctx );
         return;

      case HB_ET_ALIASVAR:
      case HB_ET_ALIASEXPR:
         hb_ownExpr( c, e->value.asAlias.pAlias, HB_OWN_KEEP );
         hb_ownExpr( c, e->value.asAlias.pVar, HB_OWN_KEEP );
         hb_ownList( c, e->value.asAlias.pExpList, HB_OWN_KEEP );
         return;

      case HB_ET_SETGET:
         hb_ownExpr( c, e->value.asSetGet.pVar, HB_OWN_KEEP );
         hb_ownExpr( c, e->value.asSetGet.pExpr, HB_OWN_KEEP );
         return;

      case HB_EO_ASSIGN:
         hb_ownAssign( c, e, ctx );
         return;

      case HB_EO_PLUSEQ:
      case HB_EO_MINUSEQ:
      case HB_EO_MULTEQ:
      case HB_EO_DIVEQ:
      case HB_EO_MODEQ:
      case HB_EO_EXPEQ:
      case HB_EO_POSTINC:
      case HB_EO_POSTDEC:
      case HB_EO_PREINC:
      case HB_EO_PREDEC:
         hb_ownWritten( c, e->value.asOperator.pLeft );
         hb_ownExpr( c, e->value.asOperator.pLeft, 0 );
         hb_ownExpr( c, e->value.asOperator.pRight, 0 );
         return;

      case HB_EO_AND:
      case HB_EO_OR:
         hb_ownExpr( c, e->value.asOperator.pLeft, 0 );
         c->iCondDepth++;
         hb_ownExpr( c, e->value.asOperator.pRight, 0 );
         c->iCondDepth--;
         return;

      default:
         /* the other operators compare or compute: they keep nothing */
         if( e->ExprType > HB_ET_VARIABLE )
         {
            hb_ownExpr( c, e->value.asOperator.pLeft, 0 );
            hb_ownExpr( c, e->value.asOperator.pRight, 0 );
         }
         return;
   }
}

/* ---- statements ---- */

/* A routine's C# block: any variable it names, and Self as `this`, may
   be kept, returned or given another value */
static void hb_ownCSharp( HB_OWNCTX * c, const char * szText )
{
   const char * p = szText;
   while( p && *p )
   {
      if( HB_ISALPHA( ( HB_UCHAR ) *p ) || *p == '_' )
      {
         char szId[ 128 ];
         int n = 0;
         while( HB_ISALPHA( ( HB_UCHAR ) *p ) || HB_ISDIGIT( ( HB_UCHAR ) *p ) ||
                *p == '_' )
         {
            if( n < ( int ) sizeof( szId ) - 1 )
               szId[ n++ ] = *p;
            p++;
         }
         szId[ n ] = '\0';
         if( strcmp( szId, "this" ) == 0 )
            hb_ownNoteSelf( c, HB_OWN_KEEP | HB_OWN_RET );
         else
         {
            HB_OWNVAR * v = hb_ownVar( c, szId );
            if( v )
            {
               hb_ownUse( c, v );
               v->fKept = v->fReturned = v->fByRef = v->fOther = HB_TRUE;
            }
         }
      }
      else
         p++;
   }
}

static void hb_ownStmt( HB_OWNCTX * c, PHB_AST_NODE s, HB_BOOL fTop )
{
   PHB_AST_NODE p;

   if( s->iLine > 0 )
      c->iLine = s->iLine;
   c->szWhy = NULL;

   switch( s->type )
   {
      case HB_AST_LOCAL:
      {
         HB_OWNVAR * v = hb_ownFindVar( c->pInfo, s->value.asVar.szName );
         PHB_EXPR pInit = s->value.asVar.pInit;
         if( ! pInit || ! v )
            break;
         if( s->value.asVar.fArrayDim )
         {
            v->fOther = HB_TRUE;
            hb_ownExpr( c, pInit, 0 );
         }
         else if( hb_ownPeel( pInit )->ExprType != HB_ET_NIL )
         {
            /* the declaration's value: not a use that comes before it */
            PHB_AST_NODE pFirst = v->pFirstUse;
            hb_ownAssignVar( c, v, pInit, NULL, HB_FALSE, 0 );
            v->pFirstUse = pFirst;
            if( v->nNew == 1 && v->pOnlyNewStmt == c->pTopStmt )
               v->fOnlyNewTop = fTop;
         }
         break;
      }

      case HB_AST_STATIC:
      case HB_AST_PUBLIC:
      case HB_AST_PRIVATE:
         hb_ownExpr( c, s->value.asVar.pInit, HB_OWN_KEEP );
         break;

      case HB_AST_EXPRSTMT:
         c->pTopExpr = fTop ? s->value.asExprStmt.pExpr : NULL;
         hb_ownExpr( c, s->value.asExprStmt.pExpr, 0 );
         c->pTopExpr = NULL;
         break;

      case HB_AST_RETURN:
      {
         PHB_EXPR pVal = hb_ownPeel( s->value.asReturn.pExpr );
         if( ! pVal )
            break;
         hb_ownExpr( c, s->value.asReturn.pExpr, HB_OWN_RET );
         if( pVal->ExprType == HB_ET_NIL )
            break;
         if( hb_ownNewClass( c, pVal ) )
            c->fReturnNew = HB_TRUE;
         else if( pVal->ExprType == HB_ET_VARIABLE &&
                  hb_ownVar( c, pVal->value.asSymbol.name ) &&
                  hb_ownVar( c, pVal->value.asSymbol.name )->iParam < 0 &&
                  c->nReturned < HB_OWN_MAXRETVARS )
            c->apReturned[ c->nReturned++ ] = hb_ownVar( c, pVal->value.asSymbol.name );
         else
            c->fReturnOther = HB_TRUE;
         break;
      }

      case HB_AST_QOUT:
      case HB_AST_QQOUT:
         hb_ownExpr( c, s->value.asQOut.pExprList, 0 );
         break;

      case HB_AST_IF:
      {
         HB_BOOL * pStart, * pJoin;
         hb_ownExpr( c, s->value.asIf.pCondition, 0 );
         pStart = hb_ownHoldsSave( c );
         pJoin = hb_ownHoldsSave( c );
         hb_ownStmts( c, s->value.asIf.pThen, HB_FALSE );
         hb_ownHoldsJoin( c, pJoin );
         for( p = s->value.asIf.pElseIfs; p; p = p->pNext )
         {
            hb_ownHoldsLoad( c, pStart );
            hb_ownExpr( c, p->value.asElseIf.pCondition, 0 );
            hb_ownStmts( c, p->value.asElseIf.pBody, HB_FALSE );
            hb_ownHoldsJoin( c, pJoin );
         }
         /* pJoin began as pStart: no ELSE is that path */
         if( s->value.asIf.pElse )
         {
            hb_ownHoldsLoad( c, pStart );
            hb_ownStmts( c, s->value.asIf.pElse, HB_FALSE );
            hb_ownHoldsJoin( c, pJoin );
         }
         hb_ownHoldsLoad( c, pJoin );
         hb_xfree( pStart );
         hb_xfree( pJoin );
         break;
      }

      case HB_AST_DOWHILE:
      {
         HB_BOOL * pStart = hb_ownHoldsSave( c );
         int * pHeld = hb_ownHeldSave( c );
         c->iLoopDepth++;
         hb_ownExpr( c, s->value.asWhile.pCondition, 0 );
         hb_ownStmts( c, s->value.asWhile.pBody, HB_FALSE );
         c->iLoopDepth--;
         hb_ownHeldJoin( c, pStart, pHeld );
         hb_xfree( pStart );
         hb_xfree( pHeld );
         break;
      }

      case HB_AST_FOR:
      {
         HB_OWNVAR * v = hb_ownVar( c, s->value.asFor.szVar );
         HB_BOOL * pStart;
         int * pHeld;
         if( v )
         {
            hb_ownUse( c, v );
            v->fOther = HB_TRUE;
            v->fHolds = HB_TRUE;
            v->nHeld++;
         }
         hb_ownExpr( c, s->value.asFor.pStart, 0 );
         hb_ownExpr( c, s->value.asFor.pEnd, 0 );
         hb_ownExpr( c, s->value.asFor.pStep, 0 );
         pStart = hb_ownHoldsSave( c );
         pHeld = hb_ownHeldSave( c );
         c->iLoopDepth++;
         hb_ownStmts( c, s->value.asFor.pBody, HB_FALSE );
         c->iLoopDepth--;
         hb_ownHeldJoin( c, pStart, pHeld );
         hb_xfree( pStart );
         hb_xfree( pHeld );
         break;
      }

      case HB_AST_FOREACH:
      {
         HB_BOOL * pStart;
         int * pHeld;
         hb_ownWritten( c, s->value.asForEach.pVar );
         hb_ownExpr( c, s->value.asForEach.pEnum, HB_OWN_KEEP );
         pStart = hb_ownHoldsSave( c );
         pHeld = hb_ownHeldSave( c );
         c->iLoopDepth++;
         hb_ownStmts( c, s->value.asForEach.pBody, HB_FALSE );
         c->iLoopDepth--;
         hb_ownHeldJoin( c, pStart, pHeld );
         hb_xfree( pStart );
         hb_xfree( pHeld );
         break;
      }

      case HB_AST_DOCASE:
      {
         /* each CASE from the state before the first, joined; no
            OTHERWISE is the path where none ran */
         HB_BOOL * pStart = hb_ownHoldsSave( c );
         HB_BOOL * pJoin = hb_ownHoldsSave( c );
         for( p = s->value.asDoCase.pCases; p; p = p->pNext )
         {
            hb_ownHoldsLoad( c, pStart );
            hb_ownExpr( c, p->value.asCase.pCondition, 0 );
            hb_ownStmts( c, p->value.asCase.pBody, HB_FALSE );
            hb_ownHoldsJoin( c, pJoin );
         }
         if( s->value.asDoCase.pOtherwise )
         {
            hb_ownHoldsLoad( c, pStart );
            hb_ownStmts( c, s->value.asDoCase.pOtherwise, HB_FALSE );
            hb_ownHoldsJoin( c, pJoin );
         }
         hb_ownHoldsLoad( c, pJoin );
         hb_xfree( pStart );
         hb_xfree( pJoin );
         break;
      }

      case HB_AST_SWITCH:
      {
         /* a CASE may fall through into the next: taken as a loop */
         HB_BOOL * pStart = hb_ownHoldsSave( c );
         int * pHeld = hb_ownHeldSave( c );
         hb_ownExpr( c, s->value.asSwitch.pSwitch, 0 );
         c->iLoopDepth++;
         for( p = s->value.asSwitch.pCases; p; p = p->pNext )
         {
            hb_ownExpr( c, p->value.asCase.pCondition, 0 );
            hb_ownStmts( c, p->value.asCase.pBody, HB_FALSE );
         }
         hb_ownStmts( c, s->value.asSwitch.pDefault, HB_FALSE );
         c->iLoopDepth--;
         hb_ownHeldJoin( c, pStart, pHeld );
         hb_xfree( pStart );
         hb_xfree( pHeld );
         break;
      }

      case HB_AST_BEGINSEQ:
      {
         /* a BREAK may leave the body anywhere: taken as a loop */
         HB_BOOL * pStart = hb_ownHoldsSave( c );
         int * pHeld = hb_ownHeldSave( c );
         c->iLoopDepth++;
         hb_ownStmts( c, s->value.asSeq.pBody, HB_FALSE );
         if( s->value.asSeq.szRecoverVar )
         {
            HB_OWNVAR * v = hb_ownVar( c, s->value.asSeq.szRecoverVar );
            if( v )
            {
               hb_ownUse( c, v );
               v->fOther = HB_TRUE;
               v->fHolds = HB_TRUE;
               v->nHeld++;
            }
         }
         hb_ownStmts( c, s->value.asSeq.pRecover, HB_FALSE );
         hb_ownStmts( c, s->value.asSeq.pAlways, HB_FALSE );
         c->iLoopDepth--;
         hb_ownHeldJoin( c, pStart, pHeld );
         hb_xfree( pStart );
         hb_xfree( pHeld );
         break;
      }

      case HB_AST_WITHOBJECT:
         hb_ownExpr( c, s->value.asWithObj.pObject, HB_OWN_KEEP );
         hb_ownStmts( c, s->value.asWithObj.pBody, HB_FALSE );
         break;

      case HB_AST_BREAK:
         hb_ownExpr( c, s->value.asBreak.pExpr, HB_OWN_KEEP );
         break;

      case HB_AST_BLOCK:
         hb_ownStmts( c, s, HB_FALSE );
         break;

      case HB_AST_CSHARP:
         hb_ownCSharp( c, s->value.asCSharp.szText );
         break;

      default:
         break;
   }
}

static void hb_ownStmts( HB_OWNCTX * c, PHB_AST_NODE pBlock, HB_BOOL fTop )
{
   PHB_AST_NODE s;
   if( ! pBlock )
      return;
   if( pBlock->type != HB_AST_BLOCK )
   {
      if( fTop )
         c->pTopStmt = pBlock;
      hb_ownStmt( c, pBlock, fTop );
      return;
   }
   for( s = pBlock->value.asBlock.pFirst; s; s = s->pNext )
   {
      if( fTop )
         c->pTopStmt = s;
      hb_ownStmt( c, s, fTop );
   }
}

/* The LOCALs a body declares */
static void hb_ownCollectLocals( HB_OWNINFO * pInfo, PHB_AST_NODE pBlock,
                                 int * pnCap )
{
   PHB_AST_NODE s;
   if( ! pBlock || pBlock->type != HB_AST_BLOCK )
      return;
   for( s = pBlock->value.asBlock.pFirst; s; s = s->pNext )
   {
      if( s->type == HB_AST_LOCAL && s->value.asVar.szName &&
          ! hb_ownFindVar( pInfo, s->value.asVar.szName ) )
      {
         HB_OWNVAR * v;
         if( pInfo->nVars == *pnCap )
         {
            *pnCap = *pnCap ? *pnCap * 2 : 16;
            pInfo->pVars = ( HB_OWNVAR * ) hb_xrealloc(
               pInfo->pVars, sizeof( HB_OWNVAR ) * *pnCap );
         }
         v = &pInfo->pVars[ pInfo->nVars++ ];
         memset( v, 0, sizeof( HB_OWNVAR ) );
         v->szName = s->value.asVar.szName;
         v->iParam = -1;
      }
   }
}

HB_OWNINFO * hb_ownAnalyse( PHB_REFTAB pTab, PHB_AST_NODE pFunc, int nParams,
                            const char * szClass, PHB_AST_NODE pFileFuncs,
                            const char * szFileBase )
{
   HB_OWNINFO * pInfo = ( HB_OWNINFO * ) hb_xgrabz( sizeof( HB_OWNINFO ) );
   HB_OWNCTX c;
   PHB_HVAR pVar;
   int nCap = 0, i;

   if( ! pFunc || pFunc->type != HB_AST_FUNCTION )
      return pInfo;

   for( pVar = pFunc->value.asFunc.pParams, i = 0; pVar && i < nParams;
        pVar = pVar->pNext, i++ )
   {
      HB_OWNVAR * v;
      if( pInfo->nVars == nCap )
      {
         nCap = nCap ? nCap * 2 : 16;
         pInfo->pVars = ( HB_OWNVAR * ) hb_xrealloc(
            pInfo->pVars, sizeof( HB_OWNVAR ) * nCap );
      }
      v = &pInfo->pVars[ pInfo->nVars++ ];
      memset( v, 0, sizeof( HB_OWNVAR ) );
      v->szName = pVar->szName;
      v->iParam = i;
   }
   hb_ownCollectLocals( pInfo, pFunc->value.asFunc.pBody, &nCap );

   memset( &c, 0, sizeof( c ) );
   c.pTab       = pTab;
   c.szClass    = szClass;
   c.pFileFuncs = pFileFuncs;
   c.szFileBase = szFileBase;
   c.pInfo      = pInfo;
   {
      const char * szTrace = getenv( "HB_OWN_TRACE" );
      if( szTrace && pFunc->value.asFunc.szName &&
          ( strcmp( szTrace, "*" ) == 0 ||
            hb_stricmp( szTrace, pFunc->value.asFunc.szName ) == 0 ) )
         c.szTrace = pFunc->value.asFunc.szName;
   }
   hb_ownStmts( &c, pFunc->value.asFunc.pBody, HB_TRUE );

   /* new objects returned: each RETURN gives a new object, NIL, or a LOCAL
      that only ever holds new objects and keeps them nowhere else */
   pInfo->fReturnsNew = ! c.fReturnOther && ( c.fReturnNew || c.nReturned > 0 );
   for( i = 0; i < c.nReturned && pInfo->fReturnsNew; i++ )
   {
      HB_OWNVAR * v = c.apReturned[ i ];
      if( v->nNew == 0 || v->fOther || v->fKept || v->fByRef )
         pInfo->fReturnsNew = HB_FALSE;
   }

   /* the verdicts */
   for( i = 0; i < pInfo->nVars; i++ )
   {
      HB_OWNVAR * v = &pInfo->pVars[ i ];
      if( v->iParam >= 0 || v->nNew == 0 || v->fOther || v->fKept ||
          v->fReturned || v->fByRef || v->fUndisposable )
         continue;
      if( v->nNew == 1 && ! v->fNil && v->fOnlyNewTop &&
          ( ! v->pOnlyNew || v->pFirstUse == v->pOnlyNewStmt ) )
         v->iOwned = HB_OWN_USING;
      else
      {
         v->iOwned = HB_OWN_FINALLY;
         pInfo->fFinally = HB_TRUE;
      }
   }
   return pInfo;
}

void hb_ownFree( HB_OWNINFO * pInfo )
{
   if( pInfo )
   {
      if( pInfo->pVars )
         hb_xfree( pInfo->pVars );
      hb_xfree( pInfo );
   }
}

HB_BOOL hb_ownRecordFacts( PHB_REFTAB pTab, const char * szKey,
                           HB_OWNINFO * pInfo, PHB_AST_NODE pFunc,
                           const char * szClass )
{
   HB_U64 kept = 0, returned = 0;
   int i;
   if( ! pTab || ! szKey || ! pInfo )
      return HB_FALSE;
   for( i = 0; i < pInfo->nVars; i++ )
   {
      HB_OWNVAR * v = &pInfo->pVars[ i ];
      if( v->iParam < 0 || v->iParam >= 64 )
         continue;
      if( v->fKept || v->fByRef )
         kept |= ( ( HB_U64 ) 1 ) << v->iParam;
      if( v->fReturned )
         returned |= ( ( HB_U64 ) 1 ) << v->iParam;
   }
   return hb_refTabSetOwnFacts( pTab, szKey, kept, returned,
                         pInfo->fSelfKept, pInfo->fSelfReturned,
                         pInfo->fReturnsNew,
                         szClass && pFunc && pFunc->value.asFunc.pBody &&
                         hb_astReturnsSelfOrNil( pFunc->value.asFunc.pBody ) );
}

/* ---- INLINE methods ---- */

typedef struct
{
   PHB_REFTAB   pTab;
   const char * szClass;
   HB_BOOL      fSelfKept;
   HB_BOOL      fSelfReturned;
} HB_OWNINLINE;

static void hb_ownInlineRow( const char * szKey, const char * szClass,
                             void * cargo )
{
   HB_OWNINLINE * s = ( HB_OWNINLINE * ) cargo;
   HB_BOOL fSelfKept = HB_FALSE, fSelfRet = HB_FALSE;
   if( ! hb_refTabIsKindOf( s->pTab, szClass, s->szClass ) &&
       ! hb_refTabIsKindOf( s->pTab, s->szClass, szClass ) )
      return;
   hb_refTabOwnFacts( s->pTab, szKey, NULL, NULL, &fSelfKept, &fSelfRet,
                      NULL, NULL );
   if( fSelfKept )
      s->fSelfKept = HB_TRUE;
   if( fSelfRet )
      s->fSelfReturned = HB_TRUE;
}

/* An INLINE method has text, not a body: read it word by word. Self is
   kept and returned if it is named, or a codeblock reads a member; a
   `::Method(` passes on that method's Self facts; a parameter named
   anywhere is kept and returned. */
HB_BOOL hb_ownInlineFacts( PHB_REFTAB pTab, const char * szKey,
                           const char * szClass, const char * szInline,
                           const char * szParams )
{
   const char * apParams[ 64 ];
   char szPBuf[ 256 ];
   int nParams = 0, i;
   HB_U64 kept = 0;
   HB_OWNINLINE s;
   const char * p;
   char cPrev = 0, cPrev2 = 0;   /* the two characters before a word */

   if( ! pTab || ! szKey || ! szInline )
      return HB_FALSE;
   memset( &s, 0, sizeof( s ) );
   s.pTab = pTab;
   s.szClass = szClass;

   if( szParams && *szParams )
   {
      char * q = szPBuf;
      hb_strncpy( szPBuf, szParams, sizeof( szPBuf ) - 1 );
      while( *q && nParams < 64 )
      {
         while( *q == ' ' || *q == ',' )
            q++;
         if( ! *q )
            break;
         apParams[ nParams++ ] = q;
         while( *q && *q != ',' && *q != ' ' )
            q++;
         if( *q )
            *q++ = '\0';
      }
   }

   if( strchr( szInline, '{' ) && strstr( szInline, "::" ) )
      s.fSelfKept = HB_TRUE;

   for( p = szInline; *p; )
   {
      if( *p == '"' || *p == '\'' )
      {
         char cQuote = *p++;
         while( *p && *p != cQuote )
            p++;
         if( *p )
            p++;
         cPrev2 = cPrev;
         cPrev = cQuote;
         continue;
      }
      if( HB_ISALPHA( ( HB_UCHAR ) *p ) || *p == '_' )
      {
         char szId[ 128 ];
         int n = 0;
         const char * q;
         HB_BOOL fColon = cPrev == ':';
         HB_BOOL fSelfSend = fColon && cPrev2 == ':';
         while( HB_ISALPHA( ( HB_UCHAR ) *p ) || HB_ISDIGIT( ( HB_UCHAR ) *p ) ||
                *p == '_' )
         {
            if( n < ( int ) sizeof( szId ) - 1 )
               szId[ n++ ] = *p;
            p++;
         }
         szId[ n ] = '\0';
         for( q = p; *q == ' ' || *q == '\t'; q++ )
            ;
         if( ! fColon && hb_stricmp( szId, "Self" ) == 0 )
            s.fSelfKept = s.fSelfReturned = HB_TRUE;
         else if( fSelfSend && *q == '(' )
            hb_refTabForEachMethod( pTab, szId, hb_ownInlineRow, &s );
         else if( ! fColon )
            for( i = 0; i < nParams; i++ )
               if( hb_stricmp( apParams[ i ], szId ) == 0 )
                  kept |= ( ( HB_U64 ) 1 ) << i;
         cPrev2 = 'a';
         cPrev = 'a';
         continue;
      }
      if( *p != ' ' && *p != '\t' )
      {
         cPrev2 = cPrev;
         cPrev = *p;
      }
      p++;
   }
   return hb_refTabSetOwnFacts( pTab, szKey, kept, kept, s.fSelfKept,
                                s.fSelfReturned, HB_FALSE, HB_FALSE );
}
