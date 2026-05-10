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
    public static dynamic[] aFlag;
    public static void Main(string[] args)
    {
        aFlag = new dynamic[(int)(3)];

        aFlag[0] = 10;
        aFlag[1] = 20;
        aFlag[2] = 30;
        HbRuntime.QOut("before:" + HbRuntime.Str(aFlag[0], 4) + HbRuntime.Str(aFlag[1], 4) + HbRuntime.Str(aFlag[2], 4));

        /* AddDelta's `aArr` parameter has the `a` Hungarian prefix, so
      the reftab types it as array. Passing `@aFlag` here would be
      `ref dynamic` against `ref dynamic[]` — exactly the CS1503
      pattern this test guards against. */
        AddDelta(ref aFlag, 5);
        HbRuntime.QOut("after: " + HbRuntime.Str(aFlag[0], 4) + HbRuntime.Str(aFlag[1], 4) + HbRuntime.Str(aFlag[2], 4));

        return;
    }

    public static void AddDelta(ref dynamic[] aArr, decimal nDelta = default)
    {
        decimal nI = default;
        for (nI = 1; nI <= HbRuntime.Len(aArr); nI++)
        {
            aArr[(int)(nI) - 1] = aArr[(int)(nI) - 1] + nDelta;
        }

        return;
    }
}
