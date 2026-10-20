// Test 46b: every call to SaveFlags uses the full arity. See
// test46a.prg.

PROCEDURE Main()

   LOCAL aArr := {}

   SaveFlags( "out.dat", @aArr, 3 )

   ? "saved[1]=" + aArr[ 1 ]
   ? "saved[3]=" + aArr[ 3 ]

RETURN
