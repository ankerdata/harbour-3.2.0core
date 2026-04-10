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
 * The path is hardcoded (HB_FUNCTAB_PATH below) — change it if the
 * source tree moves. Lookup is case-insensitive; the table is loaded
 * lazily on first access.
 *
 * Copyright 2026 harbour.github.io
 */

#ifndef HB_FUNCTAB_H_
#define HB_FUNCTAB_H_

#include "hbapi.h"

HB_EXTERN_BEGIN

#define HB_FUNCTAB_PATH \
   "/Users/alexstrickland/dev/harbour-core/src/transpiler/hbfuncs.tab"

/* Returns the namespace prefix for szName (e.g., "HbRuntime"), or
   NULL if the function is not in the table or has no remap. */
extern const char * hb_funcTabPrefix( const char * szName );

/* Returns the declared return type for szName (e.g., "STRING"), or
   NULL if the function is not in the table or its return type is
   unknown / void. */
extern const char * hb_funcTabReturnType( const char * szName );

/* Free the loaded table. Optional — safe to leak at process exit. */
extern void         hb_funcTabFree( void );

HB_EXTERN_END

#endif /* HB_FUNCTAB_H_ */
