// Test 168: a list of records is typed storage. AS ARRAY OF RECORD X is a
// C# List<X>, as a list of strings is a List<string>: a record has no
// subclasses, so nothing a List<X> holds needs a cast. Its literal's rows
// and AAdd()'s row are the record's constructor, a parameter given one
// becomes one, a FOR EACH variable and an AScan() block's parameter are the
// record, and Array( n ) makes n empty places.

#include "../include/astype.ch"

#define PORT168_NAME   1
#define PORT168_SPEED  2
#define PORT168_OPEN   3

RECORD Port168( cName, iSpeed, lOpen )

PROCEDURE Main()

   LOCAL aPorts AS ARRAY OF RECORD Port168 := { { "COM1", 9600, .F. } }
   LOCAL aSpare AS ARRAY OF RECORD Port168
   LOCAL aPort AS RECORD Port168
   LOCAL i

   AAdd( aPorts, { "COM2", 19200, .T. } )
   aPorts[ 1 ][ PORT168_OPEN ] := .T.

   FOR i := 1 TO Len( aPorts )
      ? aPorts[ i ][ PORT168_NAME ], aPorts[ i ][ PORT168_SPEED ], aPorts[ i ][ PORT168_OPEN ]
   NEXT

   ? Fastest168( aPorts ), CountOpen168( aPorts )
   ? AScan( aPorts, { | aRow | aRow[ PORT168_NAME ] == "COM2" } )

   FOR EACH aPort IN aPorts
      ? aPort[ PORT168_NAME ]
   NEXT

   aSpare := Array( 2 )
   aSpare[ 2 ] := { "LPT1", 0, .F. }
   ? Len( aSpare ), aSpare[ 1 ] == NIL, aSpare[ 2 ][ PORT168_NAME ]

RETURN

// A parameter its caller gives the list is the list
FUNCTION Fastest168( aList )

   LOCAL i
   LOCAL iBest := 0

   FOR i := 1 TO Len( aList )
      iBest := Max( iBest, aList[ i ][ PORT168_SPEED ] )
   NEXT

RETURN iBest

FUNCTION CountOpen168( aList AS ARRAY OF RECORD Port168 )

   LOCAL aPort AS RECORD Port168
   LOCAL iOpen := 0

   FOR EACH aPort IN aList
      IF aPort[ PORT168_OPEN ]
         iOpen++
      ENDIF
   NEXT

RETURN iOpen
