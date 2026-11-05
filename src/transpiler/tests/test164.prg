// Test 164: typed lists. An array of strings, or of such arrays, is a C#
// List<string> / List<List<string>> where any other array is a
// List<dynamic> (plan C12). Declared, its literals are typed, an empty one
// too; an undeclared `a` local or parameter given one becomes one, as for a
// FIXED array, and so does an undeclared function's return; a row read from
// one is a List<string>; AAdd() and FOR EACH work on it as on any array, and
// AClone() gives back a copy of the same type.

#include "../include/astype.ch"
#include "hbclass.ch"

CLASS Store164
   VAR aRows AS ARRAY OF ARRAY OF CHARACTER INIT {}
   METHOD Fetch164( cWhere ) AS ARRAY OF ARRAY OF CHARACTER
ENDCLASS

METHOD Fetch164( cWhere ) CLASS Store164

   LOCAL aResult AS ARRAY OF ARRAY OF CHARACTER := { { "cName" } }
   LOCAL aRow

   FOR EACH aRow IN ::aRows
      IF aRow[ 1 ] == cWhere
         AAdd( aResult, { aRow[ 2 ] } )
      ENDIF
   NEXT

RETURN aResult

PROCEDURE Main()

   LOCAL oStore164 := Store164():New()
   LOCAL aTable
   LOCAL aRow
   LOCAL aCopy
   LOCAL aNames AS ARRAY OF CHARACTER := {}

   AAdd( oStore164:aRows, { "red", "apple" } )
   AAdd( oStore164:aRows, { "green", "pear" } )
   AAdd( oStore164:aRows, { "red", "cherry" } )

   aTable := oStore164:Fetch164( "red" )
   aRow := aTable[ 2 ]
   ? Len( aTable ), aRow[ 1 ], Upper( aTable[ 3 ][ 1 ] )
   ? Count164( aTable ), Count164( Rows164( .F. ) ), Last164( Rows164( .T. ) )

   aCopy := AClone( aTable )
   aCopy[ 2 ][ 1 ] := "pear"
   ? Len( aCopy ), aCopy[ 2 ][ 1 ], aTable[ 2 ][ 1 ]

   AAdd( aNames, "first" )
   aNames := Names164( aNames )
   ? Len( aNames ), aNames[ 2 ]

   RETURN

FUNCTION Rows164( lAny ) AS ARRAY OF ARRAY OF CHARACTER

   IF ! lAny
      RETURN {}
   ENDIF

RETURN { { "cName" }, { "one" } }

FUNCTION Count164( aRows )

RETURN Max( Len( aRows ) - 1, 0 )

FUNCTION Last164( aRows )

RETURN Lower( aRows[ Len( aRows ) ][ 1 ] )

FUNCTION Names164( aList )

   AAdd( aList, "second" )

RETURN aList
