// The audit's LIST-SLOT: a typed list put where C# holds a dynamic slot, a
// plain hash's value or a plain array's element. A line marked `row` must
// give a row, and no other line may: a shape's key declared as that list
// is where the list belongs, in the shape's literal and in a shape nested
// in it.

SHAPE Part47s( Names AS ARRAY OF STRING, Count AS INTEGER )
SHAPE Whole47s( Title AS STRING, Sizes AS ARRAY OF INTEGER, Part AS SHAPE Part47s )

FUNCTION Slot47s( acNames AS ARRAY OF STRING, aiSizes AS ARRAY OF INTEGER )

   LOCAL hcPlain := { "Names" => acNames }                                  // row: a plain hash's value
   LOCAL aPlain := { acNames, 1 }                                           // row: a plain array's element
   LOCAL hcPart AS SHAPE Part47s := { "Names" => acNames, "Count" => 2 }    // quiet: the key is that list
   LOCAL hcWhole AS SHAPE Whole47s

   hcWhole := { "Title" => "a", "Sizes" => aiSizes, "Part" => { "Names" => acNames, "Count" => 1 } }   // quiet

RETURN Len( hcPlain ) + Len( aPlain ) + hcPart[ "Count" ] + Len( hcWhole[ "Sizes" ] )
