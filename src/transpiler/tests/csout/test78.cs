using System;
using static HbRuntime;
using static Program;

// Test 78: INTEGER inference (Pass 2.5 int candidacy).
//
// decimal stays the default for Harbour numerics, but a variable whose
// numeric life is purely index-shaped — array subscripts, FOR loop
// variables, integral initializers/arithmetic (literals, Len(), other
// INTEGER vars) — emits as C# `int`: subscripts drop the (int) cast,
// FOR loops declare int, and int widens to decimal implicitly at every
// consumer boundary. Division is the hard disqualifier (Harbour 5/2 is
// 2.5, C# int/int truncates): a division-fed variable stays decimal
// and, when used as an index, earns W0026 pointing at the division so
// the source can decide (wrap with int() or keep decimal). W0026 is
// expected on nHalf below.
public static partial class Program
{
    public static void Main(string[] args)
    {
        dynamic[] aItems = new dynamic[] { "alpha", "beta", "gamma", "delta" };
        int i = default;
        int nIdx = 1;
        int nLast = (int)(HbRuntime.Len(aItems));
        // division → stays decimal, W0026
        decimal nHalf = 4 / 2;

        for (i = 1; i <= HbRuntime.Len(aItems); i++)
        {
            HbRuntime.QOut("i=", aItems[i - 1]);
        }

        // integral arithmetic keeps int
        nIdx = (int)(nIdx + 2);
        HbRuntime.QOut("a=", aItems[nIdx - 1]);
        HbRuntime.QOut("b=", aItems[nLast - 1]);
        // decimal index — cast path
        HbRuntime.QOut("c=", aItems[(int)(nHalf) - 1]);
        HbRuntime.QOut("d=", aItems[(int)(FirstReal(aItems)) - 1]);
        // int widens into decimal math
        HbRuntime.QOut("e=", HbRuntime.Str(nIdx * 1.5m, 6, 1));
        // (explicit width: Harbour's
        // derived display widths for
        // var*literal aren't modelled)

        return;

        // Returns an always-int local: the function's return type resolves to
        // INTEGER and callers may chain it straight into subscripts.
    }
    public static int FirstReal(dynamic[] aList = default)
    {
        int nPos = 1;

        while (nPos < HbRuntime.Len(aList) && HbRuntime.Empty(aList[nPos - 1]))
        {
            nPos = (int)(nPos + 1);
        }

        return nPos;
    }
}
