/*
 * Harbour Transpiler - User function signature table
 *
 * Records signatures of every user-defined function/procedure found
 * in the source tree, plus information about how each function gets
 * called from the rest of the program. The C# emitter consults this
 * table to:
 *
 *   - emit `ref` on by-reference parameters
 *   - emit default values on every declared parameter so callers can
 *     supply fewer arguments than the declaration
 *   - rewrite call sites with middle empty slots into named-argument
 *     form, e.g. Fred(x, , z)  ->  Fred(x, c: z)
 *
 * The table is loaded once at the start of every transpile run from a
 * single hardcoded file (HB_REFTAB_PATH). It is *not* saved during a
 * normal transpile — saving only happens in the dedicated scan mode
 * (HB_LANG_SCAN). This keeps regular transpile runs deterministic and
 * parallelisable.
 *
 * Workflow for a multi-file codebase:
 *
 *     hbtranspiler --scan-funcs file1.prg file2.prg ... fileN.prg
 *     hbtranspiler -GS file1.prg
 *     hbtranspiler -GS file2.prg
 *     ...
 *
 * The scan step needs to be re-run any time function signatures or
 * call-site by-ref usage changes.
 *
 * Copyright 2026 harbour.github.io
 */

#ifndef HB_REFTAB_H_
#define HB_REFTAB_H_

#include "hbast.h"

HB_EXTERN_BEGIN

/* The single hardcoded path the loader reads from. Change this if the
   source tree moves. */
#define HB_REFTAB_PATH \
   "/Users/alexstrickland/dev/harbour-core/src/transpiler/hbreftab.tab"

typedef struct HB_REFTAB_   HB_REFTAB,   * PHB_REFTAB;
typedef struct HB_REFPARAM_ HB_REFPARAM, * PHB_REFPARAM;

/* One parameter slot in a function signature. */
struct HB_REFPARAM_
{
   char *  szName;          /* parameter name as written in source (owned) */
   char *  szType;          /* inferred type, e.g. "NUMERIC" or "USUAL" (owned) */
   HB_BOOL fByRef;          /* set if any call site passes this slot by-ref */
   HB_BOOL fNilable;        /* set if the function body compares to or
                               assigns NIL on this slot — the C# emitter
                               renders these with `?` so they accept null */
   HB_BOOL fConflict;       /* set once two *different* specific types have
                               been seen at call sites. The slot is frozen
                               as USUAL and no further refinements are
                               accepted. */
};

/* Result of hb_refTabRefineParamType. Distinguishing these lets the
   caller emit a one-shot warning on the transition from specific to
   conflicted, without spamming on every subsequent call site. */
typedef enum
{
   HB_REFINE_OK,               /* no change (same type, or USUAL input) */
   HB_REFINE_REFINED,          /* USUAL -> specific */
   HB_REFINE_CONFLICT,         /* specific -> different specific (just conflicted) */
   HB_REFINE_ALREADY_CONFLICT  /* slot was already conflicted */
} HB_REFINE_RESULT;

/* ---- table lifecycle ---- */

extern PHB_REFTAB hb_refTabNew( void );
extern void       hb_refTabFree( PHB_REFTAB pTab );

/* Build the table key for a function or method.
     - For a free function (szClass NULL/empty), returns szMember unchanged.
     - For a method (szClass non-empty), returns "ClassName::MethodName"
       in a static buffer. The buffer is owned by the function and is
       overwritten by the next call — copy it if you need to keep it.
   This is the only place the key format is defined; everything else
   (storage, lookup, persistence) treats the result as an opaque
   case-insensitive string. */
extern const char * hb_refTabMethodKey( const char * szClass,
                                        const char * szMember );

/* ---- registering function definitions (used by the scanner) ---- */

/* Register or update a function definition. szTypes may be NULL, in
   which case all params get type "USUAL". szTypes is an array of nParams
   pointers; ownership is not taken. fVariadic marks the function as
   accepting more args than declared (use of PCount/HB_PValue). */
extern void hb_refTabAddFunc( PHB_REFTAB    pTab,
                              const char *  szName,
                              int           nParams,
                              const char ** ppNames,
                              const char ** ppTypes,
                              HB_BOOL       fVariadic );

/* Mark parameter at zero-based position iPos of function szFunc as
   by-ref. Positions >= 64 are silently ignored. The function does not
   need to have been registered yet — a stub entry is created. */
extern void hb_refTabMark( PHB_REFTAB pTab, const char * szFunc, int iPos );

/* Mark parameter at zero-based position iPos of function szFunc as
   nilable (the function body compares it to NIL or assigns NIL to it).
   Same lifecycle rules as hb_refTabMark. */
extern void hb_refTabSetNilable( PHB_REFTAB pTab, const char * szFunc, int iPos );

/* Record the inferred return type for szFunc (e.g. "NUMERIC", "STRING").
   Pass NULL to clear. The function does not need to be registered yet —
   a stub entry is created. */
extern void hb_refTabSetReturnType( PHB_REFTAB pTab, const char * szFunc,
                                    const char * szRetType );

/* Refine the type of parameter at position iPos of szFunc based on a
   call-site argument type. Merge rules:
     - szNewType NULL or "USUAL"        -> HB_REFINE_OK (no change)
     - slot already conflicted          -> HB_REFINE_ALREADY_CONFLICT
     - slot is NULL / "USUAL"           -> adopt szNewType, return REFINED
     - slot equals szNewType            -> HB_REFINE_OK
     - slot differs from szNewType      -> downgrade to "USUAL",
                                           set fConflict, return CONFLICT
   The function must already be registered via hb_refTabAddFunc for
   refinement to take effect; call-site-only knowledge of an unregistered
   function is ignored. */
extern HB_REFINE_RESULT hb_refTabRefineParamType(
   PHB_REFTAB pTab, const char * szFunc, int iPos, const char * szNewType );

/* ---- queries (used by the emitters) ---- */

/* Backward-compat predicate. Returns HB_TRUE if iPos is by-ref. */
extern HB_BOOL hb_refTabIsRef( PHB_REFTAB pTab, const char * szFunc, int iPos );

/* Returns HB_TRUE if iPos is marked nilable. */
extern HB_BOOL hb_refTabIsNilable( PHB_REFTAB pTab, const char * szFunc, int iPos );

/* Mark szName as a known user-defined class. The scanner sets this
   for every CLASS it sees so the C# emitter can recognise
   ClassName():New() patterns even when ClassName lives in another
   file. The function does not need to be registered yet. */
extern void hb_refTabMarkClass( PHB_REFTAB pTab, const char * szName );

/* Returns HB_TRUE if szName has been marked as a class. */
extern HB_BOOL hb_refTabIsClass( PHB_REFTAB pTab, const char * szName );

/* Returns the recorded return type for szFunc (e.g. "NUMERIC"), or
   NULL if the function isn't registered or has no known return type.
   The returned pointer is owned by the table — do not free. */
extern const char * hb_refTabReturnType( PHB_REFTAB pTab, const char * szFunc );

/* Returns the declared parameter count, or -1 if the function is not
   in the table. A return of 0 means "registered with zero params". */
extern int hb_refTabParamCount( PHB_REFTAB pTab, const char * szFunc );

/* Returns HB_TRUE if the function is registered as variadic. */
extern HB_BOOL hb_refTabIsVariadic( PHB_REFTAB pTab, const char * szFunc );

/* Returns the parameter at the given position, or NULL if out of range
   or the function is unknown. The returned pointer is owned by the
   table — do not free it. */
extern const HB_REFPARAM * hb_refTabParam( PHB_REFTAB pTab,
                                           const char * szFunc, int iPos );

/* ---- AST scanner ---- */

/* Walk the program (AST function list + compiler function list, walked
   in lockstep) and register every function definition (with its real
   parameter count from wParamCount), plus every call site that passes
   an argument by-ref. The HB_COMP_DECL is passed in directly so the
   scanner can read both halves. */
extern void hb_refTabCollect( PHB_REFTAB pTab, HB_COMP_DECL );

/* ---- persistence ----
 *
 * Text format, one row per function:
 *   NAME<TAB>FLAGS<TAB>RETTYPE<TAB>NPARAMS<TAB>PARAM_1<TAB>...<TAB>PARAM_N
 *
 *   FLAGS   = "V" if variadic, "-" otherwise
 *   RETTYPE = inferred return type (NUMERIC/STRING/...) or "-" if unknown
 *   PARAM   = name:type:pflags
 *   pflags  = a string of flag letters, or "-" for none:
 *               R = by-ref (some call site uses @var)
 *               N = nilable (function body compares/assigns NIL)
 *             Letters can combine in any order, e.g. "RN" or "NR".
 *
 * Loading merges into the existing table; later definitions overwrite
 * earlier ones (same name = update arity/types/refs).
 */
extern HB_BOOL hb_refTabSave( PHB_REFTAB pTab, const char * szPath );
extern HB_BOOL hb_refTabLoad( PHB_REFTAB pTab, const char * szPath );

HB_EXTERN_END

#endif /* HB_REFTAB_H_ */
