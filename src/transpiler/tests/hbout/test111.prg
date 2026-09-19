#include "astype.ch"
// Test 111: a FOR loop counts in the direction of its step's sign.
//
// Harbour's code generator takes the direction from a constant step
// (hb_compExprAsNumSign) and tests any other step's sign at run time on
// every pass (HB_P_FORTEST). The emitter knew only a negative integer
// literal: `STEP nStep` with a negative nStep emitted `nI <= nEnd` and
// looped forever (hbtest's TFORNEXTX hung the RTL harness), and
// `STEP -0.5` never ran. Now a constant step picks `<=` or `>=` as
// before, and any other step is `HbRuntime.ForTest( nI, nEnd, nStep )`,
// the end evaluated before the step as Harbour evaluates them.

PROCEDURE Main()

   LOCAL nStep := -1 AS NUMERIC
   LOCAL nI AS NUMERIC
   LOCAL cTrail := "" AS STRING

   FOR nI := 5 TO 1 STEP nStep
      cTrail += Str(nI, 1)
   NEXT

   QOut(cTrail, nI)

   cTrail := ""
   FOR nI := 1 TO 5 STEP nStep
      cTrail += Str(nI, 1)
   NEXT

   QOut("[" + cTrail + "]", nI)

   cTrail := ""
   FOR nI := 1 TO 2 STEP 0.5
      cTrail += Str(nI, 3, 1) + " "
   NEXT

   QOut(cTrail)

   cTrail := ""
   FOR nI := 2 TO 1 STEP -0.5
      cTrail += Str(nI, 3, 1) + " "
   NEXT

   QOut(cTrail)

   // the step is read again on every pass
   cTrail := ""
   nStep := 1
   FOR nI := 1 TO 10 STEP nStep
      cTrail += Str(nI, 2) + " "
      nStep := nStep * 2
   NEXT

   QOut(cTrail)

RETURN
