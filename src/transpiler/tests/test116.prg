// Test 116: a core function whose C# return type says nothing.
//
// hbfuncs.tab gives each HbRuntime function the return type of its C#
// signature, and "-" for one returning dynamic, void or a class. Such a
// call used to type the local it initialises as USUAL whatever the
// local's Hungarian prefix said, so hCopy := hb_HClone( h ) was dynamic,
// not a hash. A "-" says nothing now and the prefix decides: a hash, an
// array, a number, and dynamic only for an x name. A typed row still types
// the call: hb_ntos() is STRING.

PROCEDURE Main()

   LOCAL h := { "a" => 1, "b" => 2 }
   LOCAL hCopy := hb_HClone( h )
   LOCAL aKeys := hb_HKeys( h )
   LOCAL nB := hb_HGetDef( h, "b", 0 )
   LOCAL xAny := hb_HGetDef( h, "c", "none" )
   LOCAL cText := hb_ntos( nB + 1 )
   LOCAL cKey

   hCopy[ "c" ] := 3
   ? "clone:", hb_ntos( Len( h ) ), hb_ntos( Len( hCopy ) )
   ? "keys:"
   FOR EACH cKey IN aKeys
      ?? " " + cKey
   NEXT
   ? "nB + 1:", cText
   ? "xAny:", xAny

   RETURN
