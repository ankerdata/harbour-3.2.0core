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
// Both forms must behave identically in Harbour and in C#: the elided
// one because element writes share the reference, the ref one because
// the reassignment is passed back.
public static partial class Program
{
    public static decimal Scale(List<dynamic> aArr, decimal nFactor = default)
    {
        // element mutation only
        for (long i = 1; i <= Len(aArr); i++)
        {
            aArr[I(i)] = aArr[I(i)] * nFactor;
        }

        return Len(aArr);
    }

    public static decimal Replace(ref List<dynamic> aArr)
    {
        // reassigns the whole variable
        aArr = new List<dynamic> { 7, 8, 9 };
        return Len(aArr);
    }

    public static void Main(string[] args)
    {
        var aData = new List<dynamic> { 1, 2, 3 };
        // elided: aData[i] mutated in place
        Scale(aData, 10);
        QOut("scaled=" + Str(aData[I(1)], 4) + Str(aData[I(2)], 4) + Str(aData[I(3)], 4));
        // ref kept: aData is repointed
        Replace(ref aData);
        QOut("replaced=" + Str(aData[I(1)], 4) + Str(aData[I(2)], 4) + Str(aData[I(3)], 4));
    }
}
