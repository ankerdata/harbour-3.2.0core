#include "astype.ch"
// Test 19b: calls Swap (defined in test19a.prg) with @ args.
// See test19a.prg for the cross-file scan workflow this test exercises.
PROCEDURE Main()

   LOCAL nA := 1 AS NUMERIC
   LOCAL nB := 2 AS NUMERIC

   QOut("before: a=" + Str(nA) + " b=" + Str(nB))
   Swap(@nA, @nB)
   QOut("after:  a=" + Str(nA) + " b=" + Str(nB))

RETURN
