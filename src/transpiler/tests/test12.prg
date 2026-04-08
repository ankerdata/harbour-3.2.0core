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
   ? "nX += 5: " + Str( nX )
   nX -= 3
   ? "nX -= 3: " + Str( nX )
   nX *= 2
   ? "nX *= 2: " + Str( nX )
   nX /= 4
   ? "nX /= 4: " + Str( nX )

   // Logical operators
   IF lFlag .AND. .NOT. lOther
      cResult := "both true"
      ? "cResult=" + cResult
   ENDIF

   IF lFlag .OR. lOther
      cResult := "at least one"
      ? "cResult=" + cResult
   ENDIF

   // IIF expression
   cResult := IIF( nX > 10, "big", "small" )
   ? "cResult=" + cResult

   // Nested IIF
   nVal := IIF( nX > 100, 3, IIF( nX > 10, 2, 1 ) )
   ? "nVal=" + Str( nVal )

   // Pre/post increment
   nX++
   ? "nX++: " + Str( nX )
   ++nY
   ? "++nY: " + Str( nY )
   nX--
   ? "nX--: " + Str( nX )
   --nY
   ? "--nY: " + Str( nY )

   // Negation
   nVal := -nX
   ? "nVal=-nX: " + Str( nVal )

   // NOT operator
   lFlag := !lOther
   ? "lFlag=" + IIF( lFlag, ".T.", ".F." )

   // Comparison operators
   IF nX == nY
      cResult := "equal"
      ? "cResult=" + cResult
   ENDIF
   IF nX != nY
      cResult := "not equal"
      ? "cResult=" + cResult
   ENDIF
   IF nX < nY .AND. nX >= 0
      cResult := "in range"
      ? "cResult=" + cResult
   ENDIF

RETURN cResult
