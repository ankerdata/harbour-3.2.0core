#include "astype.ch"
// Test 2: FUNCTION, FOR/NEXT, IF/ELSEIF/ELSE, DO WHILE, EXIT
FUNCTION Main() AS STRING

   LOCAL nSum := 0 AS NUMERIC
   LOCAL i AS NUMERIC
   LOCAL cResult AS STRING

   FOR i := 1 TO 10
      nSum += i
   NEXT

   QOut("nSum=" + Str(nSum))

   IF nSum > 50
      cResult := "big"
      QOut("cResult=" + cResult)
   ELSEIF nSum > 20
      cResult := "medium"
      QOut("cResult=" + cResult)
   ELSE
      cResult := "small"
      QOut("cResult=" + cResult)
   ENDIF

   DO WHILE nSum > 0
      nSum -= 10
      IF nSum < 0
         EXIT
      ENDIF
   ENDDO

   QOut("nSum=" + Str(nSum))

RETURN cResult
