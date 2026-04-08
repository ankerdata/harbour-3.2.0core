#include "astype.ch"
// Test 11: CLASS with METHOD/PROCEDURE, standalone FUNCTIONs, constructor pattern
#include "hbclass.ch"

CLASS Calculator


   DATA nResult AS NUMERIC INIT 0

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
   QOut("nResult=" + Str(::nResult))
RETURN Self

METHOD GetResult() AS NUMERIC CLASS Calculator
RETURN ::nResult

PROCEDURE Reset() CLASS Calculator
   ::nResult := 0
   QOut("nResult=" + Str(::nResult))
RETURN

PROCEDURE Display( cLabel AS STRING ) CLASS Calculator
   QOut(cLabel + ": " + Str(::nResult))
RETURN

FUNCTION CalcTotal( nA AS NUMERIC, nB AS NUMERIC, nC AS NUMERIC ) AS NUMERIC
   LOCAL nResult := nA + nB AS NUMERIC
   QOut("nResult=" + Str(nResult))
   nResult += nC
   QOut("nResult=" + Str(nResult))
RETURN nResult

FUNCTION FormatPrice( nPrice AS NUMERIC, cCurrency AS STRING ) AS STRING
   LOCAL nFinal := nPrice * 1.15 AS NUMERIC
   QOut("nFinal=" + Str(nFinal, 10, 2))
RETURN cCurrency + " " + Str(nFinal, 10, 2)

PROCEDURE Main()

   LOCAL oCalc := Calculator():New() AS OBJECT

   oCalc:Add(10)
   oCalc:Add(20)
   oCalc:Display("Total")

   QOut("CalcTotal=" + Str(CalcTotal(1, 2, 3)))
   QOut("FormatPrice=" + FormatPrice(9.99, "$"))

RETURN
