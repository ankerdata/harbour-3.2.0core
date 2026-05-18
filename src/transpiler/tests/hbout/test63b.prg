#include "astype.ch"
// Test 63b: the global Render63() that collides by name with
// test63a's file-static one. Its parameter is numeric — a
// deliberately different signature, so that if test63a's static
// leaked into this entry (or vice versa) the mistype would show.

PROCEDURE Main()
   QOut("b=" + Render63(42))
RETURN

FUNCTION Render63( nValue AS NUMERIC ) AS STRING
RETURN "#" + LTrim(Str(nValue))
