#include "astype.ch"
// Test 96: FOR EACH with the enumerator messages.
//
// Harbour's FOR EACH binds the variable to the VALUE and exposes the
// key through `x:__enumKey()`. C# iterating a Dictionary yields pairs,
// so a loop whose body reads an enumerator message runs over
// HbRuntime.HbEnumPairs — (key, value) for a hash, (1-based index,
// element) for an array — with the variable bound to the pair's Value
// and `__enumKey()` / `__enumValue()` reading the pair. A loop that
// never asks for the key is emitted as before.
PROCEDURE Main()
   LOCAL hAges := {"ann" => 31, "bob" => 42} AS HASH
   LOCAL xItem AS USUAL
   LOCAL cOut := "" AS STRING
   LOCAL nTotal := 0 AS NUMERIC

   FOR EACH xItem IN hAges
      cOut += xItem:__enumKey() + "=" + LTrim(Str(xItem)) + " "
   NEXT

   QOut(cOut)

   FOR EACH xItem IN hAges
      nTotal += xItem:__enumValue()
   NEXT

   QOut("total=" + LTrim(Str(nTotal)))

   // no message: emitted as before
   FOR EACH xItem IN hAges
      nTotal += xItem
   NEXT

   QOut("twice=" + LTrim(Str(nTotal)))
RETURN
