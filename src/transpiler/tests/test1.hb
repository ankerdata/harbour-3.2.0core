#include "astype.ch"
// Test 1: PROCEDURE, LOCAL declarations, simple arithmetic
PROCEDURE Main()

   LOCAL x := 5 AS NUMERIC
   LOCAL y := 10 AS NUMERIC
   LOCAL z AS NUMERIC

   z := x + y

   QOut("x=" + Str(x))
   QOut("y=" + Str(y))
   QOut("z=" + Str(z))

RETURN
