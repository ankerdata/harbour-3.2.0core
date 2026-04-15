using System;
using static HbRuntime;

// Test 18: default parameters and middle-gap call sites
public static partial class Program
{
    public static void Main(string[] args)
    {
        Fred(1);
        Fred(10, 20);
        Fred(100, 200, 300);
        // middle gap → named args in C#
        Fred(1000, nC: 3000);

        return;
    }

    public static void Fred(decimal nA = default, decimal? nB = null, decimal? nC = null)
    {
        HbRuntime.QOUT("a=" + HbRuntime.STR(nA));
        if (nB != null)
        {
            HbRuntime.QOUT("b=" + HbRuntime.STR(nB));
        }

        if (nC != null)
        {
            HbRuntime.QOUT("c=" + HbRuntime.STR(nC));
        }

        return;
    }
}
