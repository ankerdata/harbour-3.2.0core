// The audit's WHOLE-N-TYPED: a local named a decimal (`n`) that the scan
// has made a whole number, a long in C#. A line marked `row` must give
// one, and no other line may: a local that keeps a fraction, an `i` name,
// and a parameter are none.

FUNCTION Count49w() AS INTEGER
RETURN 3

FUNCTION Typed49w( aRows, nGiven )

   LOCAL nCount := Count49w()                       // row: a whole number from an AS INTEGER function
   LOCAL nPrice := 2.5                              // quiet: it holds a fraction
   LOCAL iPlace := 1                                // quiet: named whole already
   LOCAL nAt                                        // row: every write a whole number, and an index

   nAt := Len( aRows )
   ? aRows[ nAt ], nGiven

RETURN nCount + iPlace + nPrice
