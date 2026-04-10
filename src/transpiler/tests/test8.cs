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

        HbRuntime.QOut("x=" + HbRuntime.Str(x));
        HbRuntime.QOut("y=" + HbRuntime.Str(y));
        HbRuntime.QOut("z=" + HbRuntime.Str(z));
        HbRuntime.QOut("cResult=" + cResult);

        /* Multi-line
      block comment
      spanning three lines */
        cResult += " world";
        HbRuntime.QOut("cResult=" + cResult);

        /* inline block comment */
        x += 1;
        HbRuntime.QOut("x=" + HbRuntime.Str(x));

        return;
    }
}
