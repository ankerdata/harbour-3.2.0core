// Test 167: Array( n ) and `LOCAL a[ n ]` for a typed list. An array name
// (`an`, `ai`, `ac`) or AS ARRAY OF given Array( n ), or declared with a
// dimension, is that list in C#, a List<decimal>, List<long> or
// List<string> of n blanks (zero, or null for a string) where Harbour's
// elements are NIL until filled. A plain name's Array( n ) stays an array
// of NIL. A `{}` in an iif() given to a typed list is that list too.

#include "../include/astype.ch"

PROCEDURE Main()

   LOCAL anAmounts
   LOCAL aiCounts
   LOCAL acLabels[ 3 ]
   LOCAL aWords AS ARRAY OF CHARACTER
   LOCAL aPlain
   LOCAL i

   anAmounts := Array( 3 )
   AFill( anAmounts, 0 )
   anAmounts[ 2 ] := 1.50

   aiCounts := Array( 2 )
   FOR i := 1 TO Len( aiCounts )
      aiCounts[ i ] := i * 10
   NEXT

   acLabels[ 1 ] := "one"
   acLabels[ 3 ] := "three"

   aWords := Array( 2 )
   aWords[ 2 ] := "second"

   aPlain := Array( 2 )
   aPlain[ 1 ] := "mixed"
   aPlain[ 2 ] := 7

   ? Len( anAmounts ), anAmounts[ 1 ], anAmounts[ 2 ]
   ? Len( aiCounts ), aiCounts[ 1 ], aiCounts[ 2 ]
   ? Len( acLabels ), acLabels[ 1 ], acLabels[ 2 ] == NIL, acLabels[ 3 ]
   ? Len( aWords ), aWords[ 1 ] == NIL, aWords[ 2 ]
   ? Len( aPlain ), aPlain[ 1 ], aPlain[ 2 ]

   // A literal in an iif() is the list the iif() goes into
   aiCounts := iif( Len( aWords ) > 5, aiCounts, {} )
   anAmounts := iif( Len( aWords ) > 5, {}, AClone( anAmounts ) )
   ? Len( aiCounts ), Len( anAmounts ), anAmounts[ 2 ]

RETURN
