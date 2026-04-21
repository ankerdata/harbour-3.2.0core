using System;
using static HbRuntime;
using static Program;

// Test 45b: Main that calls LoadFlags (from test45a.prg) at both
// arities. See test45a.prg for the short-overload rationale.
public static partial class Program
{
    public static void Main(string[] args)
    {
        dynamic[] aArr = new dynamic[] {  };

        LoadFlags("full.dat", ref aArr, 4);
        HbRuntime.QOut("full[1]=" + aArr[0]);
        HbRuntime.QOut("full[4]=" + aArr[3]);

        LoadFlags("short.dat");
        HbRuntime.QOut("short returned");

        return;
    }
}
