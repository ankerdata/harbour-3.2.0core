#include "astype.ch"
// Test 70: Clipper sized-array declarations for LOCAL and STATIC.
//
// `LOCAL aFoo[N]` and `STATIC aBar[N]` declare an array of N nil-filled
// slots in a single statement — the classic Clipper "dim" form. The
// grammar parses these via `IdentName DimList AsArrayType` and previously
// dispatched only to the PCODE path (hb_compVariableDim), which the
// transpiler ignores; the AST never saw the declaration, so every
// reference to aFoo / aBar became CS0103 in the generated C#.
//
// Now the same production also adds an HB_AST_LOCAL or HB_AST_STATIC
// node with fArrayDim=true, and the emitter's existing fArrayDim branch
// turns it into `dynamic[] aFoo = new dynamic[(int)(N)];`. Multi-dim
// (`aGrid[3][2]`) sizes by the outer dim only — inner dims are lazy
// per Harbour semantics — so we exercise that shape too by assigning
// a sub-array before reading from it.
//
// The STATIC arm here is module-scope (file static, not a function
// local), proving the same machinery picks up scopes that ride the
// HB_VSCOMP_STATIC bit (HB_VSCOMP_TH_STATIC = STATIC | THREAD).
//
// Output must round-trip identically through -GT and -GS.

STATIC saTotals[3] AS ARRAY

PROCEDURE Main()
   // 1-D dim'd LOCAL
   LOCAL aConfirm[5] AS ARRAY
   // 2-D — only outer dim sized eagerly
   LOCAL aGrid[2][3] AS ARRAY
   LOCAL nI AS NUMERIC

   FOR nI := 1 TO 3
      saTotals[nI] := nI * 10
   NEXT

   aConfirm[1] := "first"
   aConfirm[5] := "fifth"

   aGrid[1] := {"a", "b", "c"}
   aGrid[2] := {"d", "e", "f"}

   QOut("saTotals=" + Str(saTotals[1], 2) + "," + Str(saTotals[2], 2) + "," + Str(saTotals[3], 2))
   QOut("aConfirm=" + aConfirm[1] + "/" + aConfirm[5])
   QOut("aGrid=" + aGrid[1][1] + aGrid[1][3] + "|" + aGrid[2][2])
RETURN
