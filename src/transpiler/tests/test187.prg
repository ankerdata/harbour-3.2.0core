// Test 187: a shape's OPTIONAL keys. `Key AS TYPE OPTIONAL` is a key a
// message may go without: a literal may leave it out, and it is there, to
// hb_HHasKey(), Len() and hb_jsonEncode(), once it is given a value, as a
// key of Harbour's hash is. A number, a logical and text; set by a literal,
// by a subscript and inside an IF; read back typed and through an untyped
// value. The keys are set in the declared order here: on the wire a shape's
// keys stand in that order in C#, and in the order they were set in Harbour.

#include "../include/astype.ch"

SHAPE Reply187( Type AS STRING, Count AS INTEGER OPTIONAL, Note AS STRING OPTIONAL, ;
                Paid AS LOGICAL OPTIONAL, Total AS NUMERIC )

PROCEDURE Main()

   LOCAL hcBare AS SHAPE Reply187 := { "Type" => "bare", "Total" => 1.50 }
   LOCAL hcFull AS SHAPE Reply187 := { "Type" => "full", "Count" => 3, "Note" => "all", "Paid" => .T., "Total" => 2.25 }
   LOCAL hcBuilt AS SHAPE Reply187 := { => }
   LOCAL xHeld

   ? hb_jsonEncode( hcBare )
   ? hb_jsonEncode( hcFull )
   ? hb_HHasKey( hcBare, "Count" ), hb_HHasKey( hcFull, "Count" ), Len( hcBare ), Len( hcFull )
   ? hcFull[ "Count" ] + 1, hcFull[ "Note" ], hcFull[ "Paid" ]

   hcBare[ "Count" ] := 7
   hcBare[ "Paid" ] := .F.
   ? hcBare[ "Count" ] * 2, hcBare[ "Paid" ], hb_HHasKey( hcBare, "Note" ), Len( hcBare )

   hcBuilt[ "Type" ] := "built"
   IF hcFull[ "Paid" ]
      hcBuilt[ "Note" ] := "paid"
   ENDIF
   IF hcBare[ "Paid" ]
      hcBuilt[ "Paid" ] := .T.
   ENDIF
   hcBuilt[ "Total" ] := 0.75
   ? hb_jsonEncode( hcBuilt )

   xHeld := hcBuilt
   ? hb_HGetDef( xHeld, "Count", -1 ), hb_HGetDef( xHeld, "Note", "none" ), Len( hb_HKeys( xHeld ) )
   xHeld[ "Count" ] := 12
   ? hb_HGetDef( xHeld, "Count", -1 ), hcBuilt[ "Count" ], Len( xHeld )

RETURN
