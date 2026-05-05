/*
 * Harbour Transpiler - Variable type-hint table
 *
 * A small project-supplied list of variable names → types. Used to
 * accept short loop counters (`i`, `j`, `k`) and other non-Hungarian
 * names that the project promises to type a particular way without
 * having to rename them. The Hungarian-prefix gate (W0021 in
 * genscan.c) treats names in this table as valid; type inference
 * picks up the declared type for them.
 *
 * Format (one per line, # comments, blank lines ignored, tab-
 * separated): NAME<TAB>TYPE  — types match hbfuncs.tab conventions
 * (NUMERIC, STRING, LOGICAL, ARRAY, HASH, OBJECT, BLOCK, DATE,
 * TIMESTAMP), or `-` for "any".
 *
 * Loaded lazily on first lookup. The path is set per-run via the
 * `--var-types=<path>` CLI flag (see cmdcheck.c); without the flag
 * the table stays empty and every name falls through to the normal
 * Hungarian rules.
 *
 * Copyright 2026 harbour.github.io
 */

#ifndef HB_VARTYPES_H_
#define HB_VARTYPES_H_

#include "hbapi.h"

HB_EXTERN_BEGIN

/* Set the path passed via --var-types. Subsequent lookups load it
   lazily. Pass NULL or empty to disable the table. */
extern void         hb_varTabSetPath( const char * szPath );

/* Returns the type-name string for szName (case-insensitive), or
   NULL if the name isn't in the table. */
extern const char * hb_varTabType( const char * szName );

/* Free the loaded table. Optional — safe to leak at process exit. */
extern void         hb_varTabFree( void );

HB_EXTERN_END

#endif /* HB_VARTYPES_H_ */
