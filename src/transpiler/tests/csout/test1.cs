// Test 1: PROCEDURE, LOCAL declarations, simple arithmetic
public static partial class Program
{
    public static void Main(string[] args)
    {
        decimal x = 5;
        decimal y = 10;
        decimal z = default;

        z = x + y;

        QOut("x=" + Str(x));
        QOut("y=" + Str(y));
        QOut("z=" + Str(z));

        return;
    }
}
