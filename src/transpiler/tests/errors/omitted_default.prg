// W0043 and W0044 (plan D20): Harbour passes an argument left out as NIL,
// which a DEFAULT takes. C# passes a value slot's zero instead, so a routine
// that hands such a slot on to one with a declared default loses the default
// (W0043), and an `x` value holding NIL cannot reach a default the C#
// signature carries (W0044). The four silent shapes must stay silent.

PROCEDURE Main()

   LOCAL xPanel45 := iif( Seconds() < 0, 3, NIL )

   ? Header45()            // leaves Header45's nNo45 out: W0043 in Header45
   ? Kept45()              // leaves Kept45's out, but Kept45 declares the default too
   ? Given45( 7 )          // every caller passes Given45's
   ? Zeroed45()            // leaves Zeroed45's out, but Count45's default is 0, C#'s zero
   ? Panel45( xPanel45 )   // an x value into a constant default: W0044 here
   ? Panel45( 4 )

   RETURN

STATIC FUNCTION Header45( nNo45 )
   RETURN "POS " + Number45( nNo45 )

STATIC FUNCTION Kept45( nNo45 )
   hb_default( @nNo45, 1 )
   RETURN "POS " + Number45( nNo45 )

STATIC FUNCTION Given45( nNo45 )
   RETURN "POS " + Number45( nNo45 )

STATIC FUNCTION Zeroed45( nNo45 )
   RETURN Count45( nNo45 )

STATIC FUNCTION Count45( nCount45 )
   hb_default( @nCount45, 0 )
   RETURN nCount45 + 1

STATIC FUNCTION Number45( nNo45 )
   hb_default( @nNo45, 1 )
   RETURN Str( nNo45, 2 )

STATIC FUNCTION Panel45( nPanel45 )
   hb_default( @nPanel45, 15 )
   RETURN nPanel45
