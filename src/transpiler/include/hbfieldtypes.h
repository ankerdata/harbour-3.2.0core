/*
 * Harbour Transpiler - ORM def-class field-type map
 *
 * The EasiPOS filedefinitions/*def.prg files fully type every table
 * field: each def factory (`function DepartmentDef(cPath)`) pairs a
 * field array (`{ "DEPTNO", "N", len, dec }`) with a file-static
 * shHash mapping DB fields to Hungarian accessors ("DEPTNO" =>
 * "nNo"). `gen-fieldtypes.py` joins the two into a per-class table so
 * `ConstructORMTable(DepartmentDef())` receivers can be typed as
 * class DepartmentDef and every member access (`oDepartment:nNo`)
 * resolves to the field's exact C# type.
 *
 * Map file format (fieldtypes.tsv) — one entry per line, tab-separated:
 *
 *     Class<TAB>accessor<TAB>cstype<TAB>DBFIELD<TAB>len<TAB>dec<TAB>deffile
 *
 * Only the first three columns are read here; the rest are audit
 * context. cstype is one of int|long|decimal|string|bool|date|timestamp.
 * Lines starting with `#` are comments. Lookups are case-insensitive;
 * the stored spelling is canonical for emission (a source-side
 * `PluDef` call site must emit the generated class's `PLUDef`).
 *
 * Copyright 2026 harbour.github.io
 */

#ifndef HB_FIELDTYPES_H_
#define HB_FIELDTYPES_H_

#include "hbapi.h"

HB_EXTERN_BEGIN

/* Override the map path at runtime (set by `--fieldtypes=<path>` on
   the command line). Pass NULL to clear. No path set means "no map";
   lookups return NULL and ORM receivers stay dynamic as before. */
extern void         hb_fieldTypesSetPath( const char * szPath );
extern const char * hb_fieldTypesGetPath( void );

/* Load the map into the process-wide table. Safe to call multiple
   times; a second call clears and reloads. Missing file = empty
   table (not an error). Lookups load lazily; explicit calls are
   only needed to force a reload after a path change. */
extern void hb_fieldTypesLoad( void );
extern void hb_fieldTypesFree( void );

/* Is szClass a mapped ORM def class? Returns the canonical spelling
   from the map (e.g. "PLUDef" for a "pludef" query), or NULL. */
extern const char * hb_fieldTypesClassCanon( const char * szClass );

/* Look up a field accessor on a def class. Returns the C# type token
   (int|long|decimal|string|bool|date|timestamp) or NULL when the
   member isn't a mapped field (ORM base members like Seek/RecLock
   fall through to the caller's existing handling). When pszCanonMember
   is non-NULL it receives the map's canonical accessor spelling. */
extern const char * hb_fieldTypesMember( const char * szClass,
                                         const char * szMember,
                                         const char ** pszCanonMember );

/* Map a fieldtypes C# token to the transpiler's Harbour type tag
   (int -> INTEGER, long/decimal -> NUMERIC, string -> STRING,
   bool -> LOGICAL, date -> DATE, timestamp -> TIMESTAMP). Returns
   NULL for unknown tokens. long stays NUMERIC for inference — same
   rule as mapped #defines — while the generated model property is
   the exact C# long. */
extern const char * hb_fieldTypesHbType( const char * szCsToken );

HB_EXTERN_END

#endif /* HB_FIELDTYPES_H_ */
