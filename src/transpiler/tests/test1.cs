using System;

// Test 1: PROCEDURE, LOCAL declarations, simple arithmetic
public static class Program
{
    public static void Main(string[] args)
    {
        decimal x = 5;
        decimal y = 10;
        decimal z;

        z = x + y;

        HbRuntime.QOut("x=" + HbRuntime.Str(x));
        HbRuntime.QOut("y=" + HbRuntime.Str(y));
        HbRuntime.QOut("z=" + HbRuntime.Str(z));

        return;
    }
}
