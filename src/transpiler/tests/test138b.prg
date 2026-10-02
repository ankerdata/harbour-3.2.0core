// Test 138 (multi-file pair): the classes, in a file the scan reaches after
// their users (test138a.prg says why).

#include "../include/astype.ch"
#include "hbclass.ch"

CLASS Line138
   VAR nKind INIT 0
ENDCLASS

CLASS PrintLine138 INHERIT Line138
   VAR nValue INIT 0
ENDCLASS

CLASS FcnLine138 INHERIT PrintLine138
   METHOD New( nValue ) CONSTRUCTOR
ENDCLASS

METHOD New( nValue ) CLASS FcnLine138
   ::nKind := 1
   ::nValue := nValue
   RETURN Self

CLASS GenLine138 INHERIT PrintLine138
   METHOD New( nValue ) CONSTRUCTOR
ENDCLASS

METHOD New( nValue ) CLASS GenLine138
   ::nKind := 2
   ::nValue := nValue
   RETURN Self

CLASS Till138
   VAR aLines AS ARRAY OF CLASS Line138 INIT {}
ENDCLASS

PROCEDURE Main()
   LOCAL oTill138 := Till138():New()
   AAdd( oTill138:aLines, FcnLine138():New( 5 ) )
   AAdd( oTill138:aLines, GenLine138():New( 7 ) )
   ? FirstTotal138( oTill138 ), SumValues138( oTill138 )
   ? LaterTotal138( FcnLine138():New( 2 ), GenLine138():New( 3 ) )
   RETURN
