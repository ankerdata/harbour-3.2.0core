#include "astype.ch"
// Test 48: local variable used both inside a FOR-loop body and later
// as a FOREACH iterator. The transpiler used to suppress the `local`
// declaration if ANY later FOREACH used the same name, walking only
// outer siblings — so a local assigned inside a FOR body (one level
// down) then reused by a FOREACH was emitted with no declaration,
// producing C# CS0103 / CS0841 at compile time.
//
// The fix always emits the method-level local and renames the FOREACH
// inner iterator via `__hb_fe_<name>`, assigning to the outer. This
// test locks that behaviour in.

PROCEDURE Main()

   LOCAL aFields := {"one", "two", "three"} AS ARRAY
   LOCAL cMemberName AS STRING
   LOCAL i AS NUMERIC
   LOCAL cAccum := "" AS STRING

   // Use cMemberName inside a FOR-loop body first (nested scope).
   FOR i := 1 TO Len(aFields)
      cMemberName := aFields[i]
      cAccum += "[" + cMemberName + "]"
   NEXT

   QOut("via-for: " + cAccum)

   // Then reuse the same name as a FOREACH iterator.
   cAccum := ""
   FOR EACH cMemberName IN aFields
      cAccum += "<" + cMemberName + ">"
   NEXT

   QOut("via-each: " + cAccum)

RETURN
