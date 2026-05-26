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
    public static dynamic[] test70_saTotals = new dynamic[(int)(3)];
    public static void Main(string[] args)
    {
        // 1-D dim'd LOCAL
        dynamic[] aConfirm = new dynamic[(int)(5)];
        // 2-D — only outer dim sized eagerly
        dynamic[] aGrid = new dynamic[(int)(2)];
        decimal nI = default;

        for (nI = 1; nI <= 3; nI++)
        {
            test70_saTotals[(int)(nI) - 1] = nI * 10;
        }

        aConfirm[0] = "first";
        aConfirm[4] = "fifth";

        aGrid[0] = new dynamic[] { "a", "b", "c" };
        aGrid[1] = new dynamic[] { "d", "e", "f" };

        HbRuntime.QOut("saTotals=" + HbRuntime.Str(test70_saTotals[0], 2) + "," + HbRuntime.Str(test70_saTotals[1], 2) + "," + HbRuntime.Str(test70_saTotals[2], 2));
        HbRuntime.QOut("aConfirm=" + aConfirm[0] + "/" + aConfirm[4]);
        HbRuntime.QOut("aGrid=" + aGrid[0][0] + aGrid[0][2] + "|" + aGrid[1][1]);
        return;
    }
}
