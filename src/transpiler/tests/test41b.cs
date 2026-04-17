using System;
using static HbRuntime;

// Test 41 (multi-file pair): see test41a.prg for rationale.
//
// Declares STATIC Helper() / HelperNum() with different bodies so
// the merged partial class Program has two file-private copies of
// each, mangled test41a_Helper / test41b_Helper etc.
public static partial class Program
{
    public static string HelperCallerB()
    {
        return "41b: " + test41b_Helper() + ", " + HbRuntime.STR(test41b_HelperNum(), 4);
    }

    public static string test41b_Helper()
    {
        return "from-b";
    }

    public static decimal test41b_HelperNum()
    {
        return 99;
    }
}
