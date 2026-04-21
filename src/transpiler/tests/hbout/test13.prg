#include "astype.ch"
// Test 13: Nested control flow, multiple returns, LOOP

FUNCTION Classify( nVal AS NUMERIC ) AS STRING

   // Multiple return paths
   IF nVal < 0
   RETURN "negative"
   ELSEIF nVal == 0
   RETURN "zero"
   ENDIF

RETURN "positive"

FUNCTION SumEvenTo( nMax AS NUMERIC ) AS NUMERIC

   LOCAL nSum := 0 AS NUMERIC
   LOCAL i AS NUMERIC

   FOR i := 1 TO nMax
      // LOOP = continue
      IF i % 2 != 0
         LOOP
      ENDIF

      nSum += i
   NEXT

   QOut("nSum=" + Str(nSum))

RETURN nSum

FUNCTION FindFirst( aItems AS ARRAY, cTarget AS STRING ) AS NUMERIC

   LOCAL cItem AS STRING
   LOCAL nPos := 0 AS NUMERIC
   LOCAL i AS NUMERIC

   // Nested FOR with EXIT
   FOR i := 1 TO Len(aItems)
      IF aItems[i] == cTarget
         nPos := i
         QOut("nPos=" + Str(nPos))
         EXIT
      ENDIF
   NEXT

RETURN nPos

FUNCTION DeepNest( nX AS NUMERIC ) AS STRING

   LOCAL cResult := "" AS STRING

   // Nested IF inside DO WHILE inside IF
   IF nX > 0
      DO WHILE nX > 0
         IF nX > 100
            cResult := "huge"
            QOut("cResult=" + cResult)
            EXIT
         ELSEIF nX > 10
            DO CASE
               CASE nX > 50
                  cResult := "large"
                  QOut("cResult=" + cResult)
               CASE nX > 25
                  cResult := "medium"
                  QOut("cResult=" + cResult)
               OTHERWISE
                  cResult := "smallish"
                  QOut("cResult=" + cResult)
            ENDCASE

            EXIT
         ELSE
            nX *= 2
            QOut("nX=" + Str(nX))
         ENDIF
      ENDDO
   ELSE
      cResult := "non-positive"
      QOut("cResult=" + cResult)
   ENDIF

RETURN cResult

PROCEDURE Main()

   QOut("Classify(-5)=" + Classify(-5))
   QOut("Classify(0)=" + Classify(0))
   QOut("Classify(42)=" + Classify(42))
   QOut("SumEvenTo(10)=" + Str(SumEvenTo(10)))
   QOut("FindFirst=" + Str(FindFirst({"a", "b", "c"}, "b")))
   QOut("DeepNest(5)=" + DeepNest(5))
   QOut("DeepNest(30)=" + DeepNest(30))
   QOut("DeepNest(200)=" + DeepNest(200))

RETURN
