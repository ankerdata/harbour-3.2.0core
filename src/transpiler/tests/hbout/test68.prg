#include "astype.ch"
// Test 68: ref-shim temp naming.
//
// Two things this guards about the `_hbref<base>_<pos>` temps the by-ref
// shim emits:
//   1. Two by-ref args in ONE call get distinct temps via the <pos>
//      suffix while sharing one <base> (here _hbref0_1 and _hbref0_2).
//   2. Sequential calls — each wrapped in its own `{ }` scope — REUSE the
//      base rather than incrementing it: the base tracks shim-block
//      nesting depth, not a monotonic counter, so both calls below emit
//      _hbref0_… (a nested shim would be _hbref1_…).
//
// MinMax has two USUAL by-ref outputs (x-prefixed); the caller passes
// numeric locals, so each @arg mismatches the `ref dynamic` parameter and
// is shimmed. The values must round-trip identically through -GT and -GS.

FUNCTION MinMax( aData AS ARRAY, /*@*/xLo AS USUAL, /*@*/xHi AS USUAL )
   LOCAL i AS NUMERIC
   xLo := aData[1]
   xHi := aData[1]
   FOR i := 2 TO Len(aData)
      IF aData[i] < xLo
         xLo := aData[i]
      ENDIF

      IF aData[i] > xHi
         xHi := aData[i]
      ENDIF
   NEXT

RETURN NIL

PROCEDURE Main()
   LOCAL nLo AS NUMERIC
   LOCAL nHi AS NUMERIC
   // two refs in one call
   MinMax({3, 1, 4, 1, 5}, @nLo, @nHi)
   QOut("a lo=" + Str(nLo, 2) + " hi=" + Str(nHi, 2))
   // sequential call — reuses base 0
   MinMax({9, 2, 6, 8, 7}, @nLo, @nHi)
   QOut("b lo=" + Str(nLo, 2) + " hi=" + Str(nHi, 2))
RETURN
