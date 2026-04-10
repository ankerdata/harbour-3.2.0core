// Test 20b: calls DoubleIt (defined in test20a.prg) from QuadrupleIt
// and from Main. See test20a.prg for the cross-file return-type
// propagation rationale.
FUNCTION QuadrupleIt( n )
RETURN DoubleIt( DoubleIt( n ) )

PROCEDURE Main()
   ? "QuadrupleIt(5)=" + Str( QuadrupleIt( 5 ) )
RETURN
