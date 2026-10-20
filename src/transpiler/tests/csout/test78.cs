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
        List<dynamic> aItems = new List<dynamic> { "alpha", "beta", "gamma", "delta" };
        long i = default;
        long nIdx = 1;
        long nLast = (long)(Len(aItems));
        // division → stays decimal, W0026
        decimal nHalf = (decimal)(4) / 2;

        for (i = 1; i <= Len(aItems); i++)
        {
            QOut("i=", aItems[Ix(i)]);
        }

        // integral arithmetic keeps int
        nIdx = nIdx + 2;
        QOut("a=", aItems[Ix(nIdx)]);
        QOut("b=", aItems[Ix(nLast)]);
        // decimal index — cast path
        QOut("c=", aItems[Ix(nHalf)]);
        QOut("d=", aItems[Ix(FirstReal(aItems))]);
        // int widens into decimal math
        QOut("e=", Str(nIdx * 1.5m, 6, 1));
        // (explicit width: Harbour's
        // derived display widths for
        // var*literal aren't modelled)

        return;

        // Returns an always-int local: the function's return type resolves to
        // INTEGER and callers may chain it straight into subscripts.
    }
    public static long FirstReal(List<dynamic> aList = default)
    {
        long nPos = 1;

        while (nPos < Len(aList) && Empty(aList[Ix(nPos)]))
        {
            nPos = nPos + 1;
        }

        return nPos;
    }
}
