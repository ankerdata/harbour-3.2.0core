// Test 162: HbRuntime's whole-number functions return long. An `i` name
// takes one without a cast; a local a call to one initialises, used as a
// subscript, is long (Pass 2.5 counts what hbfuncs.tab calls INTEGER as a
// whole number); a `/` of two keeps Harbour's fraction. A `+` of two is
// whole, of one and a literal a number, of a date and one a date.

PROCEDURE Main()

   ? Find162( { "a", "bb", "ccc" }, "bb" ), Find162( { "a" }, "z" )
   ? Half162( 7 ), Half162( 10 )
   ? Parts162( 0d20261030 )
   ? Quarter162( 0d20261030 )
   ? Next162( "abc" ), Both162( "ab", "cde" )
   ? DToS( Later162( 0d20261030, 2.5 ) )

   RETURN

FUNCTION Find162( aList, cWant )

   LOCAL nPos := AScan( aList, cWant )

   IF nPos > 0
      RETURN aList[ nPos ]
   ENDIF

   RETURN "-"

FUNCTION Half162( nTotal )

   LOCAL iHalf := Int( nTotal / 2 )

   RETURN iHalf

FUNCTION Parts162( dDay )

   LOCAL iYear := Year( dDay )

   RETURN Str( iYear, 4 ) + Str( Month( dDay ), 3 ) + Str( Day( dDay ), 3 ) + Str( DoW( dDay ), 2 )

FUNCTION Quarter162( dDay )

   RETURN Str( Year( dDay ) / 4, 8, 2 )

FUNCTION Next162( cText )

   RETURN Len( cText ) + 1

FUNCTION Both162( cA, cB )

   RETURN Len( cA ) + Len( cB )

FUNCTION Later162( dDay, nDays )

   RETURN dDay + Int( nDays )
