#include "astype.ch"
// Test 18: default parameters and middle-gap call sites
PROCEDURE Main()

   Fred(1)
   Fred(10, 20)
   Fred(100, 200, 300)
   // middle gap → named args in C#
   Fred(1000, , 3000)

RETURN

PROCEDURE Fred( nA AS NUMERIC, nB AS NUMERIC, nC AS NUMERIC )

   QOut("a=" + Str(nA))
   IF nB != NIL
      QOut("b=" + Str(nB))
   ENDIF

   IF nC != NIL
      QOut("c=" + Str(nC))
   ENDIF

RETURN
