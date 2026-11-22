// Test 186: a shape, a hash whose keys are declared. `SHAPE X( Key AS ..., ... )`
// names the keys and their types; a hash to Harbour, a class of a field per
// key to C#. A literal under the declaration is the class with its fields
// set, nested as deep as the declarations go; a subscript by a string
// literal is the field, read or written; `{ => }` is the shape with nothing
// set, built key by key. `AS SHAPE` with no name holds any shape. Held as
// an untyped value a shape is still a hash: ValType(), hb_HHasKey(), a
// subscript by a variable, hb_jsonEncode().

#include "../include/astype.ch"

SHAPE Lamp186( Name AS STRING, Watts AS INTEGER, Price AS NUMERIC, Lit AS LOGICAL )
SHAPE Room186( Title AS STRING, Lamp AS SHAPE Lamp186, Spare AS SHAPE Lamp186, ;
               Tags AS STRING HASH, Lamps AS ARRAY OF SHAPE Lamp186, uuid AS STRING )

PROCEDURE Main()

   LOCAL hcLamp AS SHAPE Lamp186 := { "Name" => "desk", "Watts" => 40, "Price" => 12.50, "Lit" => .T. }
   LOCAL hcRoom AS SHAPE Room186 := { ;
      "Title" => "study", ;
      "Lamp" => { "Name" => "ceiling", "Watts" => 60, "Price" => 30.25, "Lit" => .F. }, ;
      "Spare" => NIL, ;
      "Tags" => { "floor" => "first" }, ;
      "Lamps" => { hcLamp }, ;
      "uuid" => "a-1" }
   LOCAL hcBuilt AS SHAPE Lamp186 := { => }
   LOCAL hcAny AS SHAPE
   LOCAL xHeld
   LOCAL cKey := "Title"

   ? hcLamp[ "Name" ], hcLamp[ "Watts" ] + 1, hcLamp[ "Price" ] * 2, hcLamp[ "Lit" ]
   hcLamp[ "Watts" ] := 25
   hcLamp[ "Lit" ] := ! hcLamp[ "Lit" ]
   ? hcLamp[ "Watts" ], hcLamp[ "Lit" ]

   ? hcRoom[ "Lamp" ][ "Name" ], hcRoom[ "Spare" ] == NIL, hcRoom[ "Tags" ][ "floor" ]
   hcRoom[ "Spare" ] := { "Name" => "spare", "Watts" => 5, "Price" => 1.00, "Lit" => .F. }
   ? hcRoom[ "Spare" ][ "Watts" ], Len( hcRoom[ "Lamps" ] ), hcRoom[ "Lamps" ][ 1 ][ "Name" ]
   AAdd( hcRoom[ "Lamps" ], MakeLamp186( "floor", 15 ) )
   ? Len( hcRoom[ "Lamps" ] ), hcRoom[ "Lamps" ][ 2 ][ "Watts" ], hcRoom[ "uuid" ]

   hcBuilt[ "Name" ] := "built"
   hcBuilt[ "Watts" ] := 7
   hcBuilt[ "Price" ] := 2.75
   hcBuilt[ "Lit" ] := .T.
   ? Describe186( hcBuilt ), Describe186( MakeLamp186( "made", 9 ) )

   ? hb_jsonEncode( hcLamp )
   ? hb_jsonEncode( hcRoom )

   hcAny := hcLamp
   ? hb_jsonEncode( hcAny )
   hcAny := hcBuilt
   ? Wire186( hcAny )

   xHeld := hcRoom
   ? ValType( xHeld ), hb_HHasKey( xHeld, "Title" ), hb_HHasKey( xHeld, "Colour" ), xHeld[ cKey ]
   ? Len( xHeld ), hb_HGetDef( xHeld, "uuid", "none" ), hb_HGetDef( xHeld, "Colour", "none" )
   ? hb_jsonEncode( { "Type" => "Room", "Payload" => hcRoom } )

RETURN

FUNCTION MakeLamp186( cName, iWatts ) AS SHAPE Lamp186
RETURN { "Name" => cName, "Watts" => iWatts, "Price" => 1.25, "Lit" => .T. }

FUNCTION Describe186( hcLamp AS SHAPE Lamp186 ) AS STRING
RETURN hcLamp[ "Name" ] + " " + hb_ntos( hcLamp[ "Watts" ] ) + iif( hcLamp[ "Lit" ], " lit", " dark" )

FUNCTION Wire186( hcAny AS SHAPE ) AS STRING
RETURN hb_jsonEncode( hcAny )
