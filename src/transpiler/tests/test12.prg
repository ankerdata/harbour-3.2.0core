// Test 12: Expressions — IIF, codeblocks, compound assignment, logical operators

FUNCTION Main()

   LOCAL nX := 10
   LOCAL nY := 20
   LOCAL cResult
   LOCAL lFlag := .T.
   LOCAL lOther := .F.
   LOCAL nVal
   LOCAL bAdd := {|a, b| a + b }

   // Compound assignment operators
   nX += 5
   nX -= 3
   nX *= 2
   nX /= 4

   // Logical operators
   IF lFlag .AND. .NOT. lOther
      cResult := "both true"
   ENDIF

   IF lFlag .OR. lOther
      cResult := "at least one"
   ENDIF

   // IIF expression
   cResult := IIF( nX > 10, "big", "small" )

   // Nested IIF
   nVal := IIF( nX > 100, 3, IIF( nX > 10, 2, 1 ) )

   // Pre/post increment
   nX++
   ++nY
   nX--
   --nY

   // Negation
   nVal := -nX

   // NOT operator
   lFlag := !lOther

   // Comparison operators
   IF nX == nY
      cResult := "equal"
   ENDIF
   IF nX != nY
      cResult := "not equal"
   ENDIF
   IF nX < nY .AND. nX >= 0
      cResult := "in range"
   ENDIF

   ? "nX=" + Str( nX )
   ? "nY=" + Str( nY )
   ? "nVal=" + Str( nVal )
   ? "cResult=" + cResult
   ? "lFlag=" + IIF( lFlag, ".T.", ".F." )

RETURN cResult
