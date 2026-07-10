using System;
using static HbRuntime;
using static Program;

// Test 82: numeric #define type tiering.
//
// A numeric #define is typed by its literal value: ANY integer ->
// `long` (the single integral tier — Harbour integers are 64-bit, no
// Int32 sub-tier), a fractional literal -> `decimal`. gendefines emits
// the const at that type AND records it in defines_map.txt; the
// transpiler (hbtypes.c) reads the map to type a reference the same
// way (both integral tokens infer as INTEGER, which emits C# long).
//
// Guarded two ways at once. Mistyping is a COMPILE error: a fractional
// declared integral will not convert (CS0266); BIGVAL exceeds Int32 so
// any regression back to an int tier overflows the literal (CS0031).
// And the RUNTIME values below must still match the Harbour (.prg)
// run, which catches wrong-but-compilable typing.
public static partial class Program
{
    public static void Main(string[] args)
    {
        // 7
        HbRuntime.QOut("small=" + HbRuntime.LTrim(HbRuntime.Str(Test82PrgConst.SMALLFLAG)));
        // 4294967297 (long)
        HbRuntime.QOut("big=" + HbRuntime.LTrim(HbRuntime.Str(Test82PrgConst.BIGVAL + 1)));
        // 3  (decimal 1.5*2)
        HbRuntime.QOut("rate=" + HbRuntime.LTrim(HbRuntime.Str(HbRuntime.Int(Test82PrgConst.RATE * 2))));
        return;
    }
}
