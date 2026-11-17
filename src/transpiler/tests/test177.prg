// Test 177: a whole number earned from another. `j` starts at `i + 1` and
// `k` at `j + 1`: each is a subscript and so a long, but only once the
// one it starts from is known to be whole, which the first reading of the
// routine does not know yet. A local set from a loop variable (`nFound177
// := i`) earns it the same way. One that takes a fraction stays a
// decimal, and so does what is set from it.

PROCEDURE Main()

   LOCAL aRows := { "a", "b", "c", "d" }
   LOCAL i, j, k
   LOCAL nFound177 := 0
   LOCAL nHalf177, nNext177

   FOR i := 1 TO Len( aRows )
      FOR j := i + 1 TO Len( aRows )
         FOR k := j + 1 TO Len( aRows )
            ? aRows[ i ] + aRows[ j ] + aRows[ k ]
         NEXT
      NEXT
      IF aRows[ i ] == "c"
         nFound177 := i
      ENDIF
   NEXT
   ? aRows[ nFound177 ], nFound177

   nHalf177 := Len( aRows ) / 8
   nNext177 := nHalf177 + 1
   ? nHalf177 < 1, nNext177 > 1

   RETURN
