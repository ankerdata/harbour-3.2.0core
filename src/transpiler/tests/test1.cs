using System;
using static HbRuntime;

// Test 1: PROCEDURE, LOCAL declarations, simple arithmetic
public static partial class Program
{
    public static void Main(string[] args)
    {
        decimal x = 5;
        decimal y = 10;
        decimal z = default;

        z = x + y;

        HbRuntime.QOUT("x=" + HbRuntime.STR(x));
        HbRuntime.QOUT("y=" + HbRuntime.STR(y));
        HbRuntime.QOUT("z=" + HbRuntime.STR(z));

        return;
    }
}
