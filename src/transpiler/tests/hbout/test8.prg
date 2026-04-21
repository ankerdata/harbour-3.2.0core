#include "astype.ch"
// Test 8: All comment types with code interspersed
FUNCTION Main() AS STRING

   // Double-slash inside function
   // trailing comment on code line
   LOCAL x := 5 AS NUMERIC

   && dBASE-style comment
   && trailing dBASE comment
   LOCAL y := 10 AS NUMERIC

   * Star comment at start of line
   LOCAL z := x + y AS NUMERIC

   NOTE Old-style comment keyword

   /* Single-line block comment */
   LOCAL cResult := "hello" AS STRING

   QOut("x=" + Str(x))
   QOut("y=" + Str(y))
   QOut("z=" + Str(z))
   QOut("cResult=" + cResult)

   /* Multi-line
      block comment
      spanning three lines */
   cResult := cResult + " world"
   QOut("cResult=" + cResult)

   /* inline block comment */
   x := x + 1
   QOut("x=" + Str(x))

RETURN cResult
