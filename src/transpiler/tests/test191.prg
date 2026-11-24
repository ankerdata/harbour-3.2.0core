// Test 191: an INLINE method's parameters are typed by their names, as a
// method's are: `cLabel` a string, `iCount` a whole number, `xExtra`
// dynamic. The C# method declares them so, and the reftab row the scan
// checks a caller's arguments against has the same types.

#include "hbclass.ch"

PROCEDURE Main()

   LOCAL oTally191 := Tally191():New()

   ? oTally191:Label191( "cups", 3 )
   ? oTally191:Label191( "jars", 12, "!" )

RETURN

CLASS Tally191
   METHOD New() CONSTRUCTOR
   METHOD Label191( cLabel, iCount, xExtra ) AS STRING INLINE ( cLabel + "=" + LTrim( Str( iCount ) ) + iif( xExtra == NIL, "", xExtra ) )
ENDCLASS

METHOD New() CLASS Tally191
RETURN Self
