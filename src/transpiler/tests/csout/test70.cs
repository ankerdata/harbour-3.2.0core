using System;
using static HbRuntime;
using static Program;

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
public static partial class Program
{
    public static List<dynamic> test70_saTotals = HbRuntime.Array(3);
    public static void Main(string[] args)
    {
        // 1-D dim'd LOCAL
        List<dynamic> aConfirm = HbRuntime.Array(5);
        // 2-D — only outer dim sized eagerly
        List<dynamic> aGrid = HbRuntime.Array(2, 3);
        long nI = default;

        for (nI = 1; nI <= 3; nI++)
        {
            test70_saTotals[Ix(nI)] = nI * 10;
        }

        aConfirm[Ix(1)] = "first";
        aConfirm[Ix(5)] = "fifth";

        aGrid[Ix(1)] = new List<dynamic> { "a", "b", "c" };
        aGrid[Ix(2)] = new List<dynamic> { "d", "e", "f" };

        HbRuntime.QOut("saTotals=" + HbRuntime.Str(test70_saTotals[Ix(1)], 2) + "," + HbRuntime.Str(test70_saTotals[Ix(2)], 2) + "," + HbRuntime.Str(test70_saTotals[Ix(3)], 2));
        HbRuntime.QOut("aConfirm=" + aConfirm[Ix(1)] + "/" + aConfirm[Ix(5)]);
        HbRuntime.QOut("aGrid=" + aGrid[Ix(1)][Ix(1)] + aGrid[Ix(1)][Ix(3)] + "|" + aGrid[Ix(2)][Ix(2)]);
        return;
    }
}
