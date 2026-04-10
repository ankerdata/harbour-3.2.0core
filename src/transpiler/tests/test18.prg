// Test 18: default parameters and middle-gap call sites
PROCEDURE Main()

   Fred( 1 )
   Fred( 10, 20 )
   Fred( 100, 200, 300 )
   Fred( 1000,, 3000 )    // middle gap → named args in C#

RETURN

PROCEDURE Fred( nA, nB, nC )

   ? "a=" + Str( nA )
   IF nB != NIL
      ? "b=" + Str( nB )
   ENDIF
   IF nC != NIL
      ? "c=" + Str( nC )
   ENDIF

RETURN
