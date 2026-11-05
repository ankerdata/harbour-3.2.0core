// Test 163: arrays of whole numbers and arrays of arrays, declared. `AS
// ARRAY OF INTEGER` (a type Harbour has not, so the lexer composes it as it
// does a typed hash) reads an element as a long; `AS ARRAY OF ARRAY OF
// CHARACTER`, sqlite3_get_table()'s rows of text, reads a row as an array
// of strings and a value as a string. On a member, a local, a parameter
// and a FIXED array.

#include "../include/astype.ch"
#include "hbclass.ch"

CLASS Order163
   VAR aIds AS ARRAY OF INTEGER INIT {}
   METHOD Add163( iId )
   METHOD Last163()
ENDCLASS

METHOD Add163( iId ) CLASS Order163

   AAdd( ::aIds, iId )

RETURN Self

METHOD Last163() CLASS Order163

   LOCAL iLast := 0

   IF Len( ::aIds ) > 0
      iLast := ::aIds[ Len( ::aIds ) ]
   ENDIF

RETURN iLast

PROCEDURE Main()

   LOCAL oOrder163 := Order163():New()
   LOCAL aTable AS ARRAY OF ARRAY OF CHARACTER := { { "nId", "cName" }, { "7", "seven" }, { "9", "nine" } }
   LOCAL aFixed AS FIXED ARRAY OF INTEGER := { 3, 4 }

   oOrder163:Add163( 5 ):Add163( 6 )
   ? oOrder163:Last163(), Sum163( oOrder163:aIds ), aFixed[ 2 ]
   ? Name163( aTable, 2 ), Upper( aTable[ 3 ][ 2 ] ), Len( aTable[ 2 ] )

   RETURN

FUNCTION Sum163( aIds AS ARRAY OF INTEGER )

   LOCAL iSum := 0
   LOCAL i

   FOR i := 1 TO Len( aIds )
      iSum += aIds[ i ]
   NEXT

RETURN iSum

FUNCTION Name163( aRows AS ARRAY OF ARRAY OF CHARACTER, iRow )

   LOCAL aRow AS ARRAY OF CHARACTER := aRows[ iRow ]

RETURN aRow[ 2 ] + "/" + aRows[ iRow ][ 1 ]
