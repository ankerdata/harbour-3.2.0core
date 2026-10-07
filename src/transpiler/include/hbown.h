/*
 * Harbour Transpiler - ownership: the variables that hold the only
 * reference to a new object
 *
 * Harbour counts references and runs an object's DESTRUCTOR the moment
 * the last one goes: at `o := NIL`, when o is given another value, or
 * when the routine holding o returns. C# counts nothing: a finalizer
 * runs when the collector gets to the object, and a Debug build keeps a
 * routine's references alive until it returns. Where the transpiler can
 * prove that a variable holds the only reference to an object, the C#
 * disposes it at those same points (gencsharp.c); an object it cannot
 * prove that of is left to its finalizer.
 *
 * Copyright 2026 harbour.github.io
 */

#ifndef HB_OWN_H_
#define HB_OWN_H_

#include "hbast.h"
#include "hbreftab.h"

HB_EXTERN_BEGIN

/* What the analysis found out about one variable of a routine */
typedef struct
{
   const char *   szName;
   int            iParam;        /* parameter slot, or -1 for a LOCAL */
   HB_BOOL        fKept;         /* its value may outlive the routine: stored,
                                    captured by a codeblock, or passed on to
                                    something that keeps it */
   HB_BOOL        fReturned;     /* its value may be returned */
   HB_BOOL        fByRef;        /* passed by reference somewhere */
   HB_BOOL        fOther;        /* given a value that is neither new nor NIL */
   HB_BOOL        fNil;          /* given NIL (its declaration aside) */
   HB_BOOL        fUndisposable; /* given a new object of a class with nothing
                                    to dispose */
   int            nNew;          /* how often it is given a new object */
   PHB_EXPR       pOnlyNew;      /* the first new object's assignment, NULL
                                    when it is the LOCAL's initializer */
   PHB_AST_NODE   pOnlyNewStmt;  /* the top-level statement holding it */
   HB_BOOL        fOnlyNewTop;   /* that assignment is a top-level statement
                                    of its own, or the LOCAL's initializer */
   PHB_AST_NODE   pFirstUse;     /* the first top-level statement naming it */
   int            iOwned;        /* the verdict: HB_OWN_NOT, HB_OWN_USING or
                                    HB_OWN_FINALLY */
   HB_BOOL        fHolds;        /* during the walk: it may hold a value here */
   int            nHeld;         /* during the walk: how often it was given one */
   PHB_EXPR       apEmpty[ 16 ]; /* assignments of a new object that find it
                                    holding nothing on every path: no old value
                                    to dispose */
   int            nEmpty;
} HB_OWNVAR;

/* Does pAssign give v a new object where v holds nothing on every path? */
extern HB_BOOL      hb_ownFindsEmpty( HB_OWNVAR * v, PHB_EXPR pAssign );

#define HB_OWN_NOT      0
#define HB_OWN_USING    1   /* one new object, given at the top level: a C#
                               `using` declaration */
#define HB_OWN_FINALLY  2   /* disposed when given another value, and when
                               the routine returns (try/finally) */

typedef struct
{
   int         nVars;
   HB_OWNVAR * pVars;
   HB_BOOL     fSelfKept;
   HB_BOOL     fSelfReturned;
   HB_BOOL     fReturnsNew;
   HB_BOOL     fFinally;     /* some local is HB_OWN_FINALLY */
} HB_OWNINFO;

/* Analyses a routine. nParams is its declared parameter count; szClass
   the class of a method, NULL for a function; pFileFuncs the file's
   function list and szFileBase its base name, which decide a call to one
   of the file's STATIC functions. */
extern HB_OWNINFO * hb_ownAnalyse( PHB_REFTAB pTab, PHB_AST_NODE pFunc,
                                   int nParams, const char * szClass,
                                   PHB_AST_NODE pFileFuncs,
                                   const char * szFileBase );
extern void         hb_ownFree( HB_OWNINFO * pInfo );

/* Records what the analysis found on the routine's reftab row; TRUE when
   that changed the row. */
extern HB_BOOL      hb_ownRecordFacts( PHB_REFTAB pTab, const char * szKey,
                                       HB_OWNINFO * pInfo, PHB_AST_NODE pFunc,
                                       const char * szClass );

/* The facts of an INLINE method, read from its text; TRUE when that
   changed its row. */
extern HB_BOOL      hb_ownInlineFacts( PHB_REFTAB pTab, const char * szKey,
                                       const char * szClass,
                                       const char * szInline,
                                       const char * szParams );

extern HB_OWNVAR *  hb_ownFindVar( HB_OWNINFO * pInfo, const char * szName );

/* A class whose objects C# disposes: one with a DESTRUCTOR or inheriting
   one, and an hbsqlit3 connection ("Sqlite3Db"). */
extern HB_BOOL      hb_ownClassDisposable( PHB_REFTAB pTab, const char * szClass );

/* --own-first-pass: a cold scan's first pass, whose reftab does not yet
   know every routine: a name it does not know keeps nothing this pass */
extern void         hb_ownSetFirstPass( HB_BOOL fFirstPass );

/* --own-facts=<path>: the program's C functions, which the scan cannot
   read. One per line, `NAME<TAB>-` for a function that keeps none of its
   arguments, or `NAME<TAB>1,3` naming (from 1) those it may keep; '#'
   starts a comment. A C function not listed keeps anything. */
extern void         hb_ownSetFactsPath( const char * szPath );

HB_EXTERN_END

#endif /* HB_OWN_H_ */
