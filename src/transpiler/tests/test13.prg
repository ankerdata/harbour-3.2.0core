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

RETURN nSum

FUNCTION FindFirst( aItems, cTarget )

   LOCAL cItem
   LOCAL nPos := 0
   LOCAL i

   // Nested FOR with EXIT
   FOR i := 1 TO Len( aItems )
      IF aItems[ i ] == cTarget
         nPos := i
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
            EXIT
         ELSEIF nX > 10
            DO CASE
               CASE nX > 50
                  cResult := "large"
               CASE nX > 25
                  cResult := "medium"
               OTHERWISE
                  cResult := "smallish"
            ENDCASE
            EXIT
         ELSE
            nX *= 2
         ENDIF
      ENDDO
   ELSE
      cResult := "non-positive"
   ENDIF

RETURN cResult

PROCEDURE Main()

   ? Classify( -5 )
   ? Classify( 0 )
   ? Classify( 42 )
   ? SumEvenTo( 10 )
   ? FindFirst( { "a", "b", "c" }, "b" )
   ? DeepNest( 5 )
   ? DeepNest( 30 )
   ? DeepNest( 200 )

RETURN
