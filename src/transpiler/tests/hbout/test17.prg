#include "astype.ch"
// Test 17: by-reference parameters via @ at call sites
PROCEDURE Main()

   LOCAL nA := 1 AS NUMERIC
   LOCAL nB := 2 AS NUMERIC

   QOut("before: a=" + Str(nA) + " b=" + Str(nB))
   Swap(@nA, @nB)
   QOut("after:  a=" + Str(nA) + " b=" + Str(nB))

RETURN

PROCEDURE Swap( /*@*/nX AS NUMERIC, /*@*/nY AS NUMERIC )

   LOCAL nT := nX AS NUMERIC

   nX := nY
   nY := nT

RETURN
