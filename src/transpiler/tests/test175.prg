// Test 175: whole numbers in, a whole number out.
//
// Min(), Max(), Abs() and Mod() of whole numbers are whole: HbRuntime has
// each for longs beside the one for decimals, C# binds it when every
// argument is a long, and the scan types the call INTEGER, so an `i` name
// takes the result without a cast and a function that returns it is long.
// The same for a difference, a product and a remainder of two whole
// numbers, as a sum already was. A decimal among the arguments or the
// operands keeps the decimal one; a quotient is never whole. A remainder
// and a quotient are printed through Str(): Harbour shows either with
// two decimals, whatever its value.

FUNCTION Clamp175( iValue, iLow, iHigh )
RETURN Max( iLow, Min( iValue, iHigh ) )

FUNCTION Span175( iFrom, iTo )
RETURN Abs( iTo - iFrom ) * 2 + Mod( iTo, 3 )

FUNCTION Share175( nAmount, iParts )
RETURN Max( nAmount / iParts, 0.5 )

PROCEDURE Main()

   LOCAL iQty := 7
   LOCAL iPick
   LOCAL iLeft
   LOCAL nPart

   iPick := Min( iQty, 3 )
   iLeft := iQty - iPick * 2 + iQty % 4
   nPart := Min( 2.5, iQty )

   ? iPick, Str( iLeft, 3 ), nPart
   ? Clamp175( 12, 1, 9 ), Clamp175( -4, 1, 9 )
   ? Str( Span175( 10, 4 ), 4 )
   ? Str( Share175( 10, 4 ), 6, 2 ), Str( Share175( 1, 4 ), 6, 2 )
   ? Abs( -3 ) + Abs( 2.5 )

RETURN
