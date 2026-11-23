// The audit's NARROW-ASSIGN: a decimal written into a whole-number (`i`)
// name, which the C# casts to long. A line marked `row` must give one, and
// no other line may: a whole number, text read with Int(), another `i`
// name and a write into an `n` name are none.

FUNCTION Price49n() AS NUMERIC
RETURN 2.5

FUNCTION Count49n() AS INTEGER
RETURN 3

FUNCTION Narrow49n( nGiven, cText )

   LOCAL iTaken := nGiven                           // row: an n parameter
   LOCAL iPlace := Count49n()                       // quiet: an AS INTEGER function
   LOCAL iCut := Int( nGiven )                      // quiet: made whole
   LOCAL iRead := Int( Val( cText ) )               // quiet: made whole
   LOCAL nKept := nGiven                            // quiet: an n name keeps it
   LOCAL iSum := 0
   STATIC siLast := 0

   iSum += Price49n()                               // row: a decimal added in
   iSum += iPlace                                   // quiet: a whole number added in
   iSum := iPlace + iCut                            // quiet: whole numbers
   siLast := Price49n()                             // row: a static too
   iTaken := Len( cText )                           // quiet: a length

RETURN iSum + iTaken + iRead + nKept + siLast
