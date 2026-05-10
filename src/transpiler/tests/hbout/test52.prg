#include "astype.ch"
// Test 52: `x`-prefix USUAL parameters are sticky in the reftab.
//
// When a parameter name starts with `x` or `X`, the user has chosen
// USUAL deliberately — typically because the body does VALTYPE()
// dispatch (the canonical Clipper/Harbour pattern for variant input).
// Without this guard, hb_refTabRefineParam would adopt the first
// concrete type observed at any call site (the "fresh USUAL slot →
// adopt new type" branch), narrowing `xFoo` to e.g. STRING. Other
// callers passing a different type then surface as CS1503 at C# emit.
//
// Pattern observed in easipos: msgresp.prg's MsgResponse(xAnswer1, ...)
// — body branches on VALTYPE(xAnswer1), callers pass either descm
// numerics like D_1Yes or strings from GetDescM(). Pre-fix, xAnswer1
// was typed STRING and 25+ call sites failed at the C# build.
//
// This test exercises the round-trip with a `xVal` parameter that
// the body dispatches on (string vs numeric) and call sites that
// pass each type. All three pipelines (.prg / .hb / .cs) must
// produce the same output.

PROCEDURE Main()

   /* Mixed-type call sites — without the sticky guard, the first
      caller's type would narrow xVal and the second would CS1503. */
   QOut("string: " + Describe("hello"))
   QOut("number: " + Describe(42))
   QOut("logical:" + Describe(.T.))

RETURN

FUNCTION Describe( xVal AS USUAL ) AS STRING
   /* Body does VALTYPE() dispatch — the canonical USUAL pattern. */
   IF VALTYPE(xVal) == "C"
   RETURN "string-" + xVal
   ELSEIF VALTYPE(xVal) == "N"
   RETURN "number-" + LTrim(Str(xVal))
   ELSEIF VALTYPE(xVal) == "L"
   RETURN "bool-" + IIF(xVal, "T", "F")
   ENDIF

RETURN "unknown"
