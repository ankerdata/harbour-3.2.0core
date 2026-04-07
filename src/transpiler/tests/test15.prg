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

   // Array subscript assignment
   aNumbers[ 2 ] := 99

   // Array length
   nLen := Len( aNumbers )

   // FOR loop with array subscript
   nTotal := 0
   FOR i := 1 TO Len( aNumbers )
      nTotal += aNumbers[ i ]
   NEXT

   // FOR EACH over array
   nTotal := 0
   FOR EACH nItem IN aNumbers
      nTotal += nItem
   NEXT

   // Nested array access
   nTotal := aMatrix[ 2 ][ 1 ]

   // Hash access
   cName := hPerson[ "name" ]

   // Hash assignment
   hPerson[ "age" ] := 31

   // String operations via mapped functions
   cUpper := Upper( cText )
   cTrimmed := AllTrim( cText )
   cSub := SubStr( cText, 3, 5 )
   nPos := Len( cText )

   ? nTotal
   ? cName
   ? cUpper
   ? cTrimmed
   ? cSub

RETURN nPos
