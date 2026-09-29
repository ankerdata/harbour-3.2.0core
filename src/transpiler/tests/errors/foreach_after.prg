// W0038: a FOR EACH variable read after its loop. Harbour restores the
// variable when the loop ends, so after NEXT it holds its value from
// before the loop, where C# keeps the last element. "Find it, EXIT, use
// it" reads NIL in Harbour. The lines marked "quiet" must not warn.

FUNCTION Find38( aNames38, cWant )
   LOCAL cName
   FOR EACH cName IN aNames38
      IF cName == cWant
         EXIT
      ENDIF
   NEXT
   RETURN cName                          // fires: Harbour returns NIL here

FUNCTION Count38( aNames38 )
   LOCAL cName
   LOCAL nCount := 0
   FOR EACH cName IN aNames38
      nCount++
   NEXT
   cName := "done"                       // quiet: assigned before any read
   ? cName
   FOR EACH cName IN aNames38            // quiet: a new loop binds it again
      nCount++
   NEXT
   RETURN nCount

FUNCTION Nested38( aRows38 )
   LOCAL aRow
   LOCAL cCell
   LOCAL cLast := ""
   FOR EACH aRow IN aRows38
      FOR EACH cCell IN aRow
         cLast := cCell
      NEXT
      IF ! Empty( cCell )                // fires: read after the inner loop
         cLast += "!"
      ENDIF
   NEXT
   RETURN cLast
