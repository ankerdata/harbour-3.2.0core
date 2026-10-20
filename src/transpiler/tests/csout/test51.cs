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
// The .prg and the .cs builds of this test must both build and
// produce identical output.
public static partial class Program
{
    public static List<dynamic> aFlag;
    public static void Main(string[] args)
    {
        aFlag = Array(3);

        aFlag[Ix(1)] = 10;
        aFlag[Ix(2)] = 20;
        aFlag[Ix(3)] = 30;
        QOut("before:" + Str(aFlag[Ix(1)], 4) + Str(aFlag[Ix(2)], 4) + Str(aFlag[Ix(3)], 4));

        /* AddDelta only mutates elements of its array parameter, so the array
      is passed plain (no `@`): element writes propagate through the shared
      reference, C# arrays being reference types. aFlag still emits as
      `dynamic[]` — the PUBLIC sized-array fix this test guards. */
        AddDelta(aFlag, 5);
        QOut("after: " + Str(aFlag[Ix(1)], 4) + Str(aFlag[Ix(2)], 4) + Str(aFlag[Ix(3)], 4));

        return;
    }

    public static void AddDelta(List<dynamic> aArr = default, decimal nDelta = default)
    {
        long nI = default;
        for (nI = 1; nI <= Len(aArr); nI++)
        {
            aArr[Ix(nI)] = aArr[Ix(nI)] + nDelta;
        }

        return;
    }
}
