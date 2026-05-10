#include "astype.ch"
// Test 18: default parameters and middle-gap call sites.
//
// `nA` is non-nullable (numeric Hungarian, strict-typing rule). `xB`
// and `xC` are USUAL (`x` prefix) so they can legitimately be NIL
// when the caller omits them — the canonical Clipper pattern for
// truly-optional params. Renaming from the prior `nB`, `nC` matches
// the strict-typing rule that says Hungarian-typed value params are
// never NIL: a NIL guard on `nB` would now be unreachable because
// the C# emit defaults `nB` to 0.

PROCEDURE Main()

   Fred(1)
   Fred(10, 20)
   Fred(100, 200, 300)
   // middle gap → named args in C#
   Fred(1000, , 3000)

RETURN

PROCEDURE Fred( nA AS NUMERIC, xB AS NUMERIC, xC AS NUMERIC )

   QOut("a=" + Str(nA))
   IF xB != NIL
      QOut("b=" + Str(xB))
   ENDIF

   IF xC != NIL
      QOut("c=" + Str(xC))
   ENDIF

RETURN
