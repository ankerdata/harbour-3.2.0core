using System;
using static HbRuntime;
using static Program;

// Test 5: FOR with STEP, FOR counting down, FOR EACH DESCEND
public static partial class Program
{
    public static void Main(string[] args)
    {
        decimal i = default;
        decimal nSum = 0;
        dynamic[] aItems = new dynamic[] { "apple", "banana", "cherry" };

        // FOR with STEP
        for (i = 0; i <= 20; i += 5)
        {
            nSum = nSum + i;
        }

        HbRuntime.QOUT("nSum after STEP=" + HbRuntime.STR(nSum));

        // FOR counting down with negative STEP
        for (i = 10; i >= 1; i += -1)
        {
            nSum = nSum + i;
        }

        HbRuntime.QOUT("nSum after DOWN=" + HbRuntime.STR(nSum));

        // FOR EACH with DESCEND
        foreach (dynamic cItem in aItems.Reverse())
        {
            nSum = nSum + HbRuntime.LEN(cItem);
        }

        HbRuntime.QOUT("nSum=" + HbRuntime.STR(nSum));

        return;
    }
}
