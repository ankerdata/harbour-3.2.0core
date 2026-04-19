using System;
using static HbRuntime;
using static Program;

// Test 1: PROCEDURE, LOCAL declarations, simple arithmetic
public static partial class Program
{
    public static void Main(string[] args)
    {
        decimal x = 5;
        decimal y = 10;
        decimal z = default;

        z = x + y;

        HbRuntime.QOut("x=" + HbRuntime.Str(x));
        HbRuntime.QOut("y=" + HbRuntime.Str(y));
        HbRuntime.QOut("z=" + HbRuntime.Str(z));

        return;
    }
}
