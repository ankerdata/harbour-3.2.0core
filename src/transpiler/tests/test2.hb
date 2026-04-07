#include "astype.ch"
FUNCTION Main() AS STRING

   LOCAL nSum := 0 AS INTEGER
   LOCAL i AS INTEGER
   LOCAL cResult AS STRING

   FOR i := 1 TO 10
      nSum += i
   NEXT

   IF nSum > 50
      cResult := "big"
   ELSEIF nSum > 20
      cResult := "medium"
   ELSE
      cResult := "small"
   ENDIF

   DO WHILE nSum > 0
      nSum -= 10
      IF nSum < 0
         EXIT
      ENDIF
   ENDDO

RETURN cResult

