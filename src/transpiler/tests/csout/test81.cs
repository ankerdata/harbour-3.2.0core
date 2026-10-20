// Test 81: the call-site by-value marker.
//
// A block comment whose content is `@`, placed immediately before a
// call argument, marks a DELIBERATE by-value pass to a by-ref
// parameter. At transpile time it suppresses W0020 for that argument
// (verified manually — the suite compares runtime output, not warnings).
// At run time it changes nothing: the argument passes by value either
// way, so the callee's write-back is discarded. This test pins that
// runtime semantics, which must be identical in the .prg and the
// generated .cs.
//
// The marker is a special token, not an ordinary comment: the lexer
// attaches it to its argument (HB_EXPRFLAG_BYREFSKIP).
public static partial class Program
{
    public static void Main(string[] args)
    {
        decimal n = 5;

        // by-ref
        Fill(ref n);
        // 6 — write-back kept
        QOut("afteref=" + LTrim(Str(n)));

        n = 5;
        // deliberate by-value
        Fill(ref HbDiscard<decimal>.Seed(n));
        // 5 — write-back discarded
        QOut("afterbyval=" + LTrim(Str(n)));

        n = 5;
        // 6 — RETURN value
        QOut("ret=" + LTrim(Str(Fill(ref HbDiscard<decimal>.Seed(n)))));
        // 5 — n untouched
        QOut("kept=" + LTrim(Str(n)));
    }

    public static decimal Fill(ref decimal nOut)
    {
        nOut = nOut + 1;
        return nOut;
    }
}
