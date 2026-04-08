FUNCTION Main()

   LOCAL nSum := 0
   LOCAL i
   LOCAL cResult

   FOR i := 1 TO 10
      nSum := nSum + i
   NEXT
   ? "nSum=" + Str( nSum )

   IF nSum > 50
      cResult := "big"
      ? "cResult=" + cResult
   ELSEIF nSum > 20
      cResult := "medium"
      ? "cResult=" + cResult
   ELSE
      cResult := "small"
      ? "cResult=" + cResult
   ENDIF

   DO WHILE nSum > 0
      nSum := nSum - 10
      IF nSum < 0
         EXIT
      ENDIF
   ENDDO
   ? "nSum=" + Str( nSum )

RETURN cResult
