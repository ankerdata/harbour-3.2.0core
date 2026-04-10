// Test 17: by-reference parameters via @ at call sites
PROCEDURE Main()

   LOCAL nA := 1
   LOCAL nB := 2

   ? "before: a=" + Str( nA ) + " b=" + Str( nB )
   Swap( @nA, @nB )
   ? "after:  a=" + Str( nA ) + " b=" + Str( nB )

RETURN

PROCEDURE Swap( nX, nY )

   LOCAL nT := nX

   nX := nY
   nY := nT

RETURN
