using System;

// Test 17: by-reference parameters via @ at call sites
public static partial class Program
{
    public static void Main(string[] args)
    {
        decimal nA = 1;
        decimal nB = 2;

        HbRuntime.QOUT("before: a=" + HbRuntime.STR(nA) + " b=" + HbRuntime.STR(nB));
        Swap(ref nA, ref nB);
        HbRuntime.QOUT("after:  a=" + HbRuntime.STR(nA) + " b=" + HbRuntime.STR(nB));

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
