// Test 161: a LOCAL is declared in the innermost block that holds all its
// uses, just before the first, where the C# runs the same; joined to its
// first assignment; a FOR's counter in the loop's header; a FOR EACH
// variable as the foreach's own.
//
// Moved: a constant-initialised local first read in a branch; one first
// assigned there (one declaration, `var` for a `new` of its type); one
// first assigned in a loop's body; one used in both branches of an inner
// IF (before that IF); a counter nothing after its loop reads; a FOR EACH
// variable the body only reads (the foreach's own) and one it assigns
// (declared from the element); a RECOVER USING variable (before the try);
// one first assigned at the routine's own level (declared there); one in
// a routine that reads `MEMVAR->x`, which names no LOCAL.
// At the top: a local a loop reads before assigning (Harbour keeps the
// last pass's value), one a codeblock made in a loop uses (each pass's
// block sees the one variable), one initialised by a call, a counter read
// after its loop.

PROCEDURE Main()

   ? Branch161( .T. ), Branch161( .F. )
   ? Loop161( 3 )
   ? Counter161( 4 )
   ? Each161( { "a", "bb", "ccc" } )
   ? Kept161( 3 )
   ? Recover161( .T. ), Recover161( .F. )
   ? Top161()
   ? Alias161( .T. ), Alias161( .F. )

   RETURN

FUNCTION Branch161( lGo )

   LOCAL nLimit := 10
   LOCAL cName
   LOCAL aList
   LOCAL cBoth
   LOCAL cOut := ""

   IF lGo
      cOut += Str( nLimit, 3 )
      cName := "go"
      aList := {}
      AAdd( aList, cName )
      cOut += cName + Str( Len( aList ), 2 )
   ENDIF
   IF Len( cOut ) >= 0
      IF lGo
         cBoth := " then"
      ELSE
         cBoth := " else"
      ENDIF
      cOut += cBoth
   ENDIF

   RETURN cOut

FUNCTION Loop161( nTimes )

   LOCAL i
   LOCAL nSquare
   LOCAL nLast
   LOCAL nSum := 0

   FOR i := 1 TO nTimes
      nSquare := i * i
      nSum += nSquare
      IF i > 1
         nSum += nLast
      ENDIF
      nLast := i
   NEXT

   RETURN nSum

FUNCTION Counter161( n )

   LOCAL i
   LOCAL j
   LOCAL cOut := ""

   FOR i := 1 TO n
      cOut += Str( i, 1 )
   NEXT
   FOR j := 1 TO n STEP 2
      cOut += "."
   NEXT

   RETURN cOut + Str( j, 2 )

FUNCTION Each161( aItems )

   LOCAL cItem
   LOCAL cWord
   LOCAL cOut := ""

   FOR EACH cItem IN aItems
      cOut += cItem
   NEXT
   FOR EACH cWord IN aItems
      cWord := Upper( cWord )
      cOut += cWord
   NEXT

   RETURN cOut

FUNCTION Kept161( n )

   LOCAL nStart := Len( "abc" )
   LOCAL nShared
   LOCAL aBlocks := {}
   LOCAL k
   LOCAL cOut := ""

   FOR k := 1 TO n
      nShared := k
      AAdd( aBlocks, {|| nShared } )
   NEXT
   IF n > 0
      cOut += Str( nStart, 2 )
   ENDIF
   AEval( aBlocks, {| b | cOut += Str( Eval( b ), 2 ) } )

   RETURN cOut

FUNCTION Recover161( lFail )

   LOCAL xValue
   LOCAL cOut := "none"

   IF lFail
      BEGIN SEQUENCE
         BREAK "boom"
      RECOVER USING xValue
         cOut := xValue
      END SEQUENCE
   ENDIF

   RETURN cOut

FUNCTION Top161()

   LOCAL cHead
   LOCAL nCount

   cHead := "top"
   nCount := Len( cHead )

   RETURN cHead + Str( nCount, 2 )

FUNCTION Alias161( lGo )

   LOCAL cOut := "-"
   LOCAL cName
   MEMVAR cShared161

   PUBLIC cShared161 := "pub"

   IF lGo
      cName := MEMVAR->cShared161
      cOut += cName
   ENDIF

   RETURN cOut
