#include "astype.ch"
#include "hbclass.ch"

CLASS MyObj


   DATA nValue AS INTEGER INIT 0

   METHOD New()
   METHOD SetValue()

ENDCLASS

METHOD New() AS OBJECT CLASS MyObj
   ::nValue := 0
RETURN Self


METHOD SetValue( nVal AS NUMERIC ) AS OBJECT CLASS MyObj
   ::nValue := nVal
RETURN Self


FUNCTION Main() AS INTEGER

   LOCAL oObj := MyObj():New() AS OBJECT
   STATIC nCounter := 0 AS INTEGER

   // WITH OBJECT test
   WITH OBJECT oObj
      :SetValue(42)
   END WITH

   nCounter += 1

RETURN oObj:nValue

