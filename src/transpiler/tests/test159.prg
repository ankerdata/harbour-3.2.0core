// Test 159: a LOCAL with no initialiser is `T name;` where C# sees it
// assigned before every read, and keeps `= default` (NIL) where a read may
// come first.
//
// Assigned first, both branches of an IF, every CASE with an OTHERWISE, a
// FOR's counter, a codeblock created after the assignment: no default.
// An IF without ELSE, a loop body's assignment read after the loop, a
// SEQUENCE body's read after END, `@x` (C# `ref` needs it assigned) and
// an assignment in the right operand of .AND.: a default. The default is
// the type's value written out: 0, false, null, and a date's `default`.

PROCEDURE Main()

   ? Assigned159( .T. ), Assigned159( .F. )
   ? Branches159( 1 ), Branches159( 2 ), Branches159( 3 )
   ? Loops159( 3 ), Loops159( 0 )
   ? Blocks159()
   ? Sequence159()
   ? ByRef159()
   ? AndRight159( .T. ), AndRight159( .F. )
   ? Defaults159( .T. ), Defaults159( .F. )

   RETURN

FUNCTION Assigned159( lFlag )

   LOCAL cName
   LOCAL nCount

   cName := "first"
   nCount := Len( cName )
   IF lFlag
      cName += "!"
   ENDIF

   RETURN cName + Str( nCount, 2 )

FUNCTION Branches159( nKind )

   LOCAL cBoth
   LOCAL cCase
   LOCAL cMaybe

   IF nKind == 1
      cBoth := "one"
   ELSE
      cBoth := "other"
   ENDIF
   DO CASE
   CASE nKind == 1
      cCase := "a"
   CASE nKind == 2
      cCase := "b"
   OTHERWISE
      cCase := "c"
   ENDCASE
   IF nKind == 3
      cMaybe := "three"
   ENDIF

   RETURN cBoth + cCase + iif( cMaybe == NIL, "-", cMaybe )

FUNCTION Loops159( nTimes )

   LOCAL i
   LOCAL nSum
   LOCAL xLast

   nSum := 0
   FOR i := 1 TO nTimes
      xLast := i
      nSum += xLast
   NEXT

   RETURN Str( nSum, 3 ) + iif( xLast == NIL, " none", Str( xLast, 3 ) )

FUNCTION Blocks159()

   LOCAL nBase
   LOCAL bAdd

   nBase := 10
   bAdd := {| n | n + nBase }

   RETURN Str( Eval( bAdd, 5 ), 3 )

FUNCTION Sequence159()

   LOCAL cDone
   LOCAL cAfter

   BEGIN SEQUENCE
      cDone := "body"
   END SEQUENCE
   cAfter := "after"

   RETURN iif( cDone == NIL, "nil", cDone ) + " " + cAfter

FUNCTION ByRef159()

   LOCAL xValue

   Bump159( @xValue )

   RETURN Str( xValue, 2 )

PROCEDURE Bump159( xCount )

   xCount := iif( xCount == NIL, 1, xCount + 1 )

   RETURN

FUNCTION AndRight159( lGo )

   LOCAL cSet

   IF lGo .AND. ( cSet := "set" ) == "set"
      RETURN cSet
   ENDIF

   RETURN "no"

FUNCTION Defaults159( lSet )

   LOCAL nCount
   LOCAL iTimes
   LOCAL lSeen
   LOCAL dWhen
   LOCAL cName
   LOCAL aRows
   LOCAL hMap

   IF lSet
      nCount := 1
      iTimes := 2
      lSeen := .T.
      dWhen := Date()
      cName := "set"
      aRows := { 1 }
      hMap := { "k" => 1 }
   ENDIF

   RETURN iif( Empty( nCount ), "e", "s" ) + iif( Empty( iTimes ), "e", "s" ) + ;
      iif( Empty( lSeen ), "e", "s" ) + iif( Empty( dWhen ), "e", "s" ) + ;
      iif( Empty( cName ), "e", "s" ) + iif( Empty( aRows ), "e", "s" ) + ;
      iif( Empty( hMap ), "e", "s" )
