// Test 178: Directory()'s entries are a record the runtime declares
// (hbclasses.tab's DirectoryEntry): `aDir[ i ][ F_NAME ]` is the entry's
// name field, a string, in a plain `a` local, a FOR EACH, a codeblock's
// parameter and an untyped parameter of a function the list is passed to.

#include "directry.ch"

PROCEDURE Main()

   LOCAL aDir, aFile, i
   LOCAL cName := "_t178.tmp"

   hb_MemoWrit( cName, "abc" )
   aDir := Directory( cName )

   ? Len( aDir ), aDir[ 1 ][ F_NAME ], aDir[ 1 ][ F_SIZE ], aDir[ 1 ][ F_DATE ] == Date(), Len( aDir[ 1 ][ F_TIME ] )
   FOR EACH aFile IN aDir
      ? Upper( aFile[ F_NAME ] )
   NEXT
   ? AScan( aDir, {| aEntry | aEntry[ F_NAME ] == cName } )
   FOR i := 1 TO Len( aDir )
      ? Name178( aDir, i )
   NEXT

   FErase( cName )

   RETURN

FUNCTION Name178( aDir, i )

RETURN Left( aDir[ i ][ F_NAME ], 5 )
