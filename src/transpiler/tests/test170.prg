// Test 170: a record written as JSON is the array Harbour holds. A record
// is a class in C#, and hb_jsonEncode() has no form for an object, so it
// would write null where Harbour writes the positions: the encoder writes
// a record as the array of its fields, on its own, in a list, nested in
// another record and as a hash's value.

#include "../include/astype.ch"

RECORD Plug170( cName, iPins, lEarthed )
RECORD Lead170( aPlug AS RECORD Plug170, nMetres, acColours )

PROCEDURE Main()

   LOCAL aPlug AS RECORD Plug170 := { "Schuko", 2, .T. }
   LOCAL aPlugs AS ARRAY OF RECORD Plug170 := { { "BS 1363", 3, .T. }, { "Europlug", 2, .F. } }
   LOCAL aLead AS RECORD Lead170 := { aPlug, 1.5, { "brown", "blue" } }
   LOCAL hStock AS STRING HASH := { "plug" => aPlug, "count" => 2 }

   ? hb_jsonEncode( aPlug )
   ? hb_jsonEncode( aPlugs )
   ? hb_jsonEncode( aLead )
   ? hb_jsonEncode( hStock )
   ? hb_jsonEncode( { aPlug, "spare" } )

RETURN
