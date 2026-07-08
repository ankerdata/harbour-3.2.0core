using System;
using static HbRuntime;
using static Program;

// Test 64: a plain variable passed to a by-ref parameter without
// Harbour's `@`.
//
// Once a parameter is reached by-ref anywhere, the C# parameter emits
// `ref`, and C# then requires `ref` at EVERY call site (CS1620) —
// where Harbour happily takes a bare argument BY VALUE. The transpiler
// keeps that by-value semantics faithful: a bare argument emits
// `ref HbDiscard<T>.Seed(x)`, so the callee sees x's value but its
// write-back is discarded and the caller's x is untouched (see
// test80 for the full matrix). Emitting `ref x` would write back —
// the divergence W0020 warns about.
public static partial class Program
{
    public static void Main(string[] args)
    {
        decimal n = 5;

        // @ form marks the param by-ref
        Inc(ref n);
        // 15 — write-back
        HbRuntime.QOut("viaref=" + HbRuntime.LTrim(HbRuntime.Str(n)));

        n = 5;
        // Inc's RETURN is 15...
        HbRuntime.QOut("bare=" + HbRuntime.LTrim(HbRuntime.Str(Inc(ref HbDiscard<decimal>.Seed(n)))));
        // ...but n stays 5 (discarded)
        HbRuntime.QOut("kept=" + HbRuntime.LTrim(HbRuntime.Str(n)));
        return;
    }

    public static decimal Inc(ref decimal nVal)
    {
        nVal = nVal + 10;
        return nVal;
    }
}
