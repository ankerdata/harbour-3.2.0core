using System;

// Test 17: by-reference parameters via @ at call sites
public static partial class Program
{
    public static void Main(string[] args)
    {
        decimal nA = 1;
        decimal nB = 2;

        HbRuntime.QOut("before: a=" + HbRuntime.Str(nA) + " b=" + HbRuntime.Str(nB));
        Swap(ref nA, ref nB);
        HbRuntime.QOut("after:  a=" + HbRuntime.Str(nA) + " b=" + HbRuntime.Str(nB));

        return;
    }

    public static void Swap(ref decimal nX, ref decimal nY)
    {
        decimal nT = nX;

        nX = nY;
        nY = nT;

        return;
    }
}
