using System;
using static HbRuntime;
using static Program;

// Test 45a: short-overload emission — typed parameters and caller-
// observed arity gating.
//
// test45a defines a function with a ref param, called from test45b with
// BOTH the full arity (@-ref form) and a short arity (just the first
// param). C# `ref` params are required, so the canonical
//
//    LoadFlags(string cFile, ref dynamic[] aFlag, decimal nCount = default)
//
// can't accept the 1-arg call. The emitter generates a short overload
// that covers arities 0..iFirstRef (= 0..1 here):
//
//    LoadFlags(string cFile = default)
//
// Two things this pair guards:
//   1. The short overload is emitted (else the 1-arg call CS1501s).
//   2. Its pre-ref parameter is typed `string` — Hungarian-inferred from
//      the `c` prefix — NOT `dynamic`. A dynamic short overload would
//      hide the typing the canonical exposes and collapse every pre-ref
//      slot back to untyped Harbour idiom.
//
// Companion test46 covers the other side: a function whose callers
// never use a short form must NOT emit the overload.
public static partial class Program
{
    public static void LoadFlags(string cFile, ref dynamic[] aFlag, decimal nCount = default)
    {
        decimal i = default;

        HbRuntime.ASize(aFlag, nCount);
        for (i = 1; i <= nCount; i++)
        {
            aFlag[(int)(i) - 1] = cFile + ":" + HbRuntime.Str(i, 1);
        }

        return;
    }

    public static void LoadFlags(string cFile = default)
    {
        dynamic[] _arg1 = default;
        decimal _arg2 = default;
        LoadFlags(cFile, ref _arg1, _arg2);
    }
}
