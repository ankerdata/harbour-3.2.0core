// Test 19b: calls Swap (defined in test19a.prg) with @ args.
// See test19a.prg for the cross-file scan workflow this test exercises.
PROCEDURE Main()

   LOCAL nA := 1
   LOCAL nB := 2

   ? "before: a=" + Str( nA ) + " b=" + Str( nB )
   Swap( @nA, @nB )
   ? "after:  a=" + Str( nA ) + " b=" + Str( nB )

RETURN
