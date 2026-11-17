// The audit's EXACT-BLANKS: a string !=, <, <=, >, >= or AScan() where a side
// may carry trailing blanks, which SET EXACT ON ignores and C# does not. A
// line marked `row` must give a row, and no other line may.

FUNCTION Blanks46( cGiven, aList )

   LOCAL cTime := Left( Time(), 5 )
   LOCAL cPadded := PadR( "x", 5 )
   LOCAL cJoined
   LOCAL cAdded := "a"
   LOCAL lAny

   cJoined := cTime + ":00"
   cAdded += cGiven

   lAny := cGiven != "abc"                          // row: a parameter is unknown
   lAny := cTime < "12:00"                          // quiet: a part of Time(), which has no blank
   lAny := cJoined <= "12:00:00"                    // quiet: two such values joined
   lAny := cPadded != "x"                           // row: padded
   lAny := cAdded != "ab"                           // row: a local that is added to
   lAny := RTrim( cGiven ) != "abc"                 // quiet: trimmed
   lAny := ! ( cGiven == "abc" )                    // quiet: exact in both
   lAny := iif( lAny, "a", "b" ) != "a"             // quiet: an IIF() of trimmed values
   lAny := cGiven == NIL .OR. Empty( cGiven )       // quiet: no comparison of strings
   lAny := AScan( aList, cTime ) > 0                // row: the elements are unknown
   lAny := hb_AScan( aList, cTime, , , .T. ) > 0    // quiet: an exact scan

RETURN lAny
