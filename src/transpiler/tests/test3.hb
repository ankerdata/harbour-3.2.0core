#include "astype.ch"
FUNCTION Main() AS STRING

   LOCAL nChoice := 2 AS INTEGER
   LOCAL aItems := {"apple", "banana", "cherry"} AS ARRAY
   LOCAL cItem AS STRING
   LOCAL cResult := "" AS STRING
   LOCAL oErr AS OBJECT

   // DO CASE
   DO CASE
      CASE nChoice == 1
         cResult := "first"
      CASE nChoice == 2
         cResult := "second"
      OTHERWISE
         cResult := "other"
   ENDCASE

   // FOR EACH
   FOR EACH cItem IN aItems
      cResult += cItem
   NEXT

   // BEGIN SEQUENCE
   BEGIN SEQUENCE
      cResult := DoSomething()
   RECOVER USING oErr
      cResult := "error caught"
   ALWAYS
      CleanUp()
   END SEQUENCE

RETURN cResult


FUNCTION DoSomething()

   BREAK

RETURN NIL


FUNCTION CleanUp()

RETURN NIL

