// Test 47: --preload-list header-rule injection with filtered
// #xcommand / #translate / macro-define directives.
//
// The `#include "test47.ch"` below is needed so the Harbour baseline
// (buildprg.sh → hbmk2) can resolve the constants at compile time.
// The transpiler runs with setNoInclude=TRUE so the directive is
// captured as an AST node but the file is NOT opened or tokenised —
// every TEST47_* reference below therefore arrives via the preload
// path (tests/preload.txt → test47.ch), not through source include
// expansion. If the preload regresses, the transpiled output would
// either fail to resolve the names or pick the wrong branch for
// TEST47_COND.
//
// The matching -DTEST47_FLAG isn't set at the command line, so the
// #else branch fires and TEST47_COND resolves to 7. That asserts the
// preload filter actually preserves #ifdef / #else / #endif — a
// flatten-all-defines filter would pick up both 42 and 7 and
// arbitrarily keep one.

#include "test47.ch"

PROCEDURE Main()
   ? "base=" + AllTrim( Str( TEST47_BASE ) )
   ? "cond=" + AllTrim( Str( TEST47_COND ) )
   ? "label=" + TEST47_LABEL
RETURN
