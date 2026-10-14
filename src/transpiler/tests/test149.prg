// Test 149: the short overload returns what the canonical returns.
//
// A function with a by-reference parameter gets a second, shorter C#
// overload for the callers that leave the `@` arguments out (test56,
// test65). It forwards to the canonical and answers its result, but it
// was declared `dynamic`, so every short call lost the type the
// canonical had: in EasiPOS 115 routines, `decimal GetQty( ref bool )`
// beside `dynamic GetQty()`. The short overload now carries the
// canonical's return type: long for the counter, string for the label,
// bool for the test; a PROCEDURE stays void.

STATIC siCalls149 := 0

FUNCTION Calls149( lFirst )
   LOCAL iCalls
   siCalls149++
   lFirst := ( siCalls149 == 1 )
   iCalls := siCalls149
RETURN iCalls

FUNCTION Label149( cPrefix, cLabel, nWidth )
   LOCAL cOut
   hb_default( @nWidth, 8 )
   cLabel := PadR( cPrefix + "-" + Str( Calls149(), 1 ), nWidth, "." )
   cOut := Upper( cLabel )
RETURN cOut

FUNCTION Even149( nValue, lOdd )
   lOdd := ( nValue % 2 == 1 )
RETURN ! lOdd

PROCEDURE Note149( cText, nLength )
   nLength := Len( cText )
   ? "note", cText
RETURN

PROCEDURE Main()
   LOCAL lFirst := .f., cLabel := "", nLength := 0, lOdd := .f.
   LOCAL nHalf

   ? Calls149( @lFirst ), lFirst          // the full call, by reference
   ? Calls149()                           // the short call: a long
   nHalf := Calls149() / 2
   ? Str( nHalf, 5, 2 ), Str( Calls149() / 2, 5, 2 )   // the whole-number return divides as Harbour's

   ? Label149( "item", @cLabel ), cLabel  // the full call, the width defaulted
   ? Label149( "sale" )                   // the short call: a string
   ? Len( Label149( "tax" ) )

   ? Even149( 4, @lOdd ), lOdd
   ? Even149( 7 )                         // the short call: a logical
   IF Even149( 10 )
      ? "ten is even"
   ENDIF

   Note149( "full", @nLength )
   ? nLength
   Note149( "short" )                     // the short call of a procedure: void
RETURN
