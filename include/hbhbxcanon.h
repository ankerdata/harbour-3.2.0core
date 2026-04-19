/*
 * Harbour Transpiler — canonical function-name map from .hbx files
 *
 * Harbour is case-insensitive for identifiers; C# isn't. Source code
 * that calls `hb_bitand()`, `hb_bitAnd()`, and `hb_BitAnd()`
 * interchangeably compiles fine under native Harbour but produces
 * three distinct CS0103 errors when emitted verbatim to C#, because
 * HbRuntime.cs only exposes one spelling.
 *
 * The .hbx files shipped with Harbour (`include/harbour.hbx` plus
 * optional `contrib/<lib>/<lib>.hbx`) are the canonical registry:
 * each line `DYNAMIC <FuncName>` declares the authoritative casing
 * for one exported function. hbformat uses them for the same reason
 * we do. This module parses one or more hbx files into a
 * lowercased-key → canonical-spelling lookup, consulted by the C#
 * emitter before emitting any bare function-call identifier.
 *
 * The mechanism does NOT help with user LOCAL/STATIC/DATA names
 * (e.g. `oPLU` vs `oPlu`) — those aren't in any hbx file. That's a
 * separate emitter concern.
 *
 * Copyright 2026 harbour.github.io
 */

#ifndef HB_HBXCANON_H_
#define HB_HBXCANON_H_

#include "hbapi.h"

HB_EXTERN_BEGIN

/* Parse szPath and merge its `DYNAMIC <Name>` entries into the
   process-wide map. Safe to call repeatedly for multiple hbx files;
   later entries win on duplicate lowercased keys (contrib file can
   override core if both are loaded). Returns HB_TRUE on successful
   open+parse, HB_FALSE if the file is missing or unreadable. */
extern HB_BOOL hb_hbxCanonLoad( const char * szPath );

/* Return the canonical spelling for szName's lowercased form, or
   NULL if no entry exists. Caller should substitute the return
   value for szName when non-NULL; otherwise leave the identifier
   alone. Returned pointer is owned by the map; do not free. */
extern const char * hb_hbxCanonLookup( const char * szName );

/* Release all loaded entries. Called at shutdown. */
extern void hb_hbxCanonFree( void );

HB_EXTERN_END

#endif /* HB_HBXCANON_H_ */
