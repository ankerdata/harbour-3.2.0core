using System;
using static HbRuntime;
using static Program;

// Test 19b: calls Swap (defined in test19a.prg) with @ args.
// See test19a.prg for the cross-file scan workflow this test exercises.
public static partial class Program
{
    public static void Main(string[] args)
    {
        decimal nA = 1;
        decimal nB = 2;

        HbRuntime.QOUT("before: a=" + HbRuntime.STR(nA) + " b=" + HbRuntime.STR(nB));
        Swap(ref nA, ref nB);
        HbRuntime.QOUT("after:  a=" + HbRuntime.STR(nA) + " b=" + HbRuntime.STR(nB));

        return;
    }
}
