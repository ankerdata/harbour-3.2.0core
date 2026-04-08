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
         ? "cResult=" + cResult
      CASE nChoice == 2
         cResult := "second"
         ? "cResult=" + cResult
      OTHERWISE
         cResult := "other"
         ? "cResult=" + cResult
   ENDCASE

   // FOR EACH
   FOR EACH cItem IN aItems
      cResult := cResult + cItem
   NEXT
   ? "cResult=" + cResult

   // BEGIN SEQUENCE
   BEGIN SEQUENCE
      cResult := DoSomething()
      ? "cResult=" + cResult
   RECOVER USING oErr
      cResult := "error caught"
      ? "cResult=" + cResult
   ALWAYS
      CleanUp()
   END SEQUENCE

RETURN cResult

FUNCTION DoSomething()

   BREAK

RETURN NIL

FUNCTION CleanUp()

RETURN NIL
