// Test 43: multiple classes in one .prg file.

#include "hbclass.ch"

CLASS Foo
   VAR nA AS NUMERIC INIT 1
ENDCLASS

CLASS Bar
   VAR nB AS NUMERIC INIT 2
ENDCLASS

PROCEDURE Main()
   LOCAL oF := Foo():New()
   LOCAL oB := Bar():New()

   ? "Foo:nA = " + Str( oF:nA, 4 )
   ? "Bar:nB = " + Str( oB:nB, 4 )

RETURN
