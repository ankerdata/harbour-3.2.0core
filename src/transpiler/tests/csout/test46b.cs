using System;
using static HbRuntime;
using static Program;

// Test 46b: every call to SaveFlags uses the full arity. See
// test46a.prg.
public static partial class Program
{
    public static void Main(string[] args)
    {
        List<dynamic> aArr = new List<dynamic> {  };

        SaveFlags("out.dat", aArr, 3);

        QOut("saved[1]=" + aArr[Ix(1)]);
        QOut("saved[3]=" + aArr[Ix(3)]);

        return;
    }
}
