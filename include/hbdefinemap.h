/*
 * Harbour Transpiler - Per-source-file #define class map
 *
 * The transpiler does not preprocess #include directives (setNoInclude),
 * so Harbour header #defines reach the C# emitter as bare identifiers.
 * The `gendefines.py` tool harvests those defines into per-source-file
 * const classes (e.g. `RmcommConst`, `DeadbillPrgConst`) and writes a
 * companion `defines_map.txt` that tells the emitter which class owns
 * each name.
 *
 * Map file format — one entry per line, tab-separated:
 *
 *     NAME<TAB>ClassName<TAB>[OwnerBasename]
 *
 *   - Empty OwnerBasename (trailing tab only) = global (header-origin).
 *     Applies to every file referencing NAME.
 *   - Non-empty OwnerBasename (e.g. `deadbill.prg`) = file-local.
 *     Applies only when the currently-compiled source matches.
 *
 * Lookup priority at emit time: local (owner == current file) beats
 * global. The emitter also asks `hb_defineMapIsLocalOwned` to decide
 * whether to skip the inline PPDEFINE emission for a name — if the
 * current file owns a local entry for it, the Const class already
 * declares it.
 *
 * Copyright 2026 harbour.github.io
 */

#ifndef HB_DEFINEMAP_H_
#define HB_DEFINEMAP_H_

#include "hbapi.h"

HB_EXTERN_BEGIN

/* Override the map path at runtime (set by `--defines-map=<path>` on
   the command line). Pass NULL to clear. An empty path means "no
   map"; lookups just return NULL and the emitter falls back to
   the legacy bare-name behaviour. */
extern void         hb_defineMapSetPath( const char * szPath );
extern const char * hb_defineMapGetPath( void );

/* Load the map into the process-wide tables. Safe to call multiple
   times; a second call clears and reloads. If no path is set or the
   file is missing, the tables end up empty (not an error). */
extern void hb_defineMapLoad( void );

/* Free the process-wide tables. Called at shutdown. */
extern void hb_defineMapFree( void );

/* Remember the current source file's basename (e.g. `deadbill.prg`)
   so subsequent lookups can apply local-owner shadowing. The string
   is duplicated; caller retains ownership. Pass NULL to clear. */
extern void hb_defineMapSetCurrentFile( const char * szBasename );

/* Look up NAME. Returns the owning class name, or NULL if the name
   isn't in the map. If the current file owns a local entry for NAME,
   that local class wins over any global entry. Returned pointer is
   owned by the map; do not free. */
extern const char * hb_defineMapLookup( const char * szName );

/* Returns HB_TRUE iff the current file has a local entry for NAME —
   i.e. the Const class already declares this define and the emitter
   should skip the inline PPDEFINE const. */
extern HB_BOOL hb_defineMapIsLocalOwned( const char * szName );

HB_EXTERN_END

#endif /* HB_DEFINEMAP_H_ */
