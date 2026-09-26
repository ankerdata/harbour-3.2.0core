// W0024 for an `i` name (a whole number, C# long) given a value that may
// hold a fraction - a division, a power, a literal with decimals - which the
// emitter's (long) cast would drop where Harbour keeps it. Each warning has
// its own wording, so run.sh checks each; the lines marked "quiet" must not
// warn (Int() says the fraction goes, a whole literal and a call say nothing).

PROCEDURE Main()

   LOCAL nTotal := 7
   LOCAL iHalf := nTotal / 2         // fires: assigning
   LOCAL iCount := 0                 // quiet
   LOCAL iPos

   iHalf := Int( nTotal / 2 )        // quiet
   iCount /= 2                       // fires: '/=' whatever the right side
   iCount *= 1.5                     // fires: a compound of a fraction
   iCount += 1                       // quiet
   FOR iPos := 1 TO 3 STEP 0.5       // fires: the STEP
      ? iPos
   NEXT
   ? Twice( nTotal / 2 )             // fires: passing
   ? Twice( Int( nTotal / 2 ) )      // quiet
   ? iHalf, iCount

   RETURN

FUNCTION Twice( iValue )

   RETURN iValue * 2
