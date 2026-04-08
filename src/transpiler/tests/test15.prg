// Test 15: Array and hash operations

FUNCTION Main()

   LOCAL aNumbers := { 10, 20, 30, 40, 50 }
   LOCAL aNames := { "Alice", "Bob", "Charlie" }
   LOCAL hPerson := { "name" => "John", "age" => 30 }
   LOCAL hConfig := { "debug" => .T., "timeout" => 60 }
   LOCAL aMatrix := { { 1, 2 }, { 3, 4 }, { 5, 6 } }
   LOCAL aEmpty := {}
   LOCAL nTotal := 0
   LOCAL nLen
   LOCAL nPos
   LOCAL i
   LOCAL cName
   LOCAL nItem
   LOCAL cText := "  Hello World  "
   LOCAL cUpper
   LOCAL cTrimmed
   LOCAL cSub

   // Array subscript access
   nTotal := aNumbers[ 1 ] + aNumbers[ 3 ] + aNumbers[ 5 ]
   ? "nTotal=" + Str( nTotal )

   // Array subscript assignment
   aNumbers[ 2 ] := 99
   ? "aNumbers[2]=" + Str( aNumbers[ 2 ] )

   // Array length
   nLen := Len( aNumbers )
   ? "nLen=" + Str( nLen )

   // FOR loop with array subscript
   nTotal := 0
   FOR i := 1 TO Len( aNumbers )
      nTotal += aNumbers[ i ]
   NEXT
   ? "nTotal=" + Str( nTotal )

   // FOR EACH over array
   nTotal := 0
   FOR EACH nItem IN aNumbers
      nTotal += nItem
   NEXT
   ? "nTotal=" + Str( nTotal )

   // Nested array access
   nTotal := aMatrix[ 2 ][ 1 ]
   ? "nTotal=" + Str( nTotal )

   // Hash access
   cName := hPerson[ "name" ]
   ? "cName=" + cName

   // Hash assignment
   hPerson[ "age" ] := 31
   ? "age=" + Str( hPerson[ "age" ] )

   // String operations via mapped functions
   cUpper := Upper( cText )
   ? "cUpper=" + cUpper
   cTrimmed := AllTrim( cText )
   ? "cTrimmed=" + cTrimmed
   cSub := SubStr( cText, 3, 5 )
   ? "cSub=" + cSub
   nPos := Len( cText )
   ? "nPos=" + Str( nPos )

RETURN nPos
