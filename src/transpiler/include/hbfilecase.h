/*
 * Harbour Transpiler - File-stem canonical-casing map
 *
 * Identifiers derived from a source-file stem (file-scope STATIC
 * prefix in gencsharp.c, file-mangled STATIC FUNCTION names, defines
 * Const class names produced by gendefines.py) inherit the casing of
 * the on-disk filename by default — `fplu.prg` yields `fplu_<var>`
 * and `FpluPrgConst`.  Projects that want a richer convention
 * (`FPlu_<var>`, `FPluPrgConst`) can supply a casing map via
 * `--filename-casing=<path>`.
 *
 * Map file format — one entry per line:
 *
 *     <stem><TAB><CamelCase>
 *
 * Blank lines and `#`-prefixed comments are ignored.  Lookups are
 * case-insensitive against the stem; the returned CamelCase string is
 * used verbatim.  Unmapped stems return NULL (callers fall back to
 * the on-disk casing).
 *
 * Copyright 2026 harbour.github.io
 */

#ifndef HB_FILECASE_H_
#define HB_FILECASE_H_

#include "hbapi.h"

HB_EXTERN_BEGIN

/* Override the map path at runtime (set by `--filename-casing=<path>`
   on the command line). Pass NULL to clear. An empty path means "no
   map"; lookups just return NULL. */
extern void         hb_fileCaseSetPath( const char * szPath );
extern const char * hb_fileCaseGetPath( void );

/* Look up the canonical CamelCase spelling for a file stem. Returns a
   pointer into the loaded table (caller does not own it) or NULL when
   no entry matches. Lazy-loads on first call. */
extern const char * hb_fileCaseLookup( const char * szStem );

/* Release the loaded table (called at transpiler shutdown). */
extern void         hb_fileCaseFree( void );

HB_EXTERN_END

#endif /* HB_FILECASE_H_ */
