#include "astype.ch"
#include "hbclass.ch"

CLASS Calculator


   DATA nResult AS INTEGER INIT 0

   METHOD New()
   METHOD Add( nValue )
   METHOD GetResult()
   METHOD Reset()
   METHOD Display( cLabel )

ENDCLASS

METHOD New() AS OBJECT CLASS Calculator
   ::nResult := 0
RETURN Self


METHOD Add( nValue AS NUMERIC ) AS OBJECT CLASS Calculator
   ::nResult := ::nResult + nValue
RETURN Self


METHOD GetResult() AS INTEGER CLASS Calculator
RETURN ::nResult


PROCEDURE Reset() CLASS Calculator
   ::nResult := 0
RETURN


PROCEDURE Display( cLabel AS STRING ) CLASS Calculator
   QOut(cLabel + ": " + Str(::nResult))
RETURN


FUNCTION CalcTotal( nA AS NUMERIC, nB AS NUMERIC, nC AS NUMERIC ) AS NUMERIC
   LOCAL nResult := nA + nB AS NUMERIC
   nResult += nC
RETURN nResult


FUNCTION FormatPrice( nPrice AS NUMERIC, cCurrency AS STRING )
   LOCAL nFinal := nPrice * 1.15 AS NUMERIC
RETURN cCurrency + " " + Str(nFinal)

