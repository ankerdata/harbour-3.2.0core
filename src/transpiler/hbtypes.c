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
#include "hbdefinemap.h"
#include "hbfieldtypes.h"

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
      {
         /* Key-typed hash inference. The emitter maps HASHC to a
            string-keyed Dictionary and HASHN to a decimal-keyed one;
            bare HASH means "keys unknown" and is the weak, upgradeable
            member of the family (subscript usage or a key-typed
            literal may refine it later — mirrors how OBJECT upgrades
            to a concrete class). A literal with no pairs, non-literal
            keys, or mixed key types stays HASH. */
         PHB_EXPR pItem = pExpr->value.asList.pExprList;
         HB_BOOL fAllStr = HB_TRUE, fAllNum = HB_TRUE, fAny = HB_FALSE;
         while( pItem )
         {
            PHB_EXPR pVal = pItem->pNext;
            fAny = HB_TRUE;
            if( pItem->ExprType != HB_ET_STRING )
               fAllStr = HB_FALSE;
            if( pItem->ExprType != HB_ET_NUMERIC )
               fAllNum = HB_FALSE;
            if( ! pVal )
               break;
            pItem = pVal->pNext;
         }
         if( fAny && fAllStr )
            return "HASHC";
         if( fAny && fAllNum )
            return "HASHN";
         return "HASH";
      }

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

      case HB_ET_VARIABLE:
      {
         /* A mapped #define reference carries the exact C# type it was
            emitted with (defines_map.txt type column). Typing it here —
            the shared inference choke point — makes every path agree with
            the emitted const: an integer-valued define is INTEGER, which
            keeps it cast-free at array subscripts / HASHN keys and stops
            the INTEGER-vs-decimal W0022 against `as int` members.
            long/decimal stay NUMERIC (INTEGER emits as C# int, which
            would overflow a >Int32 bit-flag mask); string/bool map
            through. Non-define identifiers return NULL as before, so the
            caller's typeEnv / Hungarian handling is unaffected. */
         const char * szDefType = pExpr->value.asSymbol.name
            ? hb_defineMapLookupType( pExpr->value.asSymbol.name ) : NULL;
         if( szDefType )
         {
            if( hb_stricmp( szDefType, "int" ) == 0 )
               return "INTEGER";
            if( hb_stricmp( szDefType, "string" ) == 0 )
               return "STRING";
            if( hb_stricmp( szDefType, "bool" ) == 0 )
               return "LOGICAL";
            return "NUMERIC";   /* long, decimal */
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

/* ================================================================
 * Type-insufficiency audit sink (--type-audit=<path>)
 *
 * Emitters throughout the inference passes report every silent type
 * fallback here as a TSV row: category, file, line, symbol, detail,
 * suggested fix. Rows are deduped on (category, file, line, symbol)
 * for the process lifetime; the file is opened lazily in append mode
 * so per-file transpiler invocations accumulate into one report.
 * ================================================================ */
static char   s_szAuditPath[ 1024 ] = "";
static FILE * s_pAuditFile = NULL;

#define HB_AUDIT_DEDUP 4096
static struct { char szKey[ 192 ]; } s_aAuditSeen[ HB_AUDIT_DEDUP ];
static int s_iAuditSeen = 0;

void hb_auditSetPath( const char * szPath )
{
   if( szPath )
      hb_strncpy( s_szAuditPath, szPath, sizeof( s_szAuditPath ) - 1 );
}

HB_BOOL hb_auditActive( void )
{
   return s_szAuditPath[ 0 ] != '\0';
}

void hb_auditEmit( const char * szCat, const char * szFile, int iLine,
                   const char * szSymbol, const char * szDetail,
                   const char * szFix )
{
   char szKey[ 192 ];
   int  i;

   if( ! hb_auditActive() || ! szCat )
      return;
   hb_snprintf( szKey, sizeof( szKey ), "%s|%s|%d|%s",
                szCat, szFile ? szFile : "?", iLine,
                szSymbol ? szSymbol : "?" );
   for( i = 0; i < s_iAuditSeen; i++ )
      if( strcmp( s_aAuditSeen[ i ].szKey, szKey ) == 0 )
         return;
   if( s_iAuditSeen < HB_AUDIT_DEDUP )
      hb_strncpy( s_aAuditSeen[ s_iAuditSeen++ ].szKey, szKey,
                  sizeof( s_aAuditSeen[ 0 ].szKey ) - 1 );
   if( ! s_pAuditFile )
   {
      s_pAuditFile = hb_fopen( s_szAuditPath, "a" );
      if( ! s_pAuditFile )
         return;
      /* Fresh file — write the self-describing header. Appending
         invocations (one per source file) land mid-file and skip it. */
      if( ftell( s_pAuditFile ) == 0 )
         fprintf( s_pAuditFile,
"# Harbour transpiler type-insufficiency audit (--type-audit)\n"
"# Columns: CATEGORY <TAB> FILE <TAB> LINE <TAB> SYMBOL <TAB> DETAIL <TAB> SUGGESTED-FIX\n"
"#\n"
"# Categories (transpiler-emitted):\n"
"#   RET-SENTINEL  a function's RETURN statements mix types (or mix a typed\n"
"#                 branch with an uninferrable one), so its return degrades\n"
"#                 to USUAL/dynamic and poisons every caller's inference.\n"
"#                 Fix: return NIL for 'no result' (NIL carries no type and\n"
"#                 does not degrade), or split the function.\n"
"#   W0022-USUAL   call sites disagree on a parameter's type; the slot was\n"
"#                 downgraded to USUAL (mirrors the W0022 warning).\n"
"#                 Fix: reconcile the call sites, or accept the dynamic slot.\n"
"#   HASH-WEAK     a hash whose key type never resolved — emitted string-keyed\n"
"#                 on faith. Fine if it IS string-keyed; a numeric-keyed hash\n"
"#                 needs key evidence (key-typed literal or typed subscript).\n"
"#   VAR-UNTYPED   a class VAR/DATA member with no AS clause and no typed\n"
"#                 initializer — emits as dynamic, so every send through it\n"
"#                 is unchecked dynamic dispatch (the CS1061 feed).\n"
"#                 Fix: AS clause or a typed INIT.\n"
"#   NAME-CONTRACT a variable/parameter whose name claims one type while the\n"
"#                 evidence says another (aLine receiving ':nType' sends holds\n"
"#                 an object; a param name disagreeing with its converged\n"
"#                 slot). Fix: rename per the soft-typing convention.\n"
"#   TYPED-VIEW    a discriminant branch (oVar:nType == CONST) whose body\n"
"#                 sends N messages to the still-dynamic oVar. Fix: a\n"
"#                 branch-local typed view — `local o<Class>` assigned from\n"
"#                 oVar at the branch top — turns the accesses into\n"
"#                 compile-checked members via name-matches-class.\n"
"#   INT-CONFLICT  a numeric used as an array/hash index (looks like an int)\n"
"#                 but disqualified from C# int emission: 'hard' = division /\n"
"#                 fractional value (also fires warning W0026); 'soft' = a\n"
"#                 non-integral assignment or @by-ref pass (audit-only).\n"
"#                 Fix: wrap the disqualifying expression with int(), or\n"
"#                 accept decimal. Clean candidates emit as int silently.\n"
"#\n"
"# Categories (added by scripts/audit.sh from reftab.tab):\n"
"#   REF-UNTYPED   a by-ref (@) parameter slot typed USUAL/dynamic — C# ref\n"
"#                 needs exact type identity with the caller's local (the\n"
"#                 CS1620 feed). Fix: AS type on the parameter.\n"
"#   PARAM-OBJECT  an o-prefixed parameter stuck at generic OBJECT after\n"
"#                 convergence — receivers dispatch dynamically. Fix:\n"
"#                 AS CLASS, or constructor-typed callers.\n"
"#\n" );
   }
   fprintf( s_pAuditFile, "%s\t%s\t%d\t%s\t%s\t%s\n",
            szCat,
            szFile ? hb_strCollapsePath( szFile ) : "?",
            iLine,
            szSymbol ? szSymbol : "?",
            szDetail ? szDetail : "",
            szFix ? szFix : "" );
   fflush( s_pAuditFile );
}

/* ---- INT-candidate tracking (per function) ----
   A NUMERIC variable used in index position (array subscript, hash
   key, FOR variable) is a candidate for C# `int` emission. Two
   disqualifier severities:
     - fNonIntHard (division operand, fractional literal): Harbour
       n/2 = 1.5 vs C# int/int truncation — the variable stays
       decimal and, when index-used, earns a W0026 pointing at the
       disqualifying site so the source can be fixed (Alex's rule:
       no silent demotion of something that looks like an int).
     - fNonIntSoft (non-integral assignment RHS, passed by @): keeps
       the variable decimal but only surfaces in the --type-audit
       (INT-CONFLICT) — warning on these would flood the scan gate.
   Clean index-used candidates upgrade to INTEGER after Pass 2 and
   emit as C# int. Reset per hb_astPropagate run. */
#define HB_AUDIT_MAXCAND 256
static struct
{
   const char * szName;
   int          iLine;
   HB_BOOL      fIndexUsed;
   HB_BOOL      fNonIntHard;
   HB_BOOL      fNonIntSoft;
   int          iNonIntLine;
} s_aIntCand[ HB_AUDIT_MAXCAND ];
static int s_iIntCand = 0;

#define HB_INT_MARK_INDEX 1
#define HB_INT_MARK_HARD  2
#define HB_INT_MARK_SOFT  3

static void hb_auditIntMark( const char * szName, int iLine, int iKind )
{
   int i;
   if( ! szName )
      return;
   for( i = 0; i < s_iIntCand; i++ )
      if( hb_stricmp( s_aIntCand[ i ].szName, szName ) == 0 )
         break;
   if( i == s_iIntCand )
   {
      if( s_iIntCand >= HB_AUDIT_MAXCAND )
         return;
      s_aIntCand[ s_iIntCand ].szName = szName;
      s_aIntCand[ s_iIntCand ].iLine = iLine;
      s_aIntCand[ s_iIntCand ].fIndexUsed = HB_FALSE;
      s_aIntCand[ s_iIntCand ].fNonIntHard = HB_FALSE;
      s_aIntCand[ s_iIntCand ].fNonIntSoft = HB_FALSE;
      s_aIntCand[ s_iIntCand ].iNonIntLine = 0;
      s_iIntCand++;
   }
   if( iKind == HB_INT_MARK_INDEX )
      s_aIntCand[ i ].fIndexUsed = HB_TRUE;
   else if( ! s_aIntCand[ i ].fNonIntHard && ! s_aIntCand[ i ].fNonIntSoft )
      s_aIntCand[ i ].iNonIntLine = iLine;
   if( iKind == HB_INT_MARK_HARD )
      s_aIntCand[ i ].fNonIntHard = HB_TRUE;
   else if( iKind == HB_INT_MARK_SOFT )
      s_aIntCand[ i ].fNonIntSoft = HB_TRUE;
}

/* ---- HASH key-type family ----
   "HASH" (keys unknown — weak), "HASHC" (string keys), "HASHN"
   (numeric keys). See hbast.h. */
HB_BOOL hb_astIsHashFamily( const char * szType )
{
   return szType && (
      hb_stricmp( szType, "HASH"  ) == 0 ||
      hb_stricmp( szType, "HASHC" ) == 0 ||
      hb_stricmp( szType, "HASHN" ) == 0 );
}

const char * hb_astHashFamilyMerge( const char * szA, const char * szB )
{
   if( ! hb_astIsHashFamily( szA ) || ! hb_astIsHashFamily( szB ) )
      return NULL;
   if( hb_stricmp( szA, szB ) == 0 )
      return szA;
   if( hb_stricmp( szA, "HASH" ) == 0 )
      return szB;             /* weak yields to the key-typed member */
   if( hb_stricmp( szB, "HASH" ) == 0 )
      return szA;
   return NULL;               /* HASHC vs HASHN — real conflict */
}

/* Definite value-type tags — the types where a disagreement between a
   call-site argument and a declared slot is a real contradiction (a
   class name vs OBJECT is just uncertainty; NUMERIC vs STRING is not). */
static HB_BOOL hb_astIsScalarTag( const char * szType )
{
   return szType && (
      hb_stricmp( szType, "NUMERIC"   ) == 0 ||
      hb_stricmp( szType, "INTEGER"   ) == 0 ||
      hb_stricmp( szType, "STRING"    ) == 0 ||
      hb_stricmp( szType, "LOGICAL"   ) == 0 ||
      hb_stricmp( szType, "DATE"      ) == 0 ||
      hb_stricmp( szType, "TIMESTAMP" ) == 0 ||
      hb_stricmp( szType, "ARRAY"     ) == 0 ||
      hb_astIsHashFamily( szType ) );
}

/* W0024/W0025 per-(line,name) dedup — defined with the W0024 machinery
   below. */
static HB_BOOL hb_astHungSeen( int iLine, const char * szName );

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
   HB_BOOL      fFrozen;   /* conflict-widened — refuse re-refinement */
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

   /* Update existing entry. A conflict-frozen slot refuses further
      refinement: two unrelated classes already widened it to USUAL,
      and letting the next fixed-point iteration lift it back to one
      arm's class re-triggers the widening — an USUAL <-> class
      oscillation that spins hb_astPropagate forever (adt2tbl/getrcptm/
      xzutil scan timeouts once ConstructORMTable made polymorphic
      class locals common). Returning FALSE leaves pfChanged alone. */
   for( i = 0; i < pEnv->count; i++ )
   {
      if( hb_stricmp( pEnv->entries[ i ].szName, szName ) == 0 )
      {
         if( pEnv->entries[ i ].fFrozen )
            return HB_FALSE;
         pEnv->entries[ i ].szType = szType;
         return HB_TRUE;
      }
   }

   /* Add new entry */
   if( pEnv->count < HB_TYPEENV_MAX )
   {
      pEnv->entries[ pEnv->count ].szName = szName;
      pEnv->entries[ pEnv->count ].szType = szType;
      pEnv->entries[ pEnv->count ].fFrozen = HB_FALSE;
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

/* Set szName to szType and freeze the slot against re-refinement —
   used when class widening hits UNRELATED classes and must park the
   variable at USUAL permanently (a real ancestor stays unfrozen: a
   third sibling may widen it further up the chain, which is monotonic
   and terminates). */
static HB_BOOL hb_typeEnvFreeze( HB_TYPEENV * pEnv, const char * szName,
                                 const char * szType )
{
   int i;

   for( i = 0; i < pEnv->count; i++ )
   {
      if( hb_stricmp( pEnv->entries[ i ].szName, szName ) == 0 )
      {
         HB_BOOL fChanged = ! pEnv->entries[ i ].fFrozen ||
            hb_stricmp( pEnv->entries[ i ].szType, szType ) != 0;
         pEnv->entries[ i ].szType  = szType;
         pEnv->entries[ i ].fFrozen = HB_TRUE;
         return fChanged;
      }
   }
   if( hb_typeEnvSet( pEnv, szName, szType ) )
   {
      pEnv->entries[ pEnv->count - 1 ].fFrozen = HB_TRUE;
      return HB_TRUE;
   }
   return HB_FALSE;
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

/* Line+key warning dedup shared by W0028-W0032 — defined with the ORM
   contract checks below. */
static HB_BOOL hb_astOrmSeen( int iLine, const char * szKey );

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

         /* ORM def-class receiver — `oDepartment:nNo` where the
            receiver's type is a mapped def class (seeded by the
            ConstructORMTable pattern below and carried through the
            normal class-type propagation). The def files fully type
            every field, so the accessor resolves to its exact type:
            oDepartment:nNo INTEGER but oPLU:nNo NUMERIC(long-backed) —
            per-class, never a global accessor merge. Map misses fall
            through: ORM base members (Seek/RecLock/lReadOnly...) keep
            their existing handling.

            The receiver is typed by a VARIABLE typeEnv lookup ONLY —
            deliberately not hb_astInferExprType. Recursing into the
            receiver subtree on every SEND type query re-infers shared
            subtrees per enclosing query, which goes super-linear on
            deep expressions: adt2tbl/getrcptm/xzutil blew a 60s scan
            timeout (baseline 0.08s) with the recursive form. Variable
            receivers are the whole ORM convention anyway. */
         if( pExpr->value.asMessage.szMessage &&
             pExpr->value.asMessage.pObject &&
             pExpr->value.asMessage.pObject->ExprType == HB_ET_VARIABLE )
         {
            const char * szRecvType = hb_typeEnvGet( pEnv,
               pExpr->value.asMessage.pObject->value.asSymbol.name );
            if( szRecvType && hb_fieldTypesClassCanon( szRecvType ) )
            {
               const char * szCs = hb_fieldTypesMember(
                  szRecvType, pExpr->value.asMessage.szMessage, NULL );
               const char * szHb = hb_fieldTypesHbType( szCs );
               if( szHb )
                  return szHb;
            }
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
            const char * szRet;

            /* ORM construction — ConstructORMTable(XxxDef(...), ...)
               names the def class lexically in its first argument;
               when XxxDef is in the fieldtypes map the expression IS
               an instance of the generated model class (emitted as
               `new XxxDef(...)`), so return the canonical class name.
               Checked before the generic return-type tables: reftab
               knows ConstructORMTable only as OBJECT-returning. The
               3 corpus sites that pass a variable/hash instead of a
               XxxDef() call miss here and stay dynamic. */
            if( hb_stricmp( szFunc, "ConstructORMTable" ) == 0 )
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
                     return szCanon;
               }
            }

            szRet = hb_funcTabReturnType( szFunc );
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

/* A concrete class name — anything that is not one of the built-in
   scalar / marker type tags (mirrors hbreftab's hb_refTabIsScalarType,
   which is file-static there). Used to decide when two differing
   assignment types are both classes and should widen to a common
   ancestor rather than conflict. OBJECT/USUAL are markers, not classes. */
static HB_BOOL hb_astIsClassType( const char * sz )
{
   if( ! sz || ! *sz )
      return HB_FALSE;
   return
      hb_stricmp( sz, "NUMERIC"   ) != 0 && hb_stricmp( sz, "INTEGER"   ) != 0 &&
      hb_stricmp( sz, "DECIMAL"   ) != 0 && hb_stricmp( sz, "STRING"    ) != 0 &&
      hb_stricmp( sz, "CHARACTER" ) != 0 && hb_stricmp( sz, "LOGICAL"   ) != 0 &&
      hb_stricmp( sz, "DATE"      ) != 0 && hb_stricmp( sz, "TIMESTAMP" ) != 0 &&
      hb_stricmp( sz, "ARRAY"     ) != 0 && hb_stricmp( sz, "HASH"      ) != 0 &&
      hb_stricmp( sz, "HASHC"     ) != 0 && hb_stricmp( sz, "HASHN"     ) != 0 &&
      hb_stricmp( sz, "BLOCK"     ) != 0 && hb_stricmp( sz, "CODEBLOCK" ) != 0 &&
      hb_stricmp( sz, "NIL"       ) != 0 && hb_stricmp( sz, "SYMBOL"    ) != 0 &&
      hb_stricmp( sz, "POINTER"   ) != 0 && hb_stricmp( sz, "OBJECT"    ) != 0 &&
      hb_stricmp( sz, "USUAL"     ) != 0 && hb_stricmp( sz, "FUNREF"    ) != 0;
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
   /* Weak HASH (keys unknown) upgrades to a key-typed HASHC/HASHN
      observed on the RHS — a key-typed literal or a call returning
      one. Any non-family RHS leaves the declared HASH alone (same
      spirit as the x-prefix guard: `h` committed to hash-ness, only
      the key type is open). */
   else if( strcmp( szCurType, "HASH" ) == 0 )
   {
      const char * szNewType = hb_astInferExprType( pRHS, pEnv );
      const char * szMerged  = hb_astHashFamilyMerge( szCurType, szNewType );
      if( szMerged && strcmp( szMerged, szCurType ) != 0 )
      {
         if( hb_typeEnvSet( pEnv, szVarName, szMerged ) )
            *pfChanged = HB_TRUE;
      }
   }
   /* A different concrete class assigned over an existing one — a
      polymorphic builder (`oNew := FcnTranLine():New()` in one arm,
      `GenTranLine():New()` in another). Widen to the nearest common
      ancestor via the INHERIT edges — the same rule the RETURN-type and
      by-ref-param paths use — so the emitted local is the base class
      every sibling assignment upcasts to, not the first arm's narrow
      class (which the others then fail to convert to: CS0029). Unrelated
      classes fall back to USUAL (dynamic). Class names only, so numeric /
      hash / scalar locals keep their own merge rules above. */
   else if( s_pPropRefTab && hb_astIsClassType( szCurType ) )
   {
      const char * szNewType = hb_astInferExprType( pRHS, pEnv );
      if( szNewType && hb_astIsClassType( szNewType ) &&
          hb_stricmp( szCurType, szNewType ) != 0 )
      {
         const char * szAnc = NULL;
         const char * szUp  = szNewType;
         int iDepth;
         for( iDepth = 0; szUp && iDepth < 16; iDepth++ )
         {
            if( hb_refTabIsKindOf( s_pPropRefTab, szCurType, szUp ) )
            {
               szAnc = szUp;
               break;
            }
            szUp = hb_refTabClassParent( s_pPropRefTab, szUp );
         }
         if( szAnc )
         {
            /* Related classes — widen to the common ancestor. Left
               unfrozen: a third sibling may widen further up the
               chain (monotonic, terminates). */
            if( hb_stricmp( szCurType, szAnc ) != 0 &&
                hb_typeEnvSet( pEnv, szVarName, szAnc ) )
               *pfChanged = HB_TRUE;
         }
         else
         {
            /* Unrelated classes — park at USUAL and FREEZE, or the
               next fixed-point pass re-refines USUAL back to the
               first arm's class and the widening here flips it back:
               an oscillation that never converges. */
            if( hb_typeEnvFreeze( pEnv, szVarName, "USUAL" ) )
               *pfChanged = HB_TRUE;
         }
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

/* Current statement line for the observation walkers — set per
   statement in hb_astObserveBlock so expression-level marks carry a
   usable line without threading a parameter through every visit. */
static int s_iObserveLine = 0;

/* Provably-integral expression: safe as the RHS of an assignment to a
   C#-int variable. Conservative — anything unproven is non-integral.
   Literals without a fractional part, a small set of index-producing
   builtins, INTEGER-typed variables, and +,-,*,++,-- combinations
   thereof qualify. Division never does (Harbour 5/2 = 2.5). */
static HB_BOOL hb_astExprIsIntegral( PHB_EXPR pExpr, HB_TYPEENV * pEnv,
                                     const char * szSelf )
{
   if( ! pExpr )
      return HB_FALSE;
   switch( pExpr->ExprType )
   {
      case HB_ET_NUMERIC:
         return pExpr->value.asNum.NumType == HB_ET_LONG;
      case HB_ET_VARIABLE:
      {
         const char * szT;
         /* Self-reference in the RHS of the variable's own assignment
            (`nIdx := nIdx + 2`): if the variable ends up int, this is
            int arithmetic; if it doesn't, the disqualifier that stops
            it lies elsewhere. Either way the self-reference itself is
            not evidence against. */
         if( szSelf && hb_stricmp( pExpr->value.asSymbol.name,
                                   szSelf ) == 0 )
            return HB_TRUE;
         szT = hb_astInferExprType( pExpr, pEnv );
         return szT && hb_stricmp( szT, "INTEGER" ) == 0;
      }
      case HB_ET_FUNCALL:
         if( pExpr->value.asFunCall.pFunName &&
             pExpr->value.asFunCall.pFunName->ExprType == HB_ET_FUNNAME )
         {
            const char * szFn =
               pExpr->value.asFunCall.pFunName->value.asSymbol.name;
            if( szFn && (
                hb_stricmp( szFn, "INT"  ) == 0 ||
                hb_stricmp( szFn, "LEN"  ) == 0 ||
                hb_stricmp( szFn, "AT"   ) == 0 ||
                hb_stricmp( szFn, "RAT"  ) == 0 ||
                hb_stricmp( szFn, "ASC"  ) == 0 ) )
               return HB_TRUE;
            /* User function whose return already resolved to INTEGER
               (its own index-shaped locals) — int-ness chains. */
            if( szFn && pEnv && pEnv->pRefTab )
            {
               const char * szRet =
                  hb_refTabReturnType( pEnv->pRefTab, szFn );
               return szRet && hb_stricmp( szRet, "INTEGER" ) == 0;
            }
         }
         return HB_FALSE;
      case HB_EO_PLUS:
      case HB_EO_MINUS:
      case HB_EO_MULT:
         return hb_astExprIsIntegral( pExpr->value.asOperator.pLeft,
                                      pEnv, szSelf ) &&
                hb_astExprIsIntegral( pExpr->value.asOperator.pRight,
                                      pEnv, szSelf );
      case HB_EO_NEGATE:
      case HB_EO_PREINC:
      case HB_EO_PREDEC:
      case HB_EO_POSTINC:
      case HB_EO_POSTDEC:
         return hb_astExprIsIntegral( pExpr->value.asOperator.pLeft,
                                      pEnv, szSelf );
      default:
         return HB_FALSE;
   }
}

/* Count member sends on szVar inside an expression / block — the
   payoff weight for a branch-local typed view (audit TYPED-VIEW). */
static int hb_astCountSendsOnExpr( PHB_EXPR pExpr, const char * szVar )
{
   int n = 0;
   if( ! pExpr )
      return 0;
   switch( pExpr->ExprType )
   {
      case HB_ET_SEND:
         if( pExpr->value.asMessage.pObject &&
             pExpr->value.asMessage.pObject->ExprType == HB_ET_VARIABLE &&
             hb_stricmp( pExpr->value.asMessage.pObject->value.asSymbol.name,
                         szVar ) == 0 )
            n++;
         n += hb_astCountSendsOnExpr( pExpr->value.asMessage.pObject, szVar );
         n += hb_astCountSendsOnExpr( pExpr->value.asMessage.pParms, szVar );
         return n;
      case HB_ET_FUNCALL:
         return hb_astCountSendsOnExpr( pExpr->value.asFunCall.pParms, szVar );
      case HB_ET_ARRAYAT:
         return hb_astCountSendsOnExpr( pExpr->value.asList.pExprList, szVar ) +
                hb_astCountSendsOnExpr( pExpr->value.asList.pIndex, szVar );
      case HB_ET_LIST:
      case HB_ET_ARGLIST:
      case HB_ET_MACROARGLIST:
      case HB_ET_ARRAY:
      case HB_ET_HASH:
      case HB_ET_IIF:
      {
         PHB_EXPR pI = pExpr->value.asList.pExprList;
         for( ; pI; pI = pI->pNext )
            n += hb_astCountSendsOnExpr( pI, szVar );
         return n;
      }
      case HB_ET_CODEBLOCK:
      {
         PHB_EXPR pI = pExpr->value.asCodeblock.pExprList;
         for( ; pI; pI = pI->pNext )
            n += hb_astCountSendsOnExpr( pI, szVar );
         return n;
      }
      default:
         if( pExpr->ExprType >= HB_EO_POSTINC )
            return hb_astCountSendsOnExpr( pExpr->value.asOperator.pLeft, szVar ) +
                   hb_astCountSendsOnExpr( pExpr->value.asOperator.pRight, szVar );
         return 0;
   }
}

static int hb_astCountSendsOn( PHB_AST_NODE pBlock, const char * szVar )
{
   PHB_AST_NODE pStmt;
   int n = 0;
   if( ! pBlock || pBlock->type != HB_AST_BLOCK )
      return 0;
   for( pStmt = pBlock->value.asBlock.pFirst; pStmt; pStmt = pStmt->pNext )
   {
      switch( pStmt->type )
      {
         case HB_AST_EXPRSTMT:
            n += hb_astCountSendsOnExpr( pStmt->value.asExprStmt.pExpr, szVar );
            break;
         case HB_AST_RETURN:
            n += hb_astCountSendsOnExpr( pStmt->value.asReturn.pExpr, szVar );
            break;
         case HB_AST_QOUT:
         case HB_AST_QQOUT:
            n += hb_astCountSendsOnExpr( pStmt->value.asQOut.pExprList, szVar );
            break;
         case HB_AST_LOCAL:
         case HB_AST_STATIC:
         case HB_AST_PUBLIC:
         case HB_AST_PRIVATE:
            n += hb_astCountSendsOnExpr( pStmt->value.asVar.pInit, szVar );
            break;
         case HB_AST_IF:
            n += hb_astCountSendsOnExpr( pStmt->value.asIf.pCondition, szVar );
            n += hb_astCountSendsOn( pStmt->value.asIf.pThen, szVar );
            {
               PHB_AST_NODE pE = pStmt->value.asIf.pElseIfs;
               for( ; pE; pE = pE->pNext )
               {
                  n += hb_astCountSendsOnExpr( pE->value.asElseIf.pCondition, szVar );
                  n += hb_astCountSendsOn( pE->value.asElseIf.pBody, szVar );
               }
            }
            n += hb_astCountSendsOn( pStmt->value.asIf.pElse, szVar );
            break;
         case HB_AST_DOWHILE:
            n += hb_astCountSendsOnExpr( pStmt->value.asWhile.pCondition, szVar );
            n += hb_astCountSendsOn( pStmt->value.asWhile.pBody, szVar );
            break;
         case HB_AST_FOR:
            n += hb_astCountSendsOn( pStmt->value.asFor.pBody, szVar );
            break;
         case HB_AST_FOREACH:
            n += hb_astCountSendsOn( pStmt->value.asForEach.pBody, szVar );
            break;
         default:
            break;
      }
   }
   return n;
}

/* TYPED-VIEW audit: a branch guarded by `oVar:<disc> == <CONST>` whose
   body sends messages to the same dynamic (OBJECT/USUAL) oVar is a
   candidate for a branch-local typed view — `local o<Class>; 
   o<Class> := oVar` at the branch top gives compile-checked member
   access via the name-matches-class convention. Handles a bare EQ
   condition and drills into .OR. chains for the first EQ. */
static void hb_astAuditTypedView( PHB_EXPR pCond, PHB_AST_NODE pBody,
                                  HB_TYPEENV * pEnv, int iLine )
{
   PHB_EXPR pEq = pCond;
   PHB_EXPR pRecv, pRhs;
   const char * szVar;
   const char * szT;
   int nSends;

   if( ! hb_auditActive() || ! pBody )
      return;
   /* unwrap parens / drill OR chains */
   while( pEq )
   {
      if( ( pEq->ExprType == HB_ET_LIST || pEq->ExprType == HB_ET_ARGLIST ) &&
          pEq->value.asList.pExprList && ! pEq->value.asList.pExprList->pNext )
         pEq = pEq->value.asList.pExprList;
      else if( pEq->ExprType == HB_EO_OR || pEq->ExprType == HB_EO_AND )
         pEq = pEq->value.asOperator.pLeft;
      else
         break;
   }
   if( ! pEq || ( pEq->ExprType != HB_EO_EQ &&
                  pEq->ExprType != HB_EO_EQUAL ) )
      return;
   if( ! pEq->value.asOperator.pLeft ||
       pEq->value.asOperator.pLeft->ExprType != HB_ET_SEND )
      return;
   pRecv = pEq->value.asOperator.pLeft->value.asMessage.pObject;
   pRhs  = pEq->value.asOperator.pRight;
   if( ! pRecv || pRecv->ExprType != HB_ET_VARIABLE )
      return;
   szVar = pRecv->value.asSymbol.name;
   /* Sends on Self are typed by the enclosing class already — a
      branch-local view has nothing to add. */
   if( hb_stricmp( szVar, "Self" ) == 0 )
      return;
   szT = hb_astInferExprType( pRecv, pEnv );
   if( szT && hb_stricmp( szT, "OBJECT" ) != 0 &&
       hb_stricmp( szT, "USUAL" ) != 0 )
      return;   /* already concretely typed */
   nSends = hb_astCountSendsOn( pBody, szVar );
   if( nSends >= 2 )
   {
      char szDetail[ 160 ];
      const char * szDisc =
         pEq->value.asOperator.pLeft->value.asMessage.szMessage;
      const char * szConst =
         ( pRhs && pRhs->ExprType == HB_ET_VARIABLE )
            ? pRhs->value.asSymbol.name : "<const>";
      hb_snprintf( szDetail, sizeof( szDetail ),
         "branch on :%s == %s; %d dynamic sends to '%s' in body",
         szDisc ? szDisc : "?", szConst, nSends, szVar );
      hb_auditEmit( "TYPED-VIEW", pEnv->szFile, iLine, szVar, szDetail,
         "branch-local typed view: local o<Class> := var at branch top" );
   }
}

/* ================================================================
 * Hash key-type observation (Pass 2 companion)
 *
 * Walks every expression in the body looking for `h[idx]` subscripts
 * whose base variable the env currently types as weak HASH. A known
 * index type upgrades the binding: NUMERIC → HASHN, STRING → HASHC.
 * Statement coverage mirrors hb_astRefineBlock; runs inside the Pass 2
 * fixed-point loop so an upgrade feeds later inference (and further
 * upgrades) until stable. File statics resolve through the Hungarian
 * fallback and get their env binding created here — that only serves
 * intra-function consistency; the cross-function registry upgrade for
 * their declarations happens at emit (gencsharp observation pre-pass).
 * ================================================================ */
static void hb_astObserveExpr( PHB_EXPR pExpr, HB_TYPEENV * pEnv,
                               HB_BOOL * pfChanged )
{
   if( ! pExpr )
      return;

   switch( pExpr->ExprType )
   {
      case HB_ET_ARRAYAT:
      {
         PHB_EXPR pBase = pExpr->value.asList.pExprList;
         PHB_EXPR pIdx  = pExpr->value.asList.pIndex;
         if( pBase && pBase->ExprType == HB_ET_VARIABLE && pIdx )
         {
            const char * szBase =
               hb_astInferExprType( pBase, pEnv );
            if( szBase && strcmp( szBase, "HASH" ) == 0 )
            {
               const char * szIdx = hb_astInferExprType( pIdx, pEnv );
               const char * szKeyed = NULL;
               if( szIdx && strcmp( szIdx, "NUMERIC" ) == 0 )
                  szKeyed = "HASHN";
               else if( szIdx && strcmp( szIdx, "STRING" ) == 0 )
                  szKeyed = "HASHC";
               if( szKeyed &&
                   hb_typeEnvSet( pEnv, pBase->value.asSymbol.name,
                                  szKeyed ) )
                  *pfChanged = HB_TRUE;
            }
         }
         /* A NUMERIC variable in index position is an int-emission
            candidate (subscript of array OR hash). */
         if( pIdx && pIdx->ExprType == HB_ET_VARIABLE )
         {
            const char * szIdxT = hb_astInferExprType( pIdx, pEnv );
            if( szIdxT && ( strcmp( szIdxT, "NUMERIC" ) == 0 ||
                            strcmp( szIdxT, "INTEGER" ) == 0 ) )
               hb_auditIntMark( pIdx->value.asSymbol.name,
                                s_iObserveLine, HB_INT_MARK_INDEX );
         }
         hb_astObserveExpr( pBase, pEnv, pfChanged );
         hb_astObserveExpr( pIdx,  pEnv, pfChanged );
         break;
      }

      case HB_ET_FUNCALL:
         hb_astObserveExpr( pExpr->value.asFunCall.pParms, pEnv, pfChanged );
         break;

      case HB_ET_SEND:
         /* W0032 — a message send on a variable whose name claims a
            scalar type (aLine:nType — arrays don't receive sends) is
            a naming-contract violation: the variable actually holds
            an object and its stale Hungarian poisons inference at
            every call site it feeds. Near-zero false positives.
            Promoted from the NAME-CONTRACT audit category (the W0021
            graduation pattern): every instance has a definite fix —
            rename to o<...>. The audit row remains as the structured
            mirror. */
         if( pExpr->value.asMessage.pObject &&
             pExpr->value.asMessage.pObject->ExprType == HB_ET_VARIABLE &&
             pExpr->value.asMessage.szMessage )
         {
            PHB_EXPR pRecv = pExpr->value.asMessage.pObject;
            const char * szT = hb_astInferExprType( pRecv, pEnv );
            /* __enum* are Harbour's FOR-EACH enumerator messages —
               valid on any iteration variable, not a naming clue. */
            if( hb_astIsScalarTag( szT ) &&
                strncmp( pExpr->value.asMessage.szMessage, "__enum", 6 )
                   != 0 )
            {
               char szDetail[ 160 ];
               hb_snprintf( szDetail, sizeof( szDetail ),
                  "name claims %s but receives send ':%s' — holds an "
                  "object", szT, pExpr->value.asMessage.szMessage );
               if( ! hb_astOrmSeen( s_iObserveLine,
                                    pRecv->value.asSymbol.name ) )
                  fprintf( stderr,
                     "hbtranspiler: %s(%d): warning W0032  "
                     "'%s' %s — rename to o<...> (soft-typing "
                     "contract)\n",
                     pEnv->szFile ? hb_strCollapsePath( pEnv->szFile )
                                  : "?",
                     s_iObserveLine, pRecv->value.asSymbol.name,
                     szDetail );
               hb_auditEmit( "NAME-CONTRACT", pEnv->szFile,
                  s_iObserveLine, pRecv->value.asSymbol.name, szDetail,
                  "rename to o<...> (soft-typing contract)" );
            }
         }
         hb_astObserveExpr( pExpr->value.asMessage.pObject, pEnv, pfChanged );
         hb_astObserveExpr( pExpr->value.asMessage.pParms,  pEnv, pfChanged );
         break;

      case HB_ET_VARREF:
         /* Passed by @: the callee slot merges toward decimal across
            callers, and C# `ref` demands exact type identity — an
            int local here would CS1620. Soft-disqualify. */
         hb_auditIntMark( pExpr->value.asSymbol.name,
                          s_iObserveLine, HB_INT_MARK_SOFT );
         break;

      case HB_ET_REFERENCE:
         if( pExpr->value.asReference &&
             pExpr->value.asReference->ExprType == HB_ET_VARIABLE )
            hb_auditIntMark( pExpr->value.asReference->value.asSymbol.name,
                             s_iObserveLine, HB_INT_MARK_SOFT );
         hb_astObserveExpr( pExpr->value.asReference, pEnv, pfChanged );
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
            hb_astObserveExpr( p, pEnv, pfChanged );
            p = p->pNext;
         }
         break;
      }

      case HB_ET_CODEBLOCK:
      {
         PHB_EXPR p = pExpr->value.asCodeblock.pExprList;
         while( p )
         {
            hb_astObserveExpr( p, pEnv, pfChanged );
            p = p->pNext;
         }
         break;
      }

      default:
         if( pExpr->ExprType >= HB_EO_POSTINC )
         {
            /* Division is THE hard int-emission disqualifier —
               Harbour n/2 is 1.5, C# int/int truncates. Either
               VARIABLE operand of a division loses candidacy (hard —
               W0026 when index-used). A fractional-literal assignment
               is hard too. Any other assignment whose RHS isn't
               provably integral is a soft disqualifier: the variable
               stays decimal but only the audit reports it. */
            if( pExpr->ExprType == HB_EO_DIV ||
                pExpr->ExprType == HB_EO_DIVEQ )
            {
               PHB_EXPR pSide = pExpr->value.asOperator.pLeft;
               int k;
               for( k = 0; k < 2; k++,
                    pSide = pExpr->value.asOperator.pRight )
                  if( pSide && pSide->ExprType == HB_ET_VARIABLE )
                  {
                     const char * szT =
                        hb_astInferExprType( pSide, pEnv );
                     if( szT && ( strcmp( szT, "NUMERIC" ) == 0 ||
                                  strcmp( szT, "INTEGER" ) == 0 ) )
                        hb_auditIntMark( pSide->value.asSymbol.name,
                                         s_iObserveLine,
                                         HB_INT_MARK_HARD );
                  }
            }
            else if( ( pExpr->ExprType == HB_EO_ASSIGN ||
                       pExpr->ExprType == HB_EO_PLUSEQ ||
                       pExpr->ExprType == HB_EO_MINUSEQ ||
                       pExpr->ExprType == HB_EO_MULTEQ ) &&
                     pExpr->value.asOperator.pLeft &&
                     pExpr->value.asOperator.pLeft->ExprType ==
                        HB_ET_VARIABLE &&
                     pExpr->value.asOperator.pRight )
            {
               PHB_EXPR pRhs = pExpr->value.asOperator.pRight;
               const char * szLhs =
                  pExpr->value.asOperator.pLeft->value.asSymbol.name;
               if( ( pRhs->ExprType == HB_ET_NUMERIC &&
                     pRhs->value.asNum.NumType != HB_ET_LONG ) ||
                   pRhs->ExprType == HB_EO_DIV )
                  /* Fractional literal or a division result — the
                     "looks like an int but can't be" shape that
                     earns W0026 when index-used. */
                  hb_auditIntMark( szLhs, s_iObserveLine,
                                   HB_INT_MARK_HARD );
               else if( ! hb_astExprIsIntegral( pRhs, pEnv, szLhs ) )
                  hb_auditIntMark( szLhs, s_iObserveLine,
                                   HB_INT_MARK_SOFT );
            }
            else if( ( pExpr->ExprType == HB_EO_MODEQ ||
                       pExpr->ExprType == HB_EO_DIVEQ ||
                       pExpr->ExprType == HB_EO_EXPEQ ) &&
                     pExpr->value.asOperator.pLeft &&
                     pExpr->value.asOperator.pLeft->ExprType ==
                        HB_ET_VARIABLE )
               hb_auditIntMark(
                  pExpr->value.asOperator.pLeft->value.asSymbol.name,
                  s_iObserveLine, HB_INT_MARK_HARD );
            hb_astObserveExpr( pExpr->value.asOperator.pLeft,  pEnv, pfChanged );
            hb_astObserveExpr( pExpr->value.asOperator.pRight, pEnv, pfChanged );
         }
         break;
   }
}

static void hb_astObserveBlock( PHB_AST_NODE pNode, HB_TYPEENV * pEnv,
                                HB_BOOL * pfChanged )
{
   PHB_AST_NODE pStmt;

   if( ! pNode || pNode->type != HB_AST_BLOCK )
      return;

   pStmt = pNode->value.asBlock.pFirst;
   while( pStmt )
   {
      s_iObserveLine = pStmt->iLine;
      switch( pStmt->type )
      {
         case HB_AST_EXPRSTMT:
            hb_astObserveExpr( pStmt->value.asExprStmt.pExpr, pEnv, pfChanged );
            break;
         case HB_AST_RETURN:
            hb_astObserveExpr( pStmt->value.asReturn.pExpr, pEnv, pfChanged );
            break;
         case HB_AST_QOUT:
         case HB_AST_QQOUT:
            hb_astObserveExpr( pStmt->value.asQOut.pExprList, pEnv, pfChanged );
            break;
         case HB_AST_LOCAL:
         case HB_AST_STATIC:
         case HB_AST_PUBLIC:
         case HB_AST_PRIVATE:
            /* A declaration initializer is an assignment for
               int-candidacy purposes: fractional literal / division
               result = hard disqualifier, other non-integral = soft. */
            if( pStmt->value.asVar.szName && pStmt->value.asVar.pInit &&
                ! pStmt->value.asVar.fArrayDim )
            {
               PHB_EXPR pInit = pStmt->value.asVar.pInit;
               if( ( pInit->ExprType == HB_ET_NUMERIC &&
                     pInit->value.asNum.NumType != HB_ET_LONG ) ||
                   pInit->ExprType == HB_EO_DIV )
                  hb_auditIntMark( pStmt->value.asVar.szName,
                                   pStmt->iLine, HB_INT_MARK_HARD );
               else if( ! hb_astExprIsIntegral( pInit, pEnv,
                                                pStmt->value.asVar.szName ) )
                  hb_auditIntMark( pStmt->value.asVar.szName,
                                   pStmt->iLine, HB_INT_MARK_SOFT );
            }
            hb_astObserveExpr( pStmt->value.asVar.pInit, pEnv, pfChanged );
            break;
         case HB_AST_IF:
            hb_astObserveExpr( pStmt->value.asIf.pCondition, pEnv, pfChanged );
            hb_astAuditTypedView( pStmt->value.asIf.pCondition,
                                  pStmt->value.asIf.pThen, pEnv,
                                  pStmt->iLine );
            hb_astObserveBlock( pStmt->value.asIf.pThen, pEnv, pfChanged );
            {
               PHB_AST_NODE p = pStmt->value.asIf.pElseIfs;
               while( p )
               {
                  hb_astObserveExpr( p->value.asElseIf.pCondition, pEnv, pfChanged );
                  hb_astAuditTypedView( p->value.asElseIf.pCondition,
                                        p->value.asElseIf.pBody, pEnv,
                                        p->iLine );
                  hb_astObserveBlock( p->value.asElseIf.pBody, pEnv, pfChanged );
                  p = p->pNext;
               }
            }
            hb_astObserveBlock( pStmt->value.asIf.pElse, pEnv, pfChanged );
            break;
         case HB_AST_DOWHILE:
            hb_astObserveExpr( pStmt->value.asWhile.pCondition, pEnv, pfChanged );
            hb_astObserveBlock( pStmt->value.asWhile.pBody, pEnv, pfChanged );
            break;
         case HB_AST_FOR:
            /* FOR variables are prime int candidates: C# emits
               `for (int i = <start>; ...)`, so the START must be
               provably integral (the end bound only appears in a
               comparison, where int widens to decimal for free); a
               fractional STEP is a hard disqualifier. */
            if( pStmt->value.asFor.szVar )
            {
               PHB_EXPR pStep = pStmt->value.asFor.pStep;
               HB_BOOL fFracStep = pStep &&
                  pStep->ExprType == HB_ET_NUMERIC &&
                  pStep->value.asNum.NumType != HB_ET_LONG;
               hb_auditIntMark( pStmt->value.asFor.szVar, pStmt->iLine,
                                HB_INT_MARK_INDEX );
               if( fFracStep )
                  hb_auditIntMark( pStmt->value.asFor.szVar,
                                   pStmt->iLine, HB_INT_MARK_HARD );
               else if( ! hb_astExprIsIntegral( pStmt->value.asFor.pStart,
                                                pEnv, NULL ) )
                  hb_auditIntMark( pStmt->value.asFor.szVar,
                                   pStmt->iLine, HB_INT_MARK_SOFT );
            }
            hb_astObserveExpr( pStmt->value.asFor.pStart, pEnv, pfChanged );
            hb_astObserveExpr( pStmt->value.asFor.pEnd,   pEnv, pfChanged );
            hb_astObserveExpr( pStmt->value.asFor.pStep,  pEnv, pfChanged );
            hb_astObserveBlock( pStmt->value.asFor.pBody, pEnv, pfChanged );
            break;
         case HB_AST_FOREACH:
            hb_astObserveExpr( pStmt->value.asForEach.pEnum, pEnv, pfChanged );
            hb_astObserveBlock( pStmt->value.asForEach.pBody, pEnv, pfChanged );
            break;
         case HB_AST_DOCASE:
         {
            PHB_AST_NODE p = pStmt->value.asDoCase.pCases;
            while( p )
            {
               hb_astObserveExpr( p->value.asCase.pCondition, pEnv, pfChanged );
               hb_astObserveBlock( p->value.asCase.pBody, pEnv, pfChanged );
               p = p->pNext;
            }
            hb_astObserveBlock( pStmt->value.asDoCase.pOtherwise, pEnv, pfChanged );
            break;
         }
         case HB_AST_SWITCH:
         {
            PHB_AST_NODE p = pStmt->value.asSwitch.pCases;
            /* A SWITCH subject is a soft int-disqualifier: C# demands
               case-label/subject type identity, and case labels here
               are routinely `const decimal` defines — switch(int) on
               them is CS0266 per label. A variable compared against
               constants isn't index-shaped anyway. */
            if( pStmt->value.asSwitch.pSwitch &&
                pStmt->value.asSwitch.pSwitch->ExprType == HB_ET_VARIABLE )
               hb_auditIntMark(
                  pStmt->value.asSwitch.pSwitch->value.asSymbol.name,
                  pStmt->iLine, HB_INT_MARK_SOFT );
            hb_astObserveExpr( pStmt->value.asSwitch.pSwitch, pEnv, pfChanged );
            while( p )
            {
               hb_astObserveExpr( p->value.asCase.pCondition, pEnv, pfChanged );
               hb_astObserveBlock( p->value.asCase.pBody, pEnv, pfChanged );
               p = p->pNext;
            }
            hb_astObserveBlock( pStmt->value.asSwitch.pDefault, pEnv, pfChanged );
            break;
         }
         case HB_AST_BEGINSEQ:
            hb_astObserveBlock( pStmt->value.asSeq.pBody, pEnv, pfChanged );
            hb_astObserveBlock( pStmt->value.asSeq.pRecover, pEnv, pfChanged );
            hb_astObserveBlock( pStmt->value.asSeq.pAlways, pEnv, pfChanged );
            break;
         case HB_AST_WITHOBJECT:
            hb_astObserveExpr( pStmt->value.asWithObj.pObject, pEnv, pfChanged );
            hb_astObserveBlock( pStmt->value.asWithObj.pBody, pEnv, pfChanged );
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
               /* Multiple RETURN types disagree — compatible if both
                  numeric, or both in the HASH key-type family (the
                  weak HASH yields to a key-typed HASHC/HASHN branch;
                  HASHC vs HASHN merges to NULL and conflicts). */
               const char * szHashMerge =
                  hb_astHashFamilyMerge( *pszRetType, szType );
               HB_BOOL fNumA =
                  strcmp( *pszRetType, "NUMERIC" ) == 0 ||
                  strcmp( *pszRetType, "INTEGER" ) == 0;
               HB_BOOL fNumB =
                  strcmp( szType, "NUMERIC" ) == 0 ||
                  strcmp( szType, "INTEGER" ) == 0;
               if( fNumA && fNumB )
                  /* Numeric family: INTEGER is a refinement of
                     NUMERIC; mixed returns widen quietly. */
                  *pszRetType = "NUMERIC";
               else if( szHashMerge )
                  *pszRetType = szHashMerge;
               else
               {
                  /* Related classes merge to their nearest common
                     ancestor (Transaction vs OldTransaction returns);
                     unrelated types conflict as before. */
                  const char * szAnc = NULL;
                  if( s_pPropRefTab )
                  {
                     const char * szUp = szType;
                     int iDepth;
                     for( iDepth = 0; szUp && iDepth < 16; iDepth++ )
                     {
                        if( hb_refTabIsKindOf( s_pPropRefTab,
                                               *pszRetType, szUp ) )
                        {
                           szAnc = szUp;
                           break;
                        }
                        szUp = hb_refTabClassParent( s_pPropRefTab, szUp );
                     }
                  }
                  if( szAnc )
                     *pszRetType = szAnc;
                  else
                     *pfConflict = HB_TRUE;
               }
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
               {
                  char szSym[ 192 ];
                  hb_snprintf( szSym, sizeof( szSym ), "%s:%s",
                               szCallee, szPName );
                  hb_auditEmit( "W0022-USUAL", pEnv->szFile, iLine, szSym,
                     szArgType ? szArgType : "conflicting call-site types",
                     "reconcile call-site types or split the parameter" );
               }
            }
         }
      }
      pArg = pArg->pNext;
      iPos++;
   }
}

/* ---- ORM def-class contract checks (W0028..W0031) ----
   With --fieldtypes loaded, a def-class receiver's members are a
   closed, fully-typed contract, so violations can be surfaced at SCAN
   time (-GF: warnings.txt + --type-audit) instead of hours later as
   CS1061/CS0266/CS0037/CS0029 in the dotnet build. That keeps the
   scan phase alone viable as a CI contract-checker for the source
   branch. Non-halting; dedup'd across the scan+emit passes (same
   pattern as W0024). */
#define HB_ORM_DEDUP 4096
static struct { int iLine; char szKey[ 96 ]; } s_aOrmWarned[ HB_ORM_DEDUP ];
static int s_iOrmWarned = 0;

static HB_BOOL hb_astOrmSeen( int iLine, const char * szKey )
{
   int i;

   for( i = 0; i < s_iOrmWarned; i++ )
      if( s_aOrmWarned[ i ].iLine == iLine &&
          strcmp( s_aOrmWarned[ i ].szKey, szKey ) == 0 )
         return HB_TRUE;
   if( s_iOrmWarned < HB_ORM_DEDUP )
   {
      s_aOrmWarned[ s_iOrmWarned ].iLine = iLine;
      hb_strncpy( s_aOrmWarned[ s_iOrmWarned ].szKey, szKey,
                  sizeof( s_aOrmWarned[ 0 ].szKey ) - 1 );
      s_iOrmWarned++;
   }
   return HB_FALSE;
}

/* The def-class name when pRecv is a VARIABLE env-typed as a mapped
   ORM class, else NULL. Variable receivers only — same cheap, non-
   recursive rule as the inference hook. */
static const char * hb_astOrmRecvClass( PHB_EXPR pRecv, HB_TYPEENV * pEnv )
{
   const char * szT;

   if( ! pRecv || pRecv->ExprType != HB_ET_VARIABLE )
      return NULL;
   szT = hb_typeEnvGet( pEnv, pRecv->value.asSymbol.name );
   return ( szT && hb_fieldTypesClassCanon( szT ) ) ? szT : NULL;
}

/* W0028 — a member that is not in the def class's field set (or the
   OrmTable base surface), or one written with non-canonical casing.
   The former never persisted at runtime in Harbour either (the shHash
   only scatters/gathers contract fields) and is a CS1061 in C#; the
   latter runs fine in case-insensitive Harbour but not in C#. */
static void hb_astOrmMemberCheck( PHB_EXPR pSend, HB_TYPEENV * pEnv,
                                  int iLine )
{
   const char * szClass;
   const char * szMsg;
   const char * szCanon = NULL;

   if( ! pSend || pSend->ExprType != HB_ET_SEND ||
       ! pSend->value.asMessage.szMessage )
      return;
   szClass = hb_astOrmRecvClass( pSend->value.asMessage.pObject, pEnv );
   if( ! szClass )
      return;
   szMsg = pSend->value.asMessage.szMessage;

   if( hb_fieldTypesMember( szClass, szMsg, &szCanon ) ||
       hb_fieldTypesMember( "OrmTable", szMsg, &szCanon ) )
   {
      if( szCanon && strcmp( szCanon, szMsg ) != 0 &&
          ! hb_astOrmSeen( iLine, szMsg ) )
      {
         fprintf( stderr,
            "hbtranspiler: %s(%d): warning W0028  "
            "'%s:%s' — the def contract spells it '%s' "
            "(C# is case-sensitive)\n",
            pEnv->szFile ? hb_strCollapsePath( pEnv->szFile ) : "?", iLine,
            szClass, szMsg, szCanon );
         hb_auditEmit( "ORM-MEMBER", pEnv->szFile, iLine, szMsg,
                       "case mismatch vs def contract", szCanon );
      }
   }
   else if( ! hb_astOrmSeen( iLine, szMsg ) )
   {
      fprintf( stderr,
         "hbtranspiler: %s(%d): warning W0028  "
         "'%s:%s' — no such field in the %s def contract "
         "(never persists at runtime; CS1061 in C#)\n",
         pEnv->szFile ? hb_strCollapsePath( pEnv->szFile ) : "?", iLine,
         szClass, szMsg, szClass );
      hb_auditEmit( "ORM-MEMBER", pEnv->szFile, iLine, szMsg,
                    "member not in def contract", szClass );
   }
}

/* Typed-field write checks:
     W0029 — decimal-shaped RHS (or a /= ^= %= compound) into an
             int/long field: CS0266 at build, silent truncation risk
             in Harbour.
     W0030 — NIL into a value-typed field: CS0037; a DBF field can't
             hold NIL either.
     W0031 — cross-kind RHS (STRING into a numeric field, ...): CS0029;
             the same class of 9.0 bug W0024 catches for locals, but
             against the def contract instead of the Hungarian prefix. */
static void hb_astOrmAssignCheck( PHB_EXPR pExpr, HB_TYPEENV * pEnv,
                                  int iLine )
{
   PHB_EXPR pLhs = pExpr->value.asOperator.pLeft;
   PHB_EXPR pRhs = pExpr->value.asOperator.pRight;
   const char * szClass;
   const char * szCs;
   const char * szRhsT;
   const char * szKind;
   char szSym[ 128 ];

   if( ! pLhs || pLhs->ExprType != HB_ET_SEND ||
       ! pLhs->value.asMessage.szMessage || ! pRhs )
      return;
   szClass = hb_astOrmRecvClass( pLhs->value.asMessage.pObject, pEnv );
   if( ! szClass )
      return;
   szCs = hb_fieldTypesMember( szClass, pLhs->value.asMessage.szMessage,
                               NULL );
   if( ! szCs || strcmp( szCs, "method" ) == 0 )
      return;
   hb_snprintf( szSym, sizeof( szSym ), "%s:%s", szClass,
                pLhs->value.asMessage.szMessage );

   if( pRhs->ExprType == HB_ET_NIL )
   {
      if( strcmp( szCs, "string" ) != 0 &&
          ! hb_astOrmSeen( iLine, szSym ) )
      {
         fprintf( stderr,
            "hbtranspiler: %s(%d): warning W0030  "
            "NIL assigned into value-typed field '%s' (%s) — "
            "a table field can't hold NIL (CS0037 in C#)\n",
            pEnv->szFile ? hb_strCollapsePath( pEnv->szFile ) : "?", iLine, szSym, szCs );
         hb_auditEmit( "ORM-NIL", pEnv->szFile, iLine, szSym,
                       "NIL into value-typed field", szCs );
      }
      return;
   }

   /* /=, %=, ^= produce fractional/decimal results in Harbour no
      matter the operand types — always a narrowing write on an
      int/long field. */
   if( ( strcmp( szCs, "int" ) == 0 || strcmp( szCs, "long" ) == 0 ) &&
       ( pExpr->ExprType == HB_EO_DIVEQ ||
         pExpr->ExprType == HB_EO_MODEQ ||
         pExpr->ExprType == HB_EO_EXPEQ ) )
   {
      if( ! hb_astOrmSeen( iLine, szSym ) )
      {
         fprintf( stderr,
            "hbtranspiler: %s(%d): warning W0029  "
            "compound /=/%%=/^= on integer field '%s' — Harbour "
            "division is float; result won't fit the %s field "
            "(CS0266 in C#)\n",
            pEnv->szFile ? hb_strCollapsePath( pEnv->szFile ) : "?", iLine, szSym, szCs );
         hb_auditEmit( "ORM-NARROW", pEnv->szFile, iLine, szSym,
                       "float compound-assign on int/long field", szCs );
      }
      return;
   }

   szRhsT = hb_astInferExprType( pRhs, pEnv );
   if( ! szRhsT || strcmp( szRhsT, "USUAL" ) == 0 ||
       strcmp( szRhsT, "OBJECT" ) == 0 )
      return;   /* unknown/dynamic RHS — nothing provable */

   if( ( strcmp( szCs, "int" ) == 0 || strcmp( szCs, "long" ) == 0 ) &&
       strcmp( szRhsT, "NUMERIC" ) == 0 )
   {
      if( ! hb_astOrmSeen( iLine, szSym ) )
      {
         fprintf( stderr,
            "hbtranspiler: %s(%d): warning W0029  "
            "decimal-shaped value written into %s field '%s' — "
            "wrap with Int() or retype the field (CS0266 in C#)\n",
            pEnv->szFile ? hb_strCollapsePath( pEnv->szFile ) : "?", iLine, szCs, szSym );
         hb_auditEmit( "ORM-NARROW", pEnv->szFile, iLine, szSym,
                       "decimal-shaped RHS into int/long field", szCs );
      }
      return;
   }

   szKind = NULL;
   if( strcmp( szCs, "int" ) == 0 || strcmp( szCs, "long" ) == 0 ||
       strcmp( szCs, "decimal" ) == 0 )
      szKind = "NUMERIC";
   else if( strcmp( szCs, "string" ) == 0 )
      szKind = "STRING";
   else if( strcmp( szCs, "bool" ) == 0 )
      szKind = "LOGICAL";
   else if( strcmp( szCs, "date" ) == 0 )
      szKind = "DATE";
   else if( strcmp( szCs, "timestamp" ) == 0 )
      szKind = "TIMESTAMP";

   if( szKind && strcmp( szRhsT, szKind ) != 0 &&
       ! ( strcmp( szKind, "NUMERIC" ) == 0 &&
           strcmp( szRhsT, "INTEGER" ) == 0 ) &&
       ! hb_astOrmSeen( iLine, szSym ) )
   {
      fprintf( stderr,
         "hbtranspiler: %s(%d): warning W0031  "
         "%s value written into '%s' — the def contract says %s "
         "(CS0029 in C#)\n",
         pEnv->szFile ? hb_strCollapsePath( pEnv->szFile ) : "?",
         iLine, szRhsT, szSym,
         szKind );
      hb_auditEmit( "ORM-TYPE", pEnv->szFile, iLine, szSym,
                    szRhsT, szKind );
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
         const char * szRecvName = NULL;
         char szKey[ 256 ];
         const char * szLookup = NULL;

         /* ORM def-class member contract (W0028) — scan-phase
            surfacing of what would otherwise be a CS1061. */
         hb_astOrmMemberCheck( pExpr, pEnv, iLine );

         if( pExpr->value.asMessage.pObject )
         {
            PHB_EXPR pRecv = pExpr->value.asMessage.pObject;
            szRecvType = hb_astInferExprType( pRecv, pEnv );
            /* Member-access receivers only (Self:member / obj:member):
               a class DATA slot's type routinely comes from nothing
               but its own name. Plain variable receivers stay out of
               this check — locals/params named after their class are
               the dominant easipos calling convention and usually ARE
               that class; skipping them lost legitimate refinement
               AND by-ref marking file-wide (904 → 1305 errors, 487
               of them CS1615 ref mismatches, when this guard was
               receiver-shape-agnostic). */
            if( pRecv->ExprType == HB_ET_SEND )
               szRecvName = pRecv->value.asMessage.szMessage;
         }

         /* A member whose class derives purely from its NAME
            (`o<ClassName>` — hb_astIsNameSeededClass) is a weak hint,
            not evidence. Refining through it misattributes call-site
            types: the CdS COM wrappers hold their ActiveX proxy in a
            member named after the wrapper class itself (EasiCdS's
            ::oEasiCdS), so the proxy's marshaling calls
            (`::oEasiCdS:NewTransaction(ToString(nClerkId), ...)`)
            were keyed onto the wrapper's OWN methods — spurious
            W0022s, wrongful downgrade-to-USUAL of every conflicting
            parameter, and stray by-ref marks from the proxy's @args.
            Under-refining is safer than mis-refining.

            The name-matches-class convention is the project's soft
            typing contract, though — so before discarding, check
            whether the call CONTRADICTS the claimed class: more args
            than the class method declares, or a scalar arg against a
            different-scalar slot. That shape means the member is
            almost certainly NOT an instance of the class its name
            claims (a COM proxy named after its wrapper) and the
            member should be renamed — W0025. */
         if( szRecvType && szRecvName &&
             hb_astIsNameSeededClass( szRecvName, szRecvType ) )
         {
            if( szMethod && pEnv->pRefTab )
            {
               char szProbe[ 256 ];
               int  nParams;
               hb_snprintf( szProbe, sizeof( szProbe ), "%s::%s__%s",
                            szRecvType, szRecvType, szMethod );
               nParams = hb_refTabParamCount( pEnv->pRefTab, szProbe );
               if( nParams > 0 )
               {
                  PHB_EXPR pParms = pExpr->value.asMessage.pParms;
                  PHB_EXPR pArg = NULL;
                  int  iArgs = 0, iPos = 0;
                  HB_BOOL fContradicts = HB_FALSE;
                  if( pParms && ( pParms->ExprType == HB_ET_LIST ||
                                  pParms->ExprType == HB_ET_ARGLIST ||
                                  pParms->ExprType == HB_ET_MACROARGLIST ) )
                     pArg = pParms->value.asList.pExprList;
                  else
                     pArg = pParms;
                  for( ; pArg; pArg = pArg->pNext )
                  {
                     if( pArg->ExprType != HB_ET_NONE )
                     {
                        if( ! fContradicts && iPos < nParams )
                        {
                           const HB_REFPARAM * pP = hb_refTabParam(
                              pEnv->pRefTab, szProbe, iPos );
                           const char * szArgT =
                              hb_astInferExprType( pArg, pEnv );
                           if( pP && pP->szType && szArgT &&
                               hb_astIsScalarTag( pP->szType ) &&
                               hb_astIsScalarTag( szArgT ) &&
                               hb_stricmp( pP->szType, szArgT ) != 0 &&
                               ! hb_astHashFamilyMerge( pP->szType, szArgT ) &&
                               ! ( hb_stricmp( pP->szType, "TIMESTAMP" ) == 0 &&
                                   hb_stricmp( szArgT, "DATE" ) == 0 ) &&
                               ! ( hb_stricmp( pP->szType, "DATE" ) == 0 &&
                                   hb_stricmp( szArgT, "TIMESTAMP" ) == 0 ) )
                              fContradicts = HB_TRUE;
                        }
                        iArgs++;
                     }
                     iPos++;
                  }
                  if( iArgs > nParams )
                     fContradicts = HB_TRUE;
                  if( fContradicts &&
                      ! hb_astHungSeen( iLine, szRecvName ) )
                     fprintf( stderr,
                        "hbtranspiler: %s(%d): warning W0025  "
                        "member '%s' is name-typed as class %s but its "
                        "call to '%s' doesn't match %s:%s — receiver is "
                        "probably not a %s; rename the member\n",
                        pEnv->szFile ?
                           hb_strCollapsePath( pEnv->szFile ) : "?",
                        iLine, szRecvName, szRecvType, szMethod,
                        szRecvType, szMethod, szRecvType );
               }
            }
            szRecvType = NULL;
         }

         if( szRecvType && szMethod )
         {
            /* Reject the standard non-class type tags. Anything that
               survives is treated as a class name. */
            if( hb_stricmp( szRecvType, "USUAL"   ) != 0 &&
                hb_stricmp( szRecvType, "NUMERIC" ) != 0 &&
                hb_stricmp( szRecvType, "INTEGER" ) != 0 &&
                hb_stricmp( szRecvType, "STRING"  ) != 0 &&
                hb_stricmp( szRecvType, "LOGICAL" ) != 0 &&
                hb_stricmp( szRecvType, "DATE"    ) != 0 &&
                ! hb_astIsHashFamily( szRecvType )       &&
                hb_stricmp( szRecvType, "ARRAY"   ) != 0 &&
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

      case HB_EO_ASSIGN:
      case HB_EO_PLUSEQ:
      case HB_EO_MINUSEQ:
      case HB_EO_MULTEQ:
      case HB_EO_DIVEQ:
      case HB_EO_MODEQ:
      case HB_EO_EXPEQ:
         /* Typed-field write checks (W0029/W0030/W0031) — scan-phase
            surfacing of CS0266/CS0037/CS0029 at ORM field writes. */
         hb_astOrmAssignCheck( pExpr, pEnv, iLine );
         hb_astRefineExpr( pExpr->value.asOperator.pLeft,  pEnv, iLine );
         hb_astRefineExpr( pExpr->value.asOperator.pRight, pEnv, iLine );
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
   /* `h<X>` commits to hash-ness only — a key-typed HASHC/HASHN RHS
      against the prefix's plain HASH is agreement, not a W0024. Same
      for the numeric family: INTEGER is a refinement of NUMERIC. */
   if( hb_astHashFamilyMerge( szLhs, szRhs ) )
      return;
   if( ( hb_stricmp( szLhs, "NUMERIC" ) == 0 ||
         hb_stricmp( szLhs, "INTEGER" ) == 0 ) &&
       ( hb_stricmp( szRhs, "NUMERIC" ) == 0 ||
         hb_stricmp( szRhs, "INTEGER" ) == 0 ) )
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

   s_iIntCand = 0;   /* per-function int-candidate audit tracking */

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

   /* Pass 2: Walk assignments and propagate (iterate until stable).
      The hash-key observation walker runs in the same fixed point so
      a subscript-derived HASHN/HASHC upgrade feeds later assignment
      inference and vice versa. */
   do
   {
      fChanged = HB_FALSE;
      hb_astPropagateBlock( pBody, &env, &fChanged );
      hb_astObserveBlock( pBody, &env, &fChanged );
   }
   while( fChanged );

   /* Pass 2.5: apply int candidacy. A LOCAL/STATIC whose numeric life
      is purely index-shaped upgrades NUMERIC → INTEGER (C# int; int
      widens to decimal implicitly so every consumer boundary is
      free). Restricted to variables DECLARED in this body — a
      parameter's C# type comes from its reftab slot, which merges
      toward decimal across callers, and retyping only the env side
      would split the signature from the body. Hard-disqualified
      index users (division / fractional value) get W0026 at the
      disqualifying site instead of a silent demotion. */
   {
      int i;
      for( i = 0; i < s_iIntCand; i++ )
      {
         if( ! s_aIntCand[ i ].fIndexUsed )
            continue;
         if( s_aIntCand[ i ].fNonIntHard )
         {
            if( ! hb_astHungSeen( s_aIntCand[ i ].iNonIntLine,
                                  s_aIntCand[ i ].szName ) )
               fprintf( stderr,
                  "hbtranspiler: %s(%d): warning W0026  "
                  "'%s' is used as an array/hash index but a division "
                  "or fractional value here forces it to stay decimal "
                  "— wrap the expression with int() or keep decimal\n",
                  hb_strCollapsePath( szFile ? szFile : "?" ),
                  s_aIntCand[ i ].iNonIntLine,
                  s_aIntCand[ i ].szName );
         }
         else if( ! s_aIntCand[ i ].fNonIntSoft )
         {
            PHB_AST_NODE pDecl = pBody->value.asBlock.pFirst;
            while( pDecl )
            {
               if( ( pDecl->type == HB_AST_LOCAL ||
                     pDecl->type == HB_AST_STATIC ) &&
                   pDecl->value.asVar.szName &&
                   hb_stricmp( pDecl->value.asVar.szName,
                               s_aIntCand[ i ].szName ) == 0 )
               {
                  const char * szCur = hb_typeEnvGet(
                     &env, s_aIntCand[ i ].szName );
                  if( szCur && strcmp( szCur, "NUMERIC" ) == 0 )
                     hb_typeEnvSet( &env, s_aIntCand[ i ].szName,
                                    "INTEGER" );
                  break;
               }
               pDecl = pDecl->pNext;
            }
         }
      }
   }

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
         /* Weak HASH upgraded to a key-typed HASHC/HASHN by the RHS
            or the subscript observations in Pass 2. */
         else if( strcmp( szCurType, "HASH" ) == 0 &&
                  szPropType && strcmp( szPropType, "HASH" ) != 0 &&
                  hb_astIsHashFamily( szPropType ) )
            fOverride = HB_TRUE;
         /* NUMERIC upgraded to INTEGER by Pass 2.5 index candidacy. */
         else if( strcmp( szCurType, "NUMERIC" ) == 0 &&
                  szPropType && strcmp( szPropType, "INTEGER" ) == 0 )
            fOverride = HB_TRUE;
         /* Audit: a hash whose keys never resolved — defaults to
            string-keyed emission on faith. */
         else if( strcmp( szCurType, "HASH" ) == 0 &&
                  ( ! szPropType ||
                    strcmp( szPropType, "HASH" ) == 0 ) )
            hb_auditEmit( "HASH-WEAK", szFile, pStmt->iLine,
               pStmt->value.asVar.szName,
               "hash keys never inferred — emitting string-keyed",
               "fine if string-keyed; else add a key-typed literal "
               "or subscript" );

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

      if( hb_auditActive() )
      {
         const char * szFn = szFuncKey ? szFuncKey : "?";
         int iBodyLine = pBody->iLine;
         int i;
         if( fConflict )
            hb_auditEmit( "RET-SENTINEL", szFile, iBodyLine, szFn,
               "RETURN types conflict — function degrades to USUAL",
               "return NIL for no-result, or split the function" );
         else if( fSawUnknown && szRetType )
            hb_auditEmit( "RET-SENTINEL", szFile, iBodyLine, szFn,
               "typed and uninferrable RETURNs mix — degrades to USUAL",
               "type the uninferrable branch or return NIL" );
         for( i = 0; i < s_iIntCand; i++ )
         {
            char szSym[ 160 ];
            if( ! s_aIntCand[ i ].fIndexUsed )
               continue;
            hb_snprintf( szSym, sizeof( szSym ), "%s:%s",
                         szFn, s_aIntCand[ i ].szName );
            if( s_aIntCand[ i ].fNonIntHard )
               hb_auditEmit( "INT-CONFLICT", szFile,
                  s_aIntCand[ i ].iNonIntLine, szSym,
                  "index-used but division / fractional value keeps "
                  "it decimal (W0026 fired)",
                  "wrap the expression with int() or keep decimal" );
            else if( s_aIntCand[ i ].fNonIntSoft )
               hb_auditEmit( "INT-CONFLICT", szFile,
                  s_aIntCand[ i ].iNonIntLine, szSym,
                  "index-used but a non-integral assignment or @pass "
                  "keeps it decimal (soft, no warning)",
                  "make the assignment provably integral (int(), "
                  "Len(), literals) to earn C# int" );
            /* Clean candidates now EMIT as C# int — working as
               intended, so no audit row: the report is for
               insufficiencies, and hundreds of success stories would
               bury the actionable debt. (INT-CANDIDATE was the
               pre-implementation measurement category.) */
         }
      }

      /* Pair with the entry save so an outer codegen-set reftab (or a
         nesting propagate call) is restored. */
      s_pPropRefTab = pSavedPropRefTab;
      return szResult;
   }
}
