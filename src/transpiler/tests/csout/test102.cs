using System;
using static HbRuntime;
using static Program;

// Test 102: a file-static variadic function keeps its named parameters,
// and PCount() inside a widened function is the argument count.
//
// A function that reads PCount() is flagged variadic in the reftab and
// its C# signature widens to `params dynamic[] hbva`, the named
// parameters re-bound from the array at the top of the body. That
// re-bind looked the row up again by the bare name, and a file-static
// function's row is `file::func`, so trace.prg's PlatformDebug got the
// widened signature and none of its ten names (CS0103 ×10). PCount()
// itself emitted HbRuntime.PCount(), a stub returning 0 — C# has no
// caller argument count — so the ladder it guards never ran; inside a
// widened function it is now `hbva.Length`. Muster is the file-static
// shape, Convoy the public one.
public static partial class Program
{
    public static string test102_Muster(params dynamic[] hbva)
    {
        dynamic cA = hbva.Length > 0 ? hbva[0] : null;
        dynamic cB = hbva.Length > 1 ? hbva[1] : null;
        dynamic cC = hbva.Length > 2 ? hbva[2] : null;
        string cOut = "muster=" + HbRuntime.LTrim(HbRuntime.Str(hbva.Length)) + ":" + cA;
        if (hbva.Length >= 2)
        {
            cOut += "," + cB;
            if (hbva.Length >= 3)
            {
                cOut += "," + cC;
            }
        }

        return cOut;
    }

    public static dynamic Convoy(params dynamic[] hbva)
    {
        dynamic cFirst = hbva.Length > 0 ? hbva[0] : null;
        dynamic cSecond = hbva.Length > 1 ? hbva[1] : null;
        return "convoy=" + HbRuntime.LTrim(HbRuntime.Str(hbva.Length)) + ":" + (hbva.Length >= 2 ? cSecond : cFirst);
    }

    public static void Main(string[] args)
    {
        HbRuntime.QOut(test102_Muster("one"));
        HbRuntime.QOut(test102_Muster("one", "two"));
        HbRuntime.QOut(test102_Muster("one", "two", "three"));
        HbRuntime.QOut(Convoy("solo"));
        HbRuntime.QOut(Convoy("a", "b"));
        return;
    }
}
