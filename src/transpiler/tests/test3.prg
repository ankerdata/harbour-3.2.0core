FUNCTION Main()

   LOCAL nChoice := 2
   LOCAL aItems := { "apple", "banana", "cherry" }
   LOCAL cItem
   LOCAL cResult := ""
   LOCAL oErr

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
      cResult := cResult + cItem
   NEXT

   // BEGIN SEQUENCE
   BEGIN SEQUENCE
      cResult := DoSomething()
   RECOVER USING oErr
      cResult := "error caught"
   ALWAYS
      CleanUp()
   END SEQUENCE

   ? "cResult=" + cResult

RETURN cResult

FUNCTION DoSomething()

   BREAK

RETURN NIL

FUNCTION CleanUp()

RETURN NIL
