#include "astype.ch"
// Test 3: DO CASE, FOR EACH, BEGIN SEQUENCE/RECOVER/ALWAYS, BREAK
FUNCTION Main() AS STRING

   LOCAL nChoice := 2 AS NUMERIC
   LOCAL aItems := {"apple", "banana", "cherry"} AS ARRAY
   LOCAL cItem AS STRING
   LOCAL cResult := "" AS STRING
   LOCAL oErr AS OBJECT

   // DO CASE
   DO CASE
      CASE nChoice == 1
         cResult := "first"
         QOut("cResult=" + cResult)
      CASE nChoice == 2
         cResult := "second"
         QOut("cResult=" + cResult)
      OTHERWISE
         cResult := "other"
         QOut("cResult=" + cResult)
   ENDCASE

   // FOR EACH
   FOR EACH cItem IN aItems
      cResult += cItem
   NEXT

   QOut("cResult=" + cResult)

   // BEGIN SEQUENCE
   BEGIN SEQUENCE
      cResult := DoSomething()
      QOut("cResult=" + cResult)
   RECOVER USING oErr
      cResult := "error caught"
      QOut("cResult=" + cResult)
   ALWAYS
      CleanUp()
   END SEQUENCE

RETURN cResult

FUNCTION DoSomething()

   BREAK

RETURN NIL

FUNCTION CleanUp()

RETURN NIL
