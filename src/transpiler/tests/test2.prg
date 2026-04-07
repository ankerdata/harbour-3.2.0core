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

   ? "nSum after FOR=" + Str( nSum )
   ? "cResult=" + cResult

   DO WHILE nSum > 0
      nSum := nSum - 10
      IF nSum < 0
         EXIT
      ENDIF
   ENDDO

   ? "nSum after WHILE=" + Str( nSum )

RETURN cResult
