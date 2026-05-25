// Test 51: PUBLIC sized-array (`PUBLIC name[size]`) emits as
// `dynamic[]`, not `dynamic`.
//
// The fArrayDim flag from the AST is propagated through the reftab
// (`A` flag) to the C# field-emit path. Without that propagation,
// the field declaration was `public static dynamic aFlag;` and any
// callee declared `ref dynamic[]` (the canonical shape for a
// callee whose parameter has the `a` Hungarian prefix) failed at
// the call site with:
//
//   CS1503: cannot convert from 'ref dynamic' to 'ref dynamic[]'
//
// Pattern observed in easipos: drinit.prg's `PUBLIC aDRFlag[F]`
// passed by-ref to LoadAFlag(cFile, aArr, nNoFlags) — 80+ sites
// in loadopt.cs alone, all the same failure.
//
// This test exercises the round-trip so the .prg, .hb, and .cs
// pipelines all build and produce identical output.

PROCEDURE Main()
   MEMVAR aFlag
   PUBLIC aFlag[3]

   aFlag[1] := 10
   aFlag[2] := 20
   aFlag[3] := 30
   ? "before:" + Str(aFlag[1], 4) + Str(aFlag[2], 4) + Str(aFlag[3], 4)

   /* AddDelta only mutates elements of its array parameter, so the array
      is passed plain (no `@`): element writes propagate through the shared
      reference, C# arrays being reference types. aFlag still emits as
      `dynamic[]` — the PUBLIC sized-array fix this test guards. */
   AddDelta(aFlag, 5)
   ? "after: " + Str(aFlag[1], 4) + Str(aFlag[2], 4) + Str(aFlag[3], 4)

RETURN

PROCEDURE AddDelta(aArr, nDelta)
   LOCAL nI
   FOR nI := 1 TO LEN(aArr)
      aArr[nI] := aArr[nI] + nDelta
   NEXT
RETURN
