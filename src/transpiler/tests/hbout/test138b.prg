#include "astype.ch"
// Test 138 (multi-file pair): the classes, in a file the scan reaches after
// their users (test138a.prg says why).

#include "../include/astype.ch"
#include "hbclass.ch"

CLASS Line138

   DATA nKind AS NUMERIC INIT 0

ENDCLASS

CLASS PrintLine138 INHERIT Line138

   DATA nValue AS NUMERIC INIT 0

ENDCLASS

CLASS FcnLine138 INHERIT PrintLine138

   METHOD New( nValue )

ENDCLASS

CLASS GenLine138 INHERIT PrintLine138

   METHOD New( nValue )

ENDCLASS

CLASS Till138

   DATA aLines AS ARRAY OF CLASS Line138 INIT {}

ENDCLASS

METHOD New( nValue AS NUMERIC ) AS OBJECT CLASS FcnLine138
   ::nKind := 1
   ::nValue := nValue
RETURN Self

METHOD New( nValue AS NUMERIC ) AS OBJECT CLASS GenLine138
   ::nKind := 2
   ::nValue := nValue
RETURN Self

PROCEDURE Main()
   LOCAL oTill138 := Till138():New() AS OBJECT
   AAdd(oTill138:aLines, FcnLine138():New(5))
   AAdd(oTill138:aLines, GenLine138():New(7))
   QOut(FirstTotal138(oTill138), SumValues138(oTill138))
   QOut(LaterTotal138(FcnLine138():New(2), GenLine138():New(3)))
RETURN
