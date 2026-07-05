using System;
using static HbRuntime;
using static Program;

// Test 77: function-scope STATIC isolation.
//
// Harbour scopes a STATIC declared inside a function body to that
// function only — two functions each declaring `STATIC nCounter` are
// two independent variables that persist across calls. The transpiler
// used to register statics by name alone, merging both into a single
// shared `<File>_nCounter` class field (wrong values at runtime, or
// CS0102 if emitted twice). Function statics now mangle as
// `<File>_<Func>_<Var>`; file-level statics keep `<File>_<Var>` and
// stay shared across the file's functions.
public static partial class Program
{
    public static decimal test77_snShared = 100;
    public static decimal test77_Bump_nCounter = 0;
    public static decimal test77_Count_nCounter = 0;
    public static void Main(string[] args)
    {
        // 1  — Bump's own counter
        HbRuntime.QOut("a=", Bump());
        // 2
        HbRuntime.QOut("b=", Bump());
        // 1  — Count's counter, NOT Bump's
        HbRuntime.QOut("c=", Count());
        // 3
        HbRuntime.QOut("d=", Bump());
        // 2
        HbRuntime.QOut("e=", Count());
        // 110 — file-level static, shared
        HbRuntime.QOut("f=", AddShared(10));
        // 120
        HbRuntime.QOut("g=", AddShared(10));
        // 120 — same file-level static elsewhere
        HbRuntime.QOut("h=", PeekShared());

        return;
    }

    public static decimal Bump()
    {
        test77_Bump_nCounter++;
        return test77_Bump_nCounter;
    }

    public static decimal Count()
    {
        test77_Count_nCounter++;
        return test77_Count_nCounter;
    }

    public static decimal AddShared(decimal nInc = default)
    {
        test77_snShared += nInc;
        return test77_snShared;
    }

    public static decimal PeekShared()
    {
        return test77_snShared;
    }
}
