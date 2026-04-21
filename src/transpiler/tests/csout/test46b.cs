using System;
using static HbRuntime;
using static Program;

// Test 46b: every call to SaveFlags uses the full arity, so the
// emitter's short-overload guard sees no bit in 0..iFirstRef and
// skips overload emission. See test46a.prg for details.
public static partial class Program
{
    public static void Main(string[] args)
    {
        dynamic[] aArr = new dynamic[] {  };

        SaveFlags("out.dat", ref aArr, 3);

        HbRuntime.QOut("saved[1]=" + aArr[0]);
        HbRuntime.QOut("saved[3]=" + aArr[2]);

        return;
    }
}
