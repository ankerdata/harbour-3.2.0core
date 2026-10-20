// Test 17: by-reference parameters via @ at call sites
public static partial class Program
{
    public static void Main(string[] args)
    {
        decimal nA = 1;
        decimal nB = 2;

        QOut("before: a=" + Str(nA) + " b=" + Str(nB));
        Swap(ref nA, ref nB);
        QOut("after:  a=" + Str(nA) + " b=" + Str(nB));
    }

    public static void Swap(ref decimal nX, ref decimal nY)
    {
        decimal nT = nX;

        nX = nY;
        nY = nT;
    }
}
