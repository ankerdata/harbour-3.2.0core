// Test 180: a function pointer declared `AS FUNCTION Name` is a delegate of
// Name's signature: a parameter and a local. `@Other()` given to it is a
// lambda that calls Other, `f:exec( ... )` is a direct call, and a
// parameter the call leaves out is passed as its blank, a delegate having
// no optional parameters. A pointer with no declaration is the untyped
// one, called through Eval().

#include "../include/astype.ch"

PROCEDURE Main()

   LOCAL fPick AS FUNCTION Double180
   LOCAL fAny := @Triple180()

   ? Apply180( @Double180(), 4 ), Apply180( @Triple180(), 4 )
   fPick := @Triple180()
   ? fPick:exec( 5, "x" ), fPick:exec( 6 )
   fPick := @Double180()
   ? fPick:exec( 7, "y" ), fAny:exec( 8, "z" )
   Each180( @Show180(), { "a", "b" } )

   RETURN

FUNCTION Double180( iValue, cTag )

RETURN LTrim( Str( iValue * 2 ) ) + iif( Empty( cTag ), "", cTag )

FUNCTION Triple180( iValue, cTag )

RETURN LTrim( Str( iValue * 3 ) ) + iif( Empty( cTag ), "", cTag )

FUNCTION Apply180( fCalc AS FUNCTION Double180, iValue )

RETURN fCalc:exec( iValue, "!" )

PROCEDURE Show180( cText )

   ? "show:", cText

   RETURN

PROCEDURE Each180( fShow AS FUNCTION Show180, acTexts )

   LOCAL cText

   FOR EACH cText IN acTexts
      fShow:exec( cText )
   NEXT

   RETURN
