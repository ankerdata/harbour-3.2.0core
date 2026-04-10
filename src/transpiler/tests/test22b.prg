// Test 22b: companion to test22a — second class also has an `Add`
// method, but with a STRING parameter instead of NUMERIC. Plus the
// Main that exercises both. See test22a.prg for the rationale.
#include "hbclass.ch"

CLASS Names
   DATA cAll INIT ""
   METHOD New()
   METHOD Add( cValue )
   METHOD Show()
ENDCLASS

METHOD New() CLASS Names
   ::cAll := ""
RETURN Self

METHOD Add( cValue ) CLASS Names
   ::cAll := ::cAll + cValue + " "
RETURN Self

METHOD Show() CLASS Names
   ? "all=" + ::cAll
RETURN Self

PROCEDURE Main()
   LOCAL oNums  := Numbers():New()
   LOCAL oNames := Names():New()

   oNums:Add( 10 )
   oNums:Add( 20 )
   oNums:Add( 30 )
   oNums:Show()

   oNames:Add( "alpha" )
   oNames:Add( "beta" )
   oNames:Add( "gamma" )
   oNames:Show()

RETURN
