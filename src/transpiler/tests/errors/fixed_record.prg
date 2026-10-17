// W0041 for records and fixed arrays (plan C12): a `{ … }` literal for a
// record is checked by count and field by field, one for a FIXED array
// element by element; a FIXED array takes no plain array and is not
// grown or cut; a record's field takes its type only. W0046: a record is
// read at a constant position, a literal or a #define, within its
// fields (the C# class has no indexer). The lines marked "quiet" must
// not warn.

#define FR_LEN 3

RECORD FieldRow41( cName, cType, iLen, iDec )

STATIC saRows41 AS FIXED ARRAY OF RECORD FieldRow41 := { ;
   { "PLUNO", "N", 16, 0 }, ;                       // quiet
   { "CAPTION", "C", 40 } }                          // fires W0041: 3 elements for 4 fields

FUNCTION Use41( xAnything, aPlain, nPos )
   LOCAL aRow AS RECORD FieldRow41 := { "PRICE1", "N", "12", 3 }   // fires W0041: a string into iLen
   LOCAL aRows AS FIXED ARRAY OF RECORD FieldRow41
   aRow := { "PRICE2", "N", 12, 3 }                 // quiet
   aRow := xAnything                                // quiet: its type is unknown
   aRow := aPlain                                   // fires W0041: an array is not a FieldRow41
   aRow[ FR_LEN ] := "wide"                         // fires W0041: a string into iLen
   aRow[ 4 ] := 2                                   // quiet
   aRows := saRows41                                // quiet
   aRows := { aRow, { "PRICE3", "N", 12, 3 } }      // quiet
   aRows := aPlain                                  // fires W0041: a plain array into a FIXED one
   AAdd( aRows, aRow )                              // fires W0041: a FIXED array is not grown
   ASize( aRows, 1 )                                // fires W0041: nor cut
   Take41( { "PRICE4", "N", 12 } )                  // fires W0041: 3 elements for 4 fields
   Take41( aRow )                                   // quiet
   ? aRow[ nPos ]                                   // fires W0046: not a constant position
   ? aRow[ 5 ]                                      // fires W0046: no fifth field
   ? aRows[ nPos ][ FR_LEN ]                        // quiet: the row by a variable, its field by a constant
   ? aRows[ nPos ][ nPos ]                          // fires W0046: an element's position by a variable
   ? Len( aRow )                                    // fires W0046: a record has no length
   FOR EACH xAnything IN aRow                       // fires W0046: nor elements to walk
   NEXT
RETURN Len( aRows )

FUNCTION Take41( aRow AS RECORD FieldRow41 )
RETURN aRow[ 1 ]
