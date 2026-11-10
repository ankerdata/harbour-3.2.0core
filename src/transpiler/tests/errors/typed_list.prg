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

// An undeclared local inference makes a typed list (a declared function's
// result, assigned after the declaration) is one in C#: a number put into
// it is W0041 too
FUNCTION Use41k( cText )
   LOCAL aParts
   aParts := Split41k( cText )                      // quiet: a List<string>
   AAdd( aParts, "tail" )                           // quiet
   aParts[ 1 ] := Val( aParts[ 1 ] )                // fires W0041: a number into a List<string>
RETURN Len( aParts )

FUNCTION Split41k( cText ) AS ARRAY OF CHARACTER
   LOCAL aOut AS ARRAY OF CHARACTER := {}
   AAdd( aOut, cText )
RETURN aOut

// A parameter its callers made a typed list (an undeclared `a` parameter
// given one becomes one): a plain array passed to it is W0041, as C# takes
// none
FUNCTION Use41p( aPlain )
   LOCAL aNames AS ARRAY OF CHARACTER := { "a" }
   Count41p( aNames )                               // quiet: makes aItems a List<string>
   Count41p( aPlain )                               // fires W0041: a plain array for it
RETURN NIL

FUNCTION Count41p( aItems )
RETURN Len( aItems )
