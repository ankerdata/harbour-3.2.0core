FUNCTION Main()
   
   LOCAL nSum := 0
   LOCAL i
   LOCAL cResult

   FOR i := 1 TO 10
      nSum := nSum + i
   NEXT

   IF nSum > 50
      cResult := "big"
   ELSEIF nSum > 20
      cResult := "medium"
   ELSE
      cResult := "small"
   ENDIF

   DO WHILE nSum > 0
      nSum := nSum - 10
      IF nSum < 0
         EXIT
      ENDIF
   ENDDO

RETURN cResult
