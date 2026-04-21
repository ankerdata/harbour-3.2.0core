#include "astype.ch"
// Test 22b: companion to test22a — second class also has an `Add`
// method, but with a STRING parameter instead of NUMERIC. Plus the
// Main that exercises both. See test22a.prg for the rationale.
#include "hbclass.ch"

CLASS Names

   DATA cAll AS STRING INIT ""
   METHOD New()
   METHOD Add( cValue )
   METHOD Show()

ENDCLASS

METHOD New() AS OBJECT CLASS Names
   ::cAll := ""
RETURN Self

METHOD Add( cValue AS STRING ) AS OBJECT CLASS Names
   ::cAll := ::cAll + cValue + " "
RETURN Self

METHOD Show() AS OBJECT CLASS Names
   QOut("all=" + ::cAll)
RETURN Self

PROCEDURE Main()
   LOCAL oNums := Numbers():New() AS OBJECT
   LOCAL oNames := Names():New() AS OBJECT

   oNums:Add(10)
   oNums:Add(20)
   oNums:Add(30)
   oNums:Show()

   oNames:Add("alpha")
   oNames:Add("beta")
   oNames:Add("gamma")
   oNames:Show()

RETURN
