using System;
using static HbRuntime;
using static Program;

// Test 82: numeric #define type tiering.
//
// A numeric #define is typed by its literal value: an integer that fits
// Int32 -> `int`, an integer beyond Int32 -> `long` (bit-flag masks /
// large ids), a fractional literal -> `decimal`. gendefines emits the
// const at that type AND records it in defines_map.txt; the transpiler
// (hbtypes.c) reads the map to type a reference the same way.
//
// This is guarded two ways at once. Mistyping is a COMPILE error: a
// >Int32 value declared `int` overflows the literal (CS0031); a
// fractional declared `int` will not convert (CS0266) — so if the long
// or decimal tier regressed, buildcs.sh would fail here. And the
// RUNTIME values below must still match the Harbour (.prg) run, which
// catches a wrong-but-compilable typing.
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
