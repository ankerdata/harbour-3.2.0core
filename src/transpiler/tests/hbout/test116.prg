#include "astype.ch"
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

   LOCAL h := {"a" => 1, "b" => 2} AS HASH
   LOCAL hCopy := hb_HClone(h) AS HASH
   LOCAL aKeys := hb_HKeys(h) AS ARRAY
   LOCAL nB := hb_HGetDef(h, "b", 0) AS NUMERIC
   LOCAL xAny := hb_HGetDef(h, "c", "none") AS USUAL
   LOCAL cText := hb_ntos(nB + 1) AS STRING
   LOCAL cKey AS STRING

   hCopy["c"] := 3
   QOut("clone:", hb_ntos(Len(h)), hb_ntos(Len(hCopy)))
   QOut("keys:")
   FOR EACH cKey IN aKeys
      QQOut(" " + cKey)
   NEXT

   QOut("nB + 1:", cText)
   QOut("xAny:", xAny)

RETURN
