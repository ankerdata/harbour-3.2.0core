#include "astype.ch"
// Test 43: multiple classes in one .prg file.

#include "hbclass.ch"

CLASS Foo

   DATA nA AS NUMERIC INIT 1

ENDCLASS

CLASS Bar

   DATA nB AS NUMERIC INIT 2

ENDCLASS

PROCEDURE Main()
   LOCAL oF := Foo():New() AS OBJECT
   LOCAL oB := Bar():New() AS OBJECT

   QOut("Foo:nA = " + Str(oF:nA, 4))
   QOut("Bar:nB = " + Str(oB:nB, 4))

RETURN
