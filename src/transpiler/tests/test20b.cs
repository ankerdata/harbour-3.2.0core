using System;

// Test 20b: calls DoubleIt (defined in test20a.prg) from QuadrupleIt
// and from Main. See test20a.prg for the cross-file return-type
// propagation rationale.
public static partial class Program
{
    public static decimal QuadrupleIt(decimal n = default)
    {
        return DoubleIt(DoubleIt(n));
    }

    public static void Main(string[] args)
    {
        HbRuntime.QOut("QuadrupleIt(5)=" + HbRuntime.Str(QuadrupleIt(5)));
        return;
    }
}
