using System;
using static HbRuntime;
using static Program;

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
public static partial class Program
{
    public static decimal Scale(dynamic[] aArr, decimal nFactor = default)
    {
        // element mutation only
        decimal i = default;
        for (i = 1; i <= HbRuntime.Len(aArr); i++)
        {
            aArr[(int)(i) - 1] = aArr[(int)(i) - 1] * nFactor;
        }

        return HbRuntime.Len(aArr);
    }

    public static decimal Replace(ref dynamic[] aArr)
    {
        // reassigns the whole variable
        aArr = new dynamic[] { 7, 8, 9 };
        return HbRuntime.Len(aArr);
    }

    public static void Main(string[] args)
    {
        dynamic[] aData = new dynamic[] { 1, 2, 3 };
        // elided: aData[i] mutated in place
        Scale(aData, 10);
        HbRuntime.QOut("scaled=" + HbRuntime.Str(aData[0], 4) + HbRuntime.Str(aData[1], 4) + HbRuntime.Str(aData[2], 4));
        // ref kept: aData is repointed
        Replace(ref aData);
        HbRuntime.QOut("replaced=" + HbRuntime.Str(aData[0], 4) + HbRuntime.Str(aData[1], 4) + HbRuntime.Str(aData[2], 4));
        return;
    }
}
