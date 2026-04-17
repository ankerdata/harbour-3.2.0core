#include "astype.ch"
// Test 0: Comments (all types), SET COLOR, basic LOCAL assignment
PROCEDURE main()

   // /* some comment*/
   // procedure main()
   //     ? "hello"
   // return

   // Single line comment
   // trailing comment
   LOCAL x := 5 AS NUMERIC
   QOut("x=" + Str(x))
   * Star comment
   NOTE Old comment
   /*x for sex is NB*/
   && dBASE commentd
   x := x + 1
   QOut("x=" + Str(x))

   /* Standalone
      multi-line comment */
   SetColor('"W/B"')

   QOut("Amazing success!")

RETURN
