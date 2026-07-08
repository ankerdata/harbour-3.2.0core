using System;
using static HbRuntime;
using static Program;

// Test 80: faithful by-value semantics for un-@'d arguments.
//
// Harbour passes parameters BY VALUE; `@` opts into by-reference. C#
// `ref` is per-signature, so a parameter that ANY caller @-passes
// emits `ref` for every caller — including the ones who wrote a bare
// argument and, in Harbour, expect NO write-back.
//
// The transpiler reconciles those un-@'d callers with HbDiscard:
//   - a bare variable  -> ref HbDiscard<T>.Seed(x): the callee still
//     sees x's value as input, but the write-back is dropped and the
//     caller's x is untouched (in-out and out-only both faithful);
//   - a literal / omitted slot -> ref HbDiscard<T>.Value (no input to
//     preserve).
// Emitting `ref x` for the bare case would write back — the silent
// divergence from Harbour that W0020 warns about; this test pins the
// correct behaviour.
public static partial class Program
{
    public static void Main(string[] args)
    {
        decimal nRef = 10;
        decimal nBare = 10;
        decimal nInOut = 10;
        string cRef = "keep";
        string cBare = "keep";

        // AddTo has an @-caller below, so nTarget emits as C# `ref`.
        // by ref   -> nRef becomes 15
        AddTo(ref nRef, 5);
        // by value -> nBare stays 10 (write dropped)
        AddTo(ref HbDiscard<decimal>.Seed(nBare), 5);

        // In-out: the body READS the parameter before writing it, so the
        // bare call must still see the input value (10) even while its
        // write-back is discarded.
        // sees 10 in, returns via param, but stays 10
        Grow(nInOut);
        // 10 * 3 = 30 as the RETURN
        HbRuntime.QOut("grow-returned=", Grow(nInOut));

        // String out-param, same rules.
        // by ref   -> cRef becomes "new"
        SetTag(ref cRef, "new");
        // by value -> cBare stays "keep"
        SetTag(ref HbDiscard<string>.Seed(cBare), "new");

        // Literal into the ref slot — no input to keep, pure discard.
        {
            decimal _hbref_arg_0 = 100;
            AddTo(ref _hbref_arg_0, 5);
        }

        HbRuntime.QOut("nRef=", nRef);
        HbRuntime.QOut("nBare=", nBare);
        HbRuntime.QOut("nInOut=", nInOut);
        HbRuntime.QOut("cRef=", cRef);
        HbRuntime.QOut("cBare=", cBare);

        return;
    }

    public static void AddTo(ref decimal nTarget, decimal nAmount = default)
    {
        nTarget = nTarget + nAmount;
        return;

        // Reads nVal (proving the seeded input survives), writes it, and also
        // returns 3x so the caller can observe the computed value regardless.
    }
    public static decimal Grow(decimal nVal = default)
    {
        decimal nWas = nVal;
        nVal = nVal * 3;
        return nWas * 3;
    }

    public static void SetTag(ref string cInto, string cValue = default)
    {
        cInto = cValue;
        return;
    }
}
