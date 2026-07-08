#include "astype.ch"
// Test 64: a plain variable passed to a by-ref parameter without
// Harbour's `@`.
//
// Once a parameter is reached by-ref anywhere, the C# parameter emits
// `ref`, and C# then requires `ref` at EVERY call site (CS1620) —
// where Harbour happily takes a bare argument BY VALUE. The transpiler
// keeps that by-value semantics faithful: a bare argument emits
// `ref HbDiscard<T>.Seed(x)`, so the callee sees x's value but its
// write-back is discarded and the caller's x is untouched (see
// test80 for the full matrix). Emitting `ref x` would write back —
// the divergence W0020 warns about.

PROCEDURE Main()
   LOCAL n := 5 AS NUMERIC

   // @ form marks the param by-ref
   Inc(@n)
   // 15 — write-back
   QOut("viaref=" + LTrim(Str(n)))

   n := 5
   // Inc's RETURN is 15...
   QOut("bare=" + LTrim(Str(Inc(n))))
   // ...but n stays 5 (discarded)
   QOut("kept=" + LTrim(Str(n)))
RETURN

FUNCTION Inc( /*@*/nVal AS NUMERIC ) AS NUMERIC
   nVal := nVal + 10
RETURN nVal
