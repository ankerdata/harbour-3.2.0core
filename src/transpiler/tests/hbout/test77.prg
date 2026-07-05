#include "astype.ch"
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

STATIC snShared := 100 AS NUMERIC

PROCEDURE Main()

   // 1  — Bump's own counter
   QOut("a=", Bump())
   // 2
   QOut("b=", Bump())
   // 1  — Count's counter, NOT Bump's
   QOut("c=", Count())
   // 3
   QOut("d=", Bump())
   // 2
   QOut("e=", Count())
   // 110 — file-level static, shared
   QOut("f=", AddShared(10))
   // 120
   QOut("g=", AddShared(10))
   // 120 — same file-level static elsewhere
   QOut("h=", PeekShared())

RETURN

FUNCTION Bump() AS NUMERIC
   STATIC nCounter := 0 AS NUMERIC
   nCounter++
RETURN nCounter

FUNCTION Count() AS NUMERIC
   STATIC nCounter := 0 AS NUMERIC
   nCounter++
RETURN nCounter

FUNCTION AddShared( nInc AS NUMERIC ) AS NUMERIC
   snShared += nInc
RETURN snShared

FUNCTION PeekShared() AS NUMERIC
RETURN snShared
