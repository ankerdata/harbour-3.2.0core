// Test 173: a list of logicals is typed storage. AS ARRAY OF LOGICAL is a
// C# List<bool>, on a local, a member, a parameter and a function's
// result: its literal, AAdd(), an element read as a condition and an
// element written are checked by the C#, where a List<dynamic> made each
// read a run-time conversion.

#include "hbclass.ch"
#include "../include/astype.ch"

CLASS Switches173

   VAR aOn AS ARRAY OF LOGICAL INIT {}

   METHOD Add173( lOn )
   METHOD CountOn173()

ENDCLASS

METHOD Add173( lOn ) CLASS Switches173

   AAdd( ::aOn, lOn )

RETURN Self

METHOD CountOn173() AS INTEGER CLASS Switches173

   LOCAL lOn
   LOCAL iCount := 0

   FOR EACH lOn IN ::aOn
      IF lOn
         iCount++
      ENDIF
   NEXT

RETURN iCount

FUNCTION Flags173( lAll ) AS ARRAY OF LOGICAL
RETURN { lAll, .T., ! lAll }

FUNCTION AnyOn173( aFlags AS ARRAY OF LOGICAL ) AS LOGICAL

   LOCAL i

   FOR i := 1 TO Len( aFlags )
      IF aFlags[ i ]
         RETURN .T.
      ENDIF
   NEXT

RETURN .F.

PROCEDURE Main()

   LOCAL aFlags AS ARRAY OF LOGICAL := Flags173( .F. )
   LOCAL oSwitches173 := Switches173():New()

   ? Len( aFlags ), aFlags[ 1 ], aFlags[ 2 ], aFlags[ 3 ]
   aFlags[ 1 ] := .T.
   AAdd( aFlags, .F. )
   ? Len( aFlags ), aFlags[ 1 ] .AND. ! aFlags[ 4 ]
   ? AnyOn173( aFlags ), AnyOn173( { .F., .F. } )

   oSwitches173:Add173( .T. ):Add173( .F. ):Add173( aFlags[ 2 ] )
   ? oSwitches173:CountOn173()

RETURN
