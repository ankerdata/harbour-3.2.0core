#include "astype.ch"
// Test 10: CLASS methods, STATIC local, WITH OBJECT
#include "hbclass.ch"

CLASS MyObj


   DATA nValue AS NUMERIC INIT 0

   METHOD New()
   METHOD SetValue()

ENDCLASS

METHOD New() AS OBJECT CLASS MyObj
   ::nValue := 0
RETURN Self

METHOD SetValue( nVal AS NUMERIC ) AS OBJECT CLASS MyObj
   ::nValue := nVal
   QOut("nValue=" + Str(::nValue))
RETURN Self

FUNCTION Main() AS NUMERIC

   LOCAL oObj := MyObj():New()
   STATIC nCounter := 0 AS NUMERIC

   // WITH OBJECT test
   WITH OBJECT oObj
      :SetValue(42)
   END WITH

   nCounter += 1
   QOut("nCounter=" + Str(nCounter))

RETURN oObj:nValue
