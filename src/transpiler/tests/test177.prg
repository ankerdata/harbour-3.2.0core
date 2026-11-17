// Test 177: a whole number earned from another. `j` starts at `i + 1` and
// `k` at `j + 1`: each is a subscript and so a long, but only once the
// one it starts from is known to be whole, which the first reading of the
// routine does not know yet. A local set from a loop variable (`nFound177
// := i`) earns it the same way. One that takes a fraction stays a
// decimal, and so does what is set from it. A SWITCH subject is whole
// when its case labels are, and a loop variable set from a whole file
// static (`siStart177 + 1`) is whole too, also where the routine writes
// the variable back into the static (Resume177()).

#define KIND177_ONE   1
#define KIND177_TWO   2

STATIC siStart177 := 1
STATIC siUnset177

PROCEDURE Main()

   LOCAL aRows := { "a", "b", "c", "d" }
   LOCAL i, j, k
   LOCAL nFound177 := 0
   LOCAL nHalf177, nNext177
   LOCAL m, n

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

   FOR m := 1 TO KIND177_TWO
      SWITCH m
      CASE KIND177_ONE
         ? "one", aRows[ m ]
         EXIT
      CASE KIND177_TWO
         ? "two", aRows[ m ]
         EXIT
      ENDSWITCH
   NEXT

   n := siStart177 + 1
   ? aRows[ n ]
   siUnset177 := 0
   n := siUnset177 + 2
   ? aRows[ n ]
   ? Resume177( .F. ), Resume177( .T. ), siUnset177

   RETURN

FUNCTION Resume177( lResume )

   LOCAL aLines := { "x", "y", "z" }
   LOCAL i
   LOCAL cOut := ""

   IF lResume
      i := siUnset177 + 1
   ELSE
      i := 1
   ENDIF

   WHILE .T.
      IF i <= Len( aLines ) .AND. ! Empty( aLines[ i ] )
         cOut += aLines[ i ]
         siUnset177 := i
      ELSEIF i > Len( aLines )
         EXIT
      ENDIF
      i++
   ENDDO

RETURN cOut
