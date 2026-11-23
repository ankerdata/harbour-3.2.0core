// The audit's HASH-ANY: a variable that is a hash by its name or its
// declaration, given a value of no known type. A line marked `row` must
// give a row, and no other line may: a hash literal, a decoded JSON text,
// a clone, a value whose type the scan knows, an element of a typed list
// and a name that is no hash are none.

SHAPE Tag48h( Name AS STRING, Count AS INTEGER )

FUNCTION Any48h( xGiven, aPlain, cText, hcKnown, aTags AS ARRAY OF SHAPE Tag48h )

   LOCAL hcOne, hcTwo, hcEach, hcTyped, xLoose
   LOCAL hcDeclared AS STRING HASH
   LOCAL hcTag AS SHAPE Tag48h

   hcOne := xGiven                                  // row: an untyped parameter's value
   hcOne := aPlain[ 1 ]                             // row: an element of a plain array
   hcDeclared := xGiven                             // row: declared a hash, and given the same
   hcTwo := { "Name" => "a" }                       // quiet: a literal
   hcTwo := hb_jsonDecode( cText )                  // quiet: a hash by construction
   hcTwo := hb_HClone( hcKnown )                    // quiet
   hcTwo := hcKnown                                 // quiet: a hash by its own name
   xLoose := xGiven                                 // quiet: the name is no hash
   hcTag := aTags[ 1 ]                              // quiet: a shape from a list of them

   FOR EACH hcEach IN aPlain                        // row: each element of a plain array
      ? hcEach[ "Name" ]
   NEXT
   FOR EACH hcTyped IN aTags                        // quiet: each element is a Tag48h
      ? hcTyped[ "Name" ]
   NEXT

RETURN Len( hcOne ) + Len( hcTwo ) + Len( hcDeclared ) + hcTag[ "Count" ] + Len( xLoose )
