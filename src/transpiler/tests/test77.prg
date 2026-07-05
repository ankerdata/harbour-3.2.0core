// Test 77: function-scope STATIC isolation.
//
// Harbour scopes a STATIC declared inside a function body to that
// function only — two functions each declaring `STATIC nCounter` are
// two independent variables that persist across calls. The transpiler
// used to register statics by name alone, merging both into a single
// shared `<File>_nCounter` class field (wrong values at runtime, or
// CS0102 if emitted twice). Function statics now mangle as
// `<File>_<Func>_<Var>`; file-level statics keep `<File>_<Var>` and
// stay shared across the file's functions.

static snShared := 100

PROCEDURE Main()

    ? "a=", Bump()          // 1  — Bump's own counter
    ? "b=", Bump()          // 2
    ? "c=", Count()         // 1  — Count's counter, NOT Bump's
    ? "d=", Bump()          // 3
    ? "e=", Count()         // 2
    ? "f=", AddShared(10)   // 110 — file-level static, shared
    ? "g=", AddShared(10)   // 120
    ? "h=", PeekShared()    // 120 — same file-level static elsewhere

RETURN

function Bump()
    static nCounter := 0
    nCounter++
return nCounter

function Count()
    static nCounter := 0
    nCounter++
return nCounter

function AddShared(nInc)
    snShared += nInc
return snShared

function PeekShared()
return snShared
