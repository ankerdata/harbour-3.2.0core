// W0041 for typed lists (plan C12): an array of strings, or of such
// arrays, is a C# List<string>, which takes no plain array and no value of
// another type. The lines marked "quiet" must not warn.

FUNCTION Use41l( xAnything, aPlain, nCount )
   LOCAL aNames AS ARRAY OF CHARACTER := { "a", 5 }   // fires W0041: a number is no string
   LOCAL aRows AS ARRAY OF ARRAY OF CHARACTER := {}   // quiet
   aNames := xAnything                              // quiet: its type is unknown
   aNames := aPlain                                 // fires W0041: a plain array into a typed list
   aRows := { aNames, { "b" } }                     // quiet
   AAdd( aRows, aPlain )                            // fires W0041: a plain array into a list of lists
   AAdd( aNames, nCount )                           // fires W0041: a number into a list of strings
RETURN Len( aNames ) + Len( aRows )

// A List<long>: no decimal, and nothing that leaves NIL
FUNCTION Use41i( nValue )
   LOCAL aIds AS ARRAY OF INTEGER := { 1, 2 }      // quiet
   AAdd( aIds, 3 )                                  // quiet: a literal with no fraction
   AAdd( aIds, nValue )                             // fires W0041: a decimal into a List<long>
   ASize( aIds, 5 )                                 // fires W0041: NIL into a List<long>
   hb_ADel( aIds, 1, .T. )                          // quiet
   hb_AIns( aIds, 1, 7, .T. )                       // quiet
   ADel( aIds, 1 )                                  // fires W0041: NIL left at the end
RETURN Len( aIds )
