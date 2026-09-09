using System;
using static HbRuntime;
using static Program;

// Test 100: a local shadowing a function's name; ++ / -- on a date.
//
// Harbour resolves `name( ... )` as a function call whatever locals are
// in scope; C# sees the local and refuses (CS0149 "Method name
// expected": easipayback's `local nTax` beside the tax.prg function
// `nTax()`). A user-function call whose name a local or parameter
// shadows (here `nLevy`, a local and a function) is qualified
// `Program.name( ... )`, the class every free function lives in — the
// same qualification a user `ToString` gets against object.ToString.
// `dDate++` / `--dDate` is date arithmetic in Harbour; C# DateOnly has
// no ++, so a DATE (or TIMESTAMP) operand emits `d = d.AddDays( ±1 )`
// (xzutil's `--dStart`, CS0023).
public static partial class Program
{
    public static decimal nLevy(decimal nKind = default)
    {
        return nKind * 10;
    }

    public static void Main(string[] args)
    {
        decimal nLevy = 5;
        DateOnly dDay = HbRuntime.SToD("20260909");
        HbRuntime.QOut("levy=" + HbRuntime.LTrim(HbRuntime.Str(Program.nLevy(2) + nLevy)));
        dDay = dDay.AddDays(1);
        HbRuntime.QOut("next=" + HbRuntime.DToS(dDay));
        dDay = dDay.AddDays(-1);
        dDay = dDay.AddDays(-1);
        HbRuntime.QOut("prev=" + HbRuntime.DToS(dDay));
        return;
    }
}
