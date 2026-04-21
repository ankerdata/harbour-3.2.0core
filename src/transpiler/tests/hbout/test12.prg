#include "astype.ch"
// Test 12: Expressions — IIF, codeblocks, compound assignment, logical operators

FUNCTION Main() AS STRING

   LOCAL nX := 10 AS NUMERIC
   LOCAL nY := 20 AS NUMERIC
   LOCAL cResult AS STRING
   LOCAL lFlag := .T. AS LOGICAL
   LOCAL lOther := .F. AS LOGICAL
   LOCAL nVal AS NUMERIC
   LOCAL bAdd := {|a, b| a + b} AS BLOCK

   // Compound assignment operators
   nX += 5
   QOut("nX += 5: " + Str(nX, 10, 2))
   nX -= 3
   QOut("nX -= 3: " + Str(nX, 10, 2))
   nX *= 2
   QOut("nX *= 2: " + Str(nX, 10, 2))
   nX /= 4
   QOut("nX /= 4: " + Str(nX, 10, 2))

   // Logical operators
   IF lFlag .AND. !lOther
      cResult := "both true"
      QOut("cResult=" + cResult)
   ENDIF

   IF lFlag .OR. lOther
      cResult := "at least one"
      QOut("cResult=" + cResult)
   ENDIF

   // IIF expression
   cResult := IIF(nX > 10, "big", "small")
   QOut("cResult=" + cResult)

   // Nested IIF
   nVal := IIF(nX > 100, 3, IIF(nX > 10, 2, 1))
   QOut("nVal=" + Str(nVal, 10, 2))

   // Pre/post increment
   nX++
   QOut("nX++: " + Str(nX, 10, 2))
   ++nY
   QOut("++nY: " + Str(nY, 10, 2))
   nX--
   QOut("nX--: " + Str(nX, 10, 2))
   --nY
   QOut("--nY: " + Str(nY, 10, 2))

   // Negation
   nVal := -nX
   QOut("nVal=-nX: " + Str(nVal, 10, 2))

   // NOT operator
   lFlag := !lOther
   QOut("lFlag=" + IIF(lFlag, ".T.", ".F."))

   // Comparison operators
   IF nX == nY
      cResult := "equal"
      QOut("cResult=" + cResult)
   ENDIF

   IF nX != nY
      cResult := "not equal"
      QOut("cResult=" + cResult)
   ENDIF

   IF nX < nY .AND. nX >= 0
      cResult := "in range"
      QOut("cResult=" + cResult)
   ENDIF

RETURN cResult
