// Test 46b: every call to SaveFlags uses the full arity, so the
// emitter's short-overload guard sees no bit in 0..iFirstRef and
// skips overload emission. See test46a.prg for details.

PROCEDURE Main()

   LOCAL aArr := {}

   SaveFlags( "out.dat", @aArr, 3 )

   ? "saved[1]=" + aArr[ 1 ]
   ? "saved[3]=" + aArr[ 3 ]

RETURN
