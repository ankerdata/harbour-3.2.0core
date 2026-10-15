using System;
using static HbRuntime;
using static Program;

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
public static partial class Program
{
    public static List<dynamic> aFlag;
    public static void Main(string[] args)
    {
        aFlag = HbRuntime.Array(3);

        aFlag[Ix(1)] = 10;
        aFlag[Ix(2)] = 20;
        aFlag[Ix(3)] = 30;
        HbRuntime.QOut("before:" + HbRuntime.Str(aFlag[Ix(1)], 4) + HbRuntime.Str(aFlag[Ix(2)], 4) + HbRuntime.Str(aFlag[Ix(3)], 4));

        /* AddDelta only mutates elements of its array parameter, so the array
      is passed plain (no `@`): element writes propagate through the shared
      reference, C# arrays being reference types. aFlag still emits as
      `dynamic[]` — the PUBLIC sized-array fix this test guards. */
        AddDelta(aFlag, 5);
        HbRuntime.QOut("after: " + HbRuntime.Str(aFlag[Ix(1)], 4) + HbRuntime.Str(aFlag[Ix(2)], 4) + HbRuntime.Str(aFlag[Ix(3)], 4));

        return;
    }

    public static void AddDelta(List<dynamic> aArr = default, decimal nDelta = default)
    {
        long nI = default;
        for (nI = 1; nI <= HbRuntime.Len(aArr); nI++)
        {
            aArr[Ix(nI)] = aArr[Ix(nI)] + nDelta;
        }

        return;
    }
}
