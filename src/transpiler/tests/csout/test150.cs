// Test 150: a call binds the short overload, and a short overload is
// emitted only where a call binds it.
//
// A function with a by-reference parameter gets a short overload for the
// callers that leave the `@` arguments out (test56, test65). The call
// emitter, though, padded every omitted slot up to the last `ref` with
// `ref HbDiscard<T>.Value` as soon as a call had one argument, so only a
// call with none ever bound the short overload: in EasiPOS 64 calls did,
// 368 short-arity calls went padded to the full overload, and 138 of its
// 147 short overloads were bound by nothing. Now a call whose arguments
// stop before the first `ref` is emitted as written and binds the short
// overload, an inner gap as `default(T?)`; a call that reaches the `ref`
// keeps the padding. And the short overload is emitted only where the
// scan recorded such a call: a file-static routine's calls are credited
// to its own row (they were recorded under the bare name, which no row
// holds, so every file-static routine with a `ref` parameter got one),
// and an empty record no longer means "never scanned, emit anyway".
public static partial class Program
{
    public static string Label150(string cPrefix, ref string cLabel, decimal nWidth = 8)
    {
        cLabel = PadR(cPrefix, nWidth, ".");
        return Upper(cLabel);
    }

    public static string Label150(string cPrefix = default)
    {
        string _arg1 = default;
        decimal _arg2 = 8;
        return Label150(cPrefix, ref _arg1, _arg2);
    }

    public static string Pair150(string? cA, string cB, ref bool lFlag)
    {
        if (cA == null)
        {
            cA = "-";
        }

        lFlag = (cA == cB);
        return cA + "/" + cB;
    }

    public static string Pair150(string? cA = null, string cB = default)
    {
        bool _arg2 = default;
        return Pair150(cA, cB, ref _arg2);
    }

    public static decimal test150_Scale150(decimal nValue, ref decimal nFactor)
    {
        // a short caller: emitted and bound
        nFactor = 10;
        return nValue * nFactor;
    }

    public static decimal test150_Scale150(decimal nValue = default)
    {
        decimal _arg1 = default;
        return test150_Scale150(nValue, ref _arg1);
    }

    public static string test150_Stamp150(string cText, ref bool lDone)
    {
        // full callers only: no short overload
        lDone = true;
        return "[" + cText + "]";
    }

    public static void Main(string[] args)
    {
        string cLabel = "";
        bool lFlag = false;
        decimal nFactor = 0;
        bool lDone = false;

        // stops before the ref: the short overload
        QOut(Label150("sale"));
        QOut(Label150("item", ref cLabel), cLabel);
        // reaches past the ref: padded
        QOut(Label150("tax", ref HbDiscard<string>.Value, 4));
        // an inner gap before the ref
        QOut(Pair150(default(string?), "b"));
        QOut(Pair150("a", "a", ref lFlag), lFlag);
        QOut(test150_Scale150(3));
        QOut(test150_Scale150(2, ref nFactor), nFactor);
        QOut(test150_Stamp150("x", ref lDone), lDone);
        QOut(test150_Stamp150("y", ref lDone));
        return;
    }
}
