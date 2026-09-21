/*
 * Harbour Transpiler - Function knowledge table
 *
 * Loads src/transpiler/hbfuncs.tab — a tab-separated data file
 * describing what the transpiler knows about each Harbour function:
 *
 *   - Return type, used by the type-propagation pass.
 *   - Namespace prefix, used by the C# emitter to remap calls
 *     (e.g., STR → HbRuntime.STR).
 *
 * hb_funcTabInit() loads it once at startup, from the checkout the
 * binary was built in. Lookup is case-insensitive.
 *
 * Copyright 2026 harbour.github.io
 */

#ifndef HB_FUNCTAB_H_
#define HB_FUNCTAB_H_

#include "hbapi.h"

HB_EXTERN_BEGIN

/* Loads hbfuncs.tab from src/transpiler/ of the checkout whose bin/
   holds the binary (szExePath is argv[0]), else from src/transpiler/
   under the current directory. Returns HB_FALSE, having named each
   path tried on stderr, when neither opens: the transpiler must then
   stop, since an empty table would emit every core call unrouted. */
extern HB_BOOL      hb_funcTabInit( const char * szExePath );

/* Returns the namespace prefix for szName (e.g., "HbRuntime"), or
   NULL if the function is not in the table or has no remap. */
extern const char * hb_funcTabPrefix( const char * szName );

/* Returns the declared return type for szName (e.g., "STRING"), or
   NULL if the function is not in the table or its return type is
   unknown / void. */
extern const char * hb_funcTabReturnType( const char * szName );

/* Returns the canonical casing of szName as recorded in hbfuncs.tab
   (case-insensitive lookup). NULL if the name is not in the table.
   Used by the C# emitter so the generated call matches the actual
   method name in HbRuntime.cs (C# is case-sensitive). */
extern const char * hb_funcTabCanonName( const char * szName );

/* Free the loaded table. Optional — safe to leak at process exit. */
extern void         hb_funcTabFree( void );

HB_EXTERN_END

#endif /* HB_FUNCTAB_H_ */
