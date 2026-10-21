// Test 56: a function whose FIRST parameter is by-reference, called
// both bare (for its return value) and with `@arg`.
//
// The Harbour idiom `FUNCTION GetQty(lDecimalQty)` where the param
// is treated by-ref — most callers want only the return value and
// call `GetQty()`, a few pass `@lDecimalQty` to receive the side
// output. In C# a `ref` param is mandatory, so the emit needs a
// parameterless short overload forwarding to the canonical
// `GetQty(ref bool)`.
//
// The short-overload machinery was gated `iFirstRef > 0`, which
// excluded first-param-ref functions like this one — bare calls
// hit CS7036. Relaxing the gate to `iFirstRef >= 0` emits the
// parameterless overload. This test pins that path.
public static partial class Program
{
    public static void Main(string[] args)
    {
        bool lDec = false;

        QOut("bare=" + LTrim(Str(GetQty())));
        QOut("ref_ret=" + LTrim(Str(GetQty(ref lDec))));
        QOut("ref_out=" + (lDec ? "Y" : "N"));

        /* First (and only) param is by-ref: the `@lDec` call site above
   marks slot 0 as ref in the reftab. */
    }
    public static decimal GetQty(ref bool lDecimalQty)
    {
        lDecimalQty = true;
        return 42;
    }

    public static decimal GetQty()
    {
        bool _arg0 = false;
        return GetQty(ref _arg0);
    }
}
