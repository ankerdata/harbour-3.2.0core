// The audit's NARROW-ARG: a decimal passed to a whole-number (`i`)
// parameter, which the C# casts to long. A line marked `row` must give one,
// and no other line may: an `i` or `si` name (whatever it was last given, a
// literal the scan calls NUMERIC among them), a Round() to no decimals and
// Int() are none.

STATIC siCount49q := 0
STATIC siDecs49q := 0

PROCEDURE Show49q( iNumber, iDecs )
   ? iNumber, iDecs
RETURN

PROCEDURE Build49q( iDigit, nAmount )

   IF siDecs49q > 0
      siCount49q := siCount49q * 10 + iDigit
      siDecs49q++
   ELSE
      siDecs49q := 1
      siCount49q := Int( siCount49q / 0.01 )
   ENDIF
   Show49q( siCount49q, siDecs49q )                 // quiet: si names
   Show49q( Round( nAmount / 0.01, 0 ), 0 )         // quiet: rounded to no decimals
   Show49q( nAmount, 2 )                            // row: a decimal

RETURN
