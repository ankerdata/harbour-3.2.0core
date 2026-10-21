// Test 149: the short overload returns what the canonical returns.
//
// A function with a by-reference parameter gets a second, shorter C#
// overload for the callers that leave the `@` arguments out (test56,
// test65). It forwards to the canonical and answers its result, but it
// was declared `dynamic`, so every short call lost the type the
// canonical had: in EasiPOS 115 routines, `decimal GetQty( ref bool )`
// beside `dynamic GetQty()`. The short overload now carries the
// canonical's return type: long for the counter, string for the label,
// bool for the test; a PROCEDURE stays void.
public static partial class Program
{
    public static long test149_siCalls149 = 0;
    public static long Calls149(ref bool lFirst)
    {
        long iCalls;
        test149_siCalls149++;
        lFirst = (test149_siCalls149 == 1);
        iCalls = test149_siCalls149;
        return iCalls;
    }

    public static long Calls149()
    {
        bool _arg0 = false;
        return Calls149(ref _arg0);
    }

    public static string Label149(string cPrefix, ref string cLabel, decimal nWidth = 8)
    {
        string cOut;
        cLabel = PadR(cPrefix + "-" + Str(Calls149(), 1), nWidth, ".");
        cOut = Upper(cLabel);
        return cOut;
    }

    public static string Label149(string cPrefix = default)
    {
        string _arg1 = null;
        decimal _arg2 = 8;
        return Label149(cPrefix, ref _arg1, _arg2);
    }

    public static bool Even149(decimal nValue, ref bool lOdd)
    {
        lOdd = (nValue % 2 == 1);
        return !lOdd;
    }

    public static bool Even149(decimal nValue = default)
    {
        bool _arg1 = false;
        return Even149(nValue, ref _arg1);
    }

    public static void Note149(string cText, ref decimal nLength)
    {
        nLength = Len(cText);
        QOut("note", cText);
    }

    public static void Note149(string cText = default)
    {
        decimal _arg1 = 0;
        Note149(cText, ref _arg1);
    }

    public static void Main(string[] args)
    {
        bool lFirst = false;
        string cLabel = "";
        decimal nLength = 0;
        bool lOdd = false;
        decimal nHalf;

        // the full call, by reference
        QOut(Calls149(ref lFirst), lFirst);
        // the short call: a long
        QOut(Calls149());
        nHalf = ((decimal)(Calls149()) / 2);
        // the whole-number return divides as Harbour's
        QOut(Str(nHalf, 5, 2), Str((decimal)(Calls149()) / 2, 5, 2));

        // the full call, the width defaulted
        QOut(Label149("item", ref cLabel), cLabel);
        // the short call: a string
        QOut(Label149("sale"));
        QOut(Len(Label149("tax")));

        QOut(Even149(4, ref lOdd), lOdd);
        // the short call: a logical
        QOut(Even149(7));
        if (Even149(10))
        {
            QOut("ten is even");
        }

        Note149("full", ref nLength);
        QOut(nLength);
        // the short call of a procedure: void
        Note149("short");
    }
}
