#include "astype.ch"
// Test 67: array by-ref elision.
//
// An array parameter passed by-ref (`@aArr`) only needs C# `ref` when the
// callee REASSIGNS the whole variable — element mutation already
// propagates through the shared reference (C# arrays are reference types).
// So:
//   Scale()   mutates elements only  -> param emitted plain `dynamic[]`,
//             the call drops the `ref`/shim (and the transpiler warns
//             W0023 that the `@` is redundant).
//   Replace() reassigns the variable -> param stays `ref dynamic[]` so
//             the new array reaches the caller.
//
// Both forms must round-trip identically through -GT (Harbour) and -GS
// (C#): the elided one because element writes share the reference, the
// ref one because the reassignment is passed back.

FUNCTION Scale( /*@*/aArr AS ARRAY, nFactor AS NUMERIC ) AS NUMERIC
   // element mutation only
   LOCAL i AS NUMERIC
   FOR i := 1 TO Len(aArr)
      aArr[i] := aArr[i] * nFactor
   NEXT

RETURN Len(aArr)

FUNCTION Replace( /*@*/aArr AS ARRAY ) AS NUMERIC
   // reassigns the whole variable
   aArr := {7, 8, 9}
RETURN Len(aArr)

PROCEDURE Main()
   LOCAL aData := {1, 2, 3} AS ARRAY
   // elided: aData[i] mutated in place
   Scale(@aData, 10)
   QOut("scaled=" + Str(aData[1], 4) + Str(aData[2], 4) + Str(aData[3], 4))
   // ref kept: aData is repointed
   Replace(@aData)
   QOut("replaced=" + Str(aData[1], 4) + Str(aData[2], 4) + Str(aData[3], 4))
RETURN
