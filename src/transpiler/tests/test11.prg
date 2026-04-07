#include "hbclass.ch"

CLASS Calculator

   DATA nResult INIT 0

   METHOD New()
   METHOD Add( nValue )
   METHOD GetResult()
   METHOD Reset()
   METHOD Display( cLabel )

ENDCLASS

METHOD New() CLASS Calculator
   ::nResult := 0
RETURN Self

METHOD Add( nValue ) CLASS Calculator
   ::nResult := ::nResult + nValue
RETURN Self

METHOD GetResult() CLASS Calculator
RETURN ::nResult

PROCEDURE Reset() CLASS Calculator
   ::nResult := 0
RETURN

PROCEDURE Display( cLabel ) CLASS Calculator
   QOut( cLabel + ": " + Str( ::nResult ) )
RETURN

FUNCTION CalcTotal( nA, nB, nC )
   LOCAL nResult := nA + nB
   nResult := nResult + nC
RETURN nResult

FUNCTION FormatPrice( nPrice, cCurrency )
   LOCAL nFinal := nPrice * 1.15
RETURN cCurrency + " " + Str( nFinal )

PROCEDURE Main()

   LOCAL oCalc := Calculator():New()

   oCalc:Add( 10 )
   oCalc:Add( 20 )
   oCalc:Display( "Total" )

   ? CalcTotal( 1, 2, 3 )
   ? FormatPrice( 9.99, "$" )

RETURN
