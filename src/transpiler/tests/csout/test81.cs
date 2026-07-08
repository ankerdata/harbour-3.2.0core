using System;
using static HbRuntime;
using static Program;

// Test 81: the call-site by-value marker.
//
// A block comment whose content is `@`, placed immediately before a
// call argument, marks a DELIBERATE by-value pass to a by-ref
// parameter. At transpile time it suppresses W0020 for that argument
// (verified manually — verify.sh checks runtime output, not warnings).
// At run time it changes nothing: the argument passes by value either
// way, so the callee's write-back is discarded. This test pins that
// runtime semantics, which must be identical across .prg, round-tripped
// .hb, and generated .cs.
//
// The marker is a special token, not an ordinary comment: genhb
// re-emits it inline (attached to its argument) on -GT round-trip,
// mirroring how the declaration-site marker is synthesized.
public static partial class Program
{
    public static void Main(string[] args)
    {
        decimal n = 5;

        // by-ref
        Fill(ref n);
        // 6 — write-back kept
        HbRuntime.QOut("afteref=" + HbRuntime.LTrim(HbRuntime.Str(n)));

        n = 5;
        // deliberate by-value
        Fill(ref HbDiscard<decimal>.Seed(n));
        // 5 — write-back discarded
        HbRuntime.QOut("afterbyval=" + HbRuntime.LTrim(HbRuntime.Str(n)));

        n = 5;
        // 6 — RETURN value
        HbRuntime.QOut("ret=" + HbRuntime.LTrim(HbRuntime.Str(Fill(ref HbDiscard<decimal>.Seed(n)))));
        // 5 — n untouched
        HbRuntime.QOut("kept=" + HbRuntime.LTrim(HbRuntime.Str(n)));
        return;
    }

    public static decimal Fill(ref decimal nOut)
    {
        nOut = nOut + 1;
        return nOut;
    }
}
