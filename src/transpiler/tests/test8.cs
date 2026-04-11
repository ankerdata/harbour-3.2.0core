using System;

// Test 8: All comment types with code interspersed
public static partial class Program
{
    public static void Main(string[] args)
    {
        // Double-slash inside function
        // trailing comment on code line
        decimal x = 5;

        // dBASE-style comment
        // trailing dBASE comment
        decimal y = 10;

        // Star comment at start of line
        decimal z = x + y;

        // Old-style comment keyword

        /* Single-line block comment */
        string cResult = "hello";

        HbRuntime.QOUT("x=" + HbRuntime.STR(x));
        HbRuntime.QOUT("y=" + HbRuntime.STR(y));
        HbRuntime.QOUT("z=" + HbRuntime.STR(z));
        HbRuntime.QOUT("cResult=" + cResult);

        /* Multi-line
      block comment
      spanning three lines */
        cResult += " world";
        HbRuntime.QOUT("cResult=" + cResult);

        /* inline block comment */
        x += 1;
        HbRuntime.QOUT("x=" + HbRuntime.STR(x));

        return;
    }
}
