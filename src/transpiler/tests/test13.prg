// Test 13: Nested control flow, multiple returns, LOOP

FUNCTION Classify( nVal )

   // Multiple return paths
   IF nVal < 0
      RETURN "negative"
   ELSEIF nVal == 0
      RETURN "zero"
   ENDIF

RETURN "positive"

FUNCTION SumEvenTo( nMax )

   LOCAL nSum := 0
   LOCAL i

   FOR i := 1 TO nMax
      // LOOP = continue
      IF i % 2 != 0
         LOOP
      ENDIF
      nSum += i
   NEXT
   ? "nSum=" + Str( nSum )

RETURN nSum

FUNCTION FindFirst( aItems, cTarget )

   LOCAL cItem
   LOCAL nPos := 0
   LOCAL i

   // Nested FOR with EXIT
   FOR i := 1 TO Len( aItems )
      IF aItems[ i ] == cTarget
         nPos := i
         ? "nPos=" + Str( nPos )
         EXIT
      ENDIF
   NEXT

RETURN nPos

FUNCTION DeepNest( nX )

   LOCAL cResult := ""

   // Nested IF inside DO WHILE inside IF
   IF nX > 0
      DO WHILE nX > 0
         IF nX > 100
            cResult := "huge"
            ? "cResult=" + cResult
            EXIT
         ELSEIF nX > 10
            DO CASE
               CASE nX > 50
                  cResult := "large"
                  ? "cResult=" + cResult
               CASE nX > 25
                  cResult := "medium"
                  ? "cResult=" + cResult
               OTHERWISE
                  cResult := "smallish"
                  ? "cResult=" + cResult
            ENDCASE
            EXIT
         ELSE
            nX *= 2
            ? "nX=" + Str( nX )
         ENDIF
      ENDDO
   ELSE
      cResult := "non-positive"
      ? "cResult=" + cResult
   ENDIF

RETURN cResult

PROCEDURE Main()

   ? "Classify(-5)=" + Classify( -5 )
   ? "Classify(0)=" + Classify( 0 )
   ? "Classify(42)=" + Classify( 42 )
   ? "SumEvenTo(10)=" + Str( SumEvenTo( 10 ) )
   ? "FindFirst=" + Str( FindFirst( { "a", "b", "c" }, "b" ) )
   ? "DeepNest(5)=" + DeepNest( 5 )
   ? "DeepNest(30)=" + DeepNest( 30 )
   ? "DeepNest(200)=" + DeepNest( 200 )

RETURN
