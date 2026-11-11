// Array names (plan C12): `an`, `ai` and `ac` before a capital declare an
// array's elements as AS ARRAY OF does. W0035 for a declaration its name
// contradicts; W0041 for what such an array cannot take. The lines marked
// "quiet" must not warn.

FUNCTION Use35n( aPlain, nValue, iCount )
   LOCAL acNames AS ARRAY OF INTEGER := {}          // fires W0035: its name declares strings
   LOCAL aiFixed AS FIXED ARRAY OF INTEGER := { 1 } // quiet: a FIXED array of the same
   LOCAL aiIds := { 1, 2 }                          // quiet
   LOCAL anPrices := { 1.5 }                        // quiet
   aiIds := aPlain                                  // fires W0041: a plain array into a List<long>
   AAdd( aiIds, nValue )                            // fires W0041: a decimal into a List<long>
   AAdd( anPrices, nValue )                         // quiet: a List<decimal> takes one
   AAdd( anPrices, iCount )                         // quiet: and a whole number
   AAdd( aiIds, iCount + 1 )                        // quiet: whole arithmetic is whole
   ASize( anPrices, 3 )                             // fires W0041: NIL into a List<decimal>
   Count35n( aPlain )                               // fires W0041: a plain array for an `ac` parameter
RETURN Len( acNames ) + Len( aiIds ) + Len( aiFixed )

FUNCTION Count35n( acItems )
RETURN Len( acItems )

// A dimension, and Array( n ), make the list the name declares
FUNCTION Dim35n()
   LOCAL acLabels[ 2 ]                              // quiet
   LOCAL anTotals
   anTotals := Array( 3 )                           // quiet
RETURN Len( acLabels ) + Len( anTotals )
