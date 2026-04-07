#include "astype.ch"
// Single line comment
FUNCTION Main() AS STRING

   // Double-slash inside function
   // trailing comment on code line
   LOCAL x := 5 AS INTEGER

   && dBASE-style comment
   && trailing dBASE comment
   LOCAL y := 10 AS INTEGER

   * Star comment at start of line
   LOCAL z := x + y AS USUAL

   NOTE Old-style comment keyword

   /* Single-line block comment */
   LOCAL cResult := "hello" AS STRING

   /* Multi-line
      block comment
      spanning three lines */
   cResult += " world"

   /* inline block comment */
   x += 1

RETURN cResult

