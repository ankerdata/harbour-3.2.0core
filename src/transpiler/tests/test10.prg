// Test 10: CLASS methods, STATIC local, WITH OBJECT
#include "hbclass.ch"

CLASS MyObj

   DATA nValue INIT 0

   METHOD New()
   METHOD SetValue()

ENDCLASS

METHOD New() CLASS MyObj
   ::nValue := 0
RETURN Self

METHOD SetValue( nVal ) CLASS MyObj
   ::nValue := nVal
   ? "nValue=" + Str( ::nValue )
RETURN Self

FUNCTION Main()

   LOCAL oObj := MyObj():New()
   STATIC nCounter := 0

   // WITH OBJECT test
   WITH OBJECT oObj
      :SetValue( 42 )
   END WITH

   nCounter := nCounter + 1
   ? "nCounter=" + Str( nCounter )

RETURN oObj:nValue
