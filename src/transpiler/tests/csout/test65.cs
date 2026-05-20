using System;
using static HbRuntime;
using static Program;

// Test 65: "discard-the-outputs" overload for by-ref OUTPUT params.
//
// ErrText65 has a by-ref output param (lLog) sitting before a by-value
// param (lUpper). Harbour callers routinely ignore the output: they
// omit it, or skip it while still passing a later by-value arg
// (ErrText65(7, , .t.)). C# can't omit a `ref` param, so the emitter
// adds a "by-value" overload that exposes only the by-value params and
// supplies dummy `ref` storage; the skip-the-ref call reaches it via a
// named argument (ErrText65(7, lUpper: true)).
//
// This also exercises the prefix overload (nCode only) and the
// canonical signature (passing @lLog), so all three forms must round
// trip identically through -GT (Harbour) and -GS (C#).
public static partial class Program
{
    public static dynamic ErrText65(decimal nCode, ref bool lLog, bool lUpper = default)
    {
        // written back -> marks lLog by-ref
        lLog = (nCode > 0);
        return (lUpper == true ? "CODE" : "code") + HbRuntime.Str(nCode, 2);
    }

    public static dynamic ErrText65(decimal nCode = default)
    {
        bool _arg1 = default;
        bool _arg2 = default;
        return ErrText65(nCode, ref _arg1, _arg2);
    }

    public static void Main(string[] args)
    {
        bool lLog = false;
        // omit lLog + lUpper -> short overload
        HbRuntime.QOut(ErrText65(5, ref HbDiscard<bool>.Value));
        // skip lLog, pass lUpper -> by-value overload
        HbRuntime.QOut(ErrText65(7, ref HbDiscard<bool>.Value, true));
        // pass @lLog -> canonical (writes lLog back)
        HbRuntime.QOut(ErrText65(3, ref lLog));
        HbRuntime.QOut("lLog=" + (lLog ? "Y" : "N"));
        return;
    }
}
