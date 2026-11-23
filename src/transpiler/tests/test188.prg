// Test 188: a shape of more than 64 keys, and a list of shapes of several
// kinds. Wide188 has 70 keys, the last of them OPTIONAL: every one is a
// field, read and written by its key, and the optional one is there once
// it is set. `AS ARRAY OF SHAPE` with no name holds any shape: lines of two
// kinds are added to one list, written as JSON in their order, and read
// back through the key they share.

#include "../include/astype.ch"

SHAPE Wide188( ;
   K01 AS INTEGER, K02 AS INTEGER, K03 AS INTEGER, K04 AS INTEGER, K05 AS INTEGER, ;
   K06 AS INTEGER, K07 AS INTEGER, K08 AS INTEGER, K09 AS INTEGER, K10 AS INTEGER, ;
   K11 AS INTEGER, K12 AS INTEGER, K13 AS INTEGER, K14 AS INTEGER, K15 AS INTEGER, ;
   K16 AS INTEGER, K17 AS INTEGER, K18 AS INTEGER, K19 AS INTEGER, K20 AS INTEGER, ;
   K21 AS INTEGER, K22 AS INTEGER, K23 AS INTEGER, K24 AS INTEGER, K25 AS INTEGER, ;
   K26 AS INTEGER, K27 AS INTEGER, K28 AS INTEGER, K29 AS INTEGER, K30 AS INTEGER, ;
   K31 AS INTEGER, K32 AS INTEGER, K33 AS INTEGER, K34 AS INTEGER, K35 AS INTEGER, ;
   K36 AS INTEGER, K37 AS INTEGER, K38 AS INTEGER, K39 AS INTEGER, K40 AS INTEGER, ;
   K41 AS INTEGER, K42 AS INTEGER, K43 AS INTEGER, K44 AS INTEGER, K45 AS INTEGER, ;
   K46 AS INTEGER, K47 AS INTEGER, K48 AS INTEGER, K49 AS INTEGER, K50 AS INTEGER, ;
   K51 AS INTEGER, K52 AS INTEGER, K53 AS INTEGER, K54 AS INTEGER, K55 AS INTEGER, ;
   K56 AS INTEGER, K57 AS INTEGER, K58 AS INTEGER, K59 AS INTEGER, K60 AS INTEGER, ;
   K61 AS INTEGER, K62 AS INTEGER, K63 AS INTEGER, K64 AS INTEGER, K65 AS INTEGER, ;
   K66 AS INTEGER, K67 AS INTEGER, K68 AS INTEGER, Label AS STRING, Late AS INTEGER OPTIONAL )

SHAPE Item188( Type AS INTEGER, Name AS STRING, Price AS NUMERIC )
SHAPE Text188( Type AS INTEGER, Text AS STRING )
SHAPE Slip188( Title AS STRING, Lines AS ARRAY OF SHAPE )

PROCEDURE Main()

   LOCAL hcWide AS SHAPE Wide188 := { ;
      "K01" => 3, "K02" => 6, "K03" => 9, "K04" => 12, "K05" => 15, ;
      "K06" => 18, "K07" => 21, "K08" => 24, "K09" => 27, "K10" => 30, ;
      "K11" => 33, "K12" => 36, "K13" => 39, "K14" => 42, "K15" => 45, ;
      "K16" => 48, "K17" => 51, "K18" => 54, "K19" => 57, "K20" => 60, ;
      "K21" => 63, "K22" => 66, "K23" => 69, "K24" => 72, "K25" => 75, ;
      "K26" => 78, "K27" => 81, "K28" => 84, "K29" => 87, "K30" => 90, ;
      "K31" => 93, "K32" => 96, "K33" => 99, "K34" => 102, "K35" => 105, ;
      "K36" => 108, "K37" => 111, "K38" => 114, "K39" => 117, "K40" => 120, ;
      "K41" => 123, "K42" => 126, "K43" => 129, "K44" => 132, "K45" => 135, ;
      "K46" => 138, "K47" => 141, "K48" => 144, "K49" => 147, "K50" => 150, ;
      "K51" => 153, "K52" => 156, "K53" => 159, "K54" => 162, "K55" => 165, ;
      "K56" => 168, "K57" => 171, "K58" => 174, "K59" => 177, "K60" => 180, ;
      "K61" => 183, "K62" => 186, "K63" => 189, "K64" => 192, "K65" => 195, ;
      "K66" => 198, "K67" => 201, "K68" => 204, "Label" => "wide" }
   LOCAL aLines AS ARRAY OF SHAPE := {}
   LOCAL hcItem AS SHAPE Item188 := { "Type" => 1, "Name" => "tea", "Price" => 1.50 }
   LOCAL hcText AS SHAPE Text188 := { "Type" => 3, "Text" => "thank you" }
   LOCAL hcSlip AS SHAPE Slip188
   LOCAL hcLine AS SHAPE
   LOCAL iTypes := 0

   ? Len( hcWide ), hcWide[ "K01" ], hcWide[ "K64" ], hcWide[ "K65" ], hcWide[ "K68" ], hcWide[ "Label" ]
   ? hb_HHasKey( hcWide, "Late" ), hb_HHasKey( hcWide, "K66" )
   hcWide[ "K66" ] := 1000
   hcWide[ "Late" ] := 7
   ? hcWide[ "K66" ] + hcWide[ "Late" ], hb_HHasKey( hcWide, "Late" ), Len( hcWide )
   ? Right( hb_jsonEncode( hcWide ), 44 )

   AAdd( aLines, hcItem )
   AAdd( aLines, hcText )
   AAdd( aLines, MakeItem188( "scone", 2.25 ) )
   ? Len( aLines ), hb_jsonEncode( aLines )
   FOR EACH hcLine IN aLines
      iTypes += hb_HGet( hcLine, "Type" )
   NEXT
   ? iTypes

   hcSlip := { "Title" => "slip", "Lines" => aLines }
   ? hb_jsonEncode( hcSlip )
   ? Len( hcSlip[ "Lines" ] ), hb_HGet( hcSlip[ "Lines" ][ 2 ], "Text" )

RETURN

FUNCTION MakeItem188( cName, nPrice ) AS SHAPE Item188
RETURN { "Type" => 1, "Name" => cName, "Price" => nPrice }
