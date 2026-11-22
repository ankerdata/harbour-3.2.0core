// W0041, W0046 and W0021 for shapes: a literal with a key the shape has
// not, without one it has, or with a value of another type; a plain hash
// into a shape; a value of another type put under a key; a key read that
// the shape does not declare; and a key declared with no type. The lines
// marked "quiet" must not warn.

SHAPE Lamp41z( Name AS STRING, Watts AS INTEGER, Lit AS LOGICAL )
SHAPE Bare41z( Name AS STRING, Loose )                                              // fires W0021: a key with no type

FUNCTION Use41z( xAnything, hcPlain, nRate )
   LOCAL hcLamp AS SHAPE Lamp41z := { "Name" => "a", "Watts" => 1, "Lit" => .T. }   // quiet
   LOCAL hcShort AS SHAPE Lamp41z := { "Name" => "a", "Watts" => 1 }                // fires W0041: without "Lit"
   LOCAL hcMore AS SHAPE Lamp41z := { "Name" => "a", "Watts" => 1, "Lit" => .T., "Colour" => "red" }   // fires W0041: "Colour"
   LOCAL hcType AS SHAPE Lamp41z := { "Name" => 5, "Watts" => 1, "Lit" => .T. }     // fires W0041: a number is no name
   LOCAL hcEmpty AS SHAPE Lamp41z := { => }                                         // quiet: built key by key
   LOCAL hcBare AS SHAPE Bare41z := { => }                                          // quiet
   LOCAL hcAny AS SHAPE
   hcLamp := xAnything                               // quiet: its type is unknown
   hcLamp := hcPlain                                 // fires W0041: a plain hash into a shape
   hcLamp[ "Watts" ] := "forty"                      // fires W0041: text is no whole number
   hcLamp[ "Watts" ] := nRate                        // fires W0041: a decimal into a whole number
   hcLamp[ "Watts" ] := 40                           // quiet
   hcEmpty[ "Name" ] := "b"                          // quiet
   hcAny := hcLamp                                   // quiet: any shape
   hcAny := hcPlain                                  // fires W0041: a plain hash is no shape
   ? hcLamp[ "Colour" ]                              // fires W0046: no such key
RETURN hcLamp[ "Name" ] + hcShort[ "Name" ] + hcMore[ "Name" ] + hcType[ "Name" ] + hcBare[ "Name" ]
