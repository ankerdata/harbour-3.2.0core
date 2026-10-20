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
// The output must be the same in Harbour and in C#.
public static partial class Program
{
    public static List<dynamic> test70_saTotals = Array(3);
    public static void Main(string[] args)
    {
        // 1-D dim'd LOCAL
        List<dynamic> aConfirm = Array(5);
        // 2-D — only outer dim sized eagerly
        List<dynamic> aGrid = Array(2, 3);
        long nI = default;

        for (nI = 1; nI <= 3; nI++)
        {
            test70_saTotals[I(nI)] = nI * 10;
        }

        aConfirm[I(1)] = "first";
        aConfirm[I(5)] = "fifth";

        aGrid[I(1)] = new List<dynamic> { "a", "b", "c" };
        aGrid[I(2)] = new List<dynamic> { "d", "e", "f" };

        QOut("saTotals=" + Str(test70_saTotals[I(1)], 2) + "," + Str(test70_saTotals[I(2)], 2) + "," + Str(test70_saTotals[I(3)], 2));
        QOut("aConfirm=" + aConfirm[I(1)] + "/" + aConfirm[I(5)]);
        QOut("aGrid=" + aGrid[I(1)][I(1)] + aGrid[I(1)][I(3)] + "|" + aGrid[I(2)][I(2)]);
    }
}
