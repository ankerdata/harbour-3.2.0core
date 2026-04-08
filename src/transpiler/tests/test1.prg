// Test 1: PROCEDURE, LOCAL declarations, simple arithmetic
PROCEDURE Main()

   LOCAL x := 5
   LOCAL y := 10
   LOCAL z

   z := x + y

   ? "x=" + Str( x )
   ? "y=" + Str( y )
   ? "z=" + Str( z )

RETURN
