#include "astype.ch"
// Test 15: Array and hash operations

FUNCTION Main() AS NUMERIC

   LOCAL aNumbers := {10, 20, 30, 40, 50} AS ARRAY
   LOCAL aNames := {"Alice", "Bob", "Charlie"} AS ARRAY
   LOCAL hPerson := {"name" => "John", "age" => 30} AS HASH
   LOCAL hConfig := {"debug" => .T., "timeout" => 60} AS HASH
   LOCAL aMatrix := {{1, 2}, {3, 4}, {5, 6}} AS ARRAY
   LOCAL aEmpty := {} AS ARRAY
   LOCAL nTotal := 0 AS NUMERIC
   LOCAL nLen AS NUMERIC
   LOCAL nPos AS NUMERIC
   LOCAL i AS NUMERIC
   LOCAL cName AS STRING
   LOCAL nItem AS NUMERIC
   LOCAL cText := "  Hello World  " AS STRING
   LOCAL cUpper AS STRING
   LOCAL cTrimmed AS STRING
   LOCAL cSub AS STRING

   // Array subscript access
   nTotal := aNumbers[1] + aNumbers[3] + aNumbers[5]
   QOut("nTotal=" + Str(nTotal))

   // Array subscript assignment
   aNumbers[2] := 99
   QOut("aNumbers[2]=" + Str(aNumbers[2]))

   // Array length
   nLen := Len(aNumbers)
   QOut("nLen=" + Str(nLen))

   // FOR loop with array subscript
   nTotal := 0
   FOR i := 1 TO Len(aNumbers)
      nTotal += aNumbers[i]
   NEXT

   QOut("nTotal=" + Str(nTotal))

   // FOR EACH over array
   nTotal := 0
   FOR EACH nItem IN aNumbers
      nTotal += nItem
   NEXT

   QOut("nTotal=" + Str(nTotal))

   // Nested array access
   nTotal := aMatrix[2][1]
   QOut("nTotal=" + Str(nTotal))

   // Hash access
   cName := hPerson["name"]
   QOut("cName=" + cName)

   // Hash assignment
   hPerson["age"] := 31
   QOut("age=" + Str(hPerson["age"]))

   // String operations via mapped functions
   cUpper := Upper(cText)
   QOut("cUpper=" + cUpper)
   cTrimmed := AllTrim(cText)
   QOut("cTrimmed=" + cTrimmed)
   cSub := SubStr(cText, 3, 5)
   QOut("cSub=" + cSub)
   nPos := Len(cText)
   QOut("nPos=" + Str(nPos))

RETURN nPos
