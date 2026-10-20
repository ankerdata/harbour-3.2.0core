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

        QOut("x=" + Str(x));
        QOut("y=" + Str(y));
        QOut("z=" + Str(z));
        QOut("cResult=" + cResult);

        /* Multi-line
      block comment
      spanning three lines */
        cResult = cResult + " world";
        QOut("cResult=" + cResult);

        /* inline block comment */
        x = x + 1;
        QOut("x=" + Str(x));

        return;
    }
}
