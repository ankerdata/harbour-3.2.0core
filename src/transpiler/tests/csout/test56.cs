using System;
using static HbRuntime;
using static Program;

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

        HbRuntime.QOut("bare=" + HbRuntime.LTrim(HbRuntime.Str(GetQty())));
        HbRuntime.QOut("ref_ret=" + HbRuntime.LTrim(HbRuntime.Str(GetQty(ref lDec))));
        HbRuntime.QOut("ref_out=" + (lDec ? "Y" : "N"));
        return;

        /* First (and only) param is by-ref: the `@lDec` call site above
   marks slot 0 as ref in the reftab. */
    }
    public static decimal GetQty(ref bool lDecimalQty)
    {
        lDecimalQty = true;
        return 42;
    }

    public static dynamic GetQty()
    {
        bool _arg0 = default;
        return GetQty(ref _arg0);
    }
}
