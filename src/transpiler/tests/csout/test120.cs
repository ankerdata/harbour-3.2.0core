using System;
using static HbRuntime;
using static Program;

// Test 120: an `i` name is a whole number, C# long.
//
// The Hungarian `n` is a Harbour number, C# decimal. An `i` name says the
// number is whole - a count, an index, a flag's integer value - and the
// transpiler takes it at its word, as it takes `AS INTEGER`: the local, the
// static and the parameter are long, a numeric initializer does not make
// them decimal again, and a decimal written into one is cast, as for a
// Pass 2.5 local. A value that may hold a fraction (a division, a power, a
// literal with decimals) is W0024 instead, so it is not in this test.
// Pinned: initializers, assignment from a decimal, the compound family, a
// FOR counter, an `i` parameter called with a number, an `i` value passed
// to an `n` parameter, a subscript, and a function returning an `i` local.
public static partial class Program
{
    public static long test120_siCalls120 = 0;
    public static void Main(string[] args)
    {
        dynamic[] aItems = new dynamic[] { "a", "b", "c", "d" };
        long iCount = 0;
        long iLen = (long)(HbRuntime.Len(aItems));
        decimal nPrice = 2.5m;
        long iPos = default;
        long iWhole = default;

        for (iPos = 1; iPos <= iLen; iPos++)
        {
            iCount += iPos;
        }

        HbRuntime.QOut("count:", iCount, iLen);

        iWhole = (long)(HbRuntime.Int(nPrice * 1.2m));
        HbRuntime.QOut("whole:", iWhole, aItems[iWhole - 1]);

        iWhole = (long)(HbRuntime.Round(nPrice, 0) + iLen);
        HbRuntime.QOut("rounded:", iWhole);

        HbRuntime.QOut("twice:", Twice120(iLen), Twice120(7), Twice120((long)(HbRuntime.Len("xyz"))));
        HbRuntime.QOut("priced:", Price120(iCount, nPrice));
        HbRuntime.QOut("calls:", test120_siCalls120);

        return;
    }

    public static long Twice120(long iValue = default)
    {
        long iResult = (long)(iValue * 2);
        test120_siCalls120++;
        return iResult;
    }

    public static decimal Price120(decimal nQty = default, decimal nPrice = default)
    {
        return nQty * nPrice;
    }
}
