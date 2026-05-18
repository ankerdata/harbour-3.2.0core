// Test 63b: the global Render63() that collides by name with
// test63a's file-static one. Its parameter is numeric — a
// deliberately different signature, so that if test63a's static
// leaked into this entry (or vice versa) the mistype would show.

PROCEDURE Main()
   ? "b=" + Render63( 42 )
RETURN

FUNCTION Render63( nValue )
RETURN "#" + LTrim( Str( nValue ) )
