// Test 45b: Main that calls LoadFlags (from test45a.prg) at both
// arities. See test45a.prg for the short-overload rationale.

PROCEDURE Main()

   LOCAL aArr := {}

   LoadFlags( "full.dat", @aArr, 4 )
   ? "full[1]=" + aArr[ 1 ]
   ? "full[4]=" + aArr[ 4 ]

   LoadFlags( "short.dat" )
   ? "short returned"

RETURN
