using System;
using static HbRuntime;

// Test 32: PP command-rule matching with trailing `//` comments.
//
// With comment preservation enabled, a #command line like
//     SET CENTURY ON      // not a set - __SetCentury
// used to hang the preprocessor for tens of seconds per line. The
// rule `#command SET CENTURY <x:ON,OFF,&> => __SetCentury( <(x)> )`
// has a STRSMART (<(x)>) result marker that entered
// hb_pp_matchResultLstAdd's for(;;) walk with a match range that
// included the trailing COMMENT token, and the walk failed to reach
// its stop sentinel.
//
// Fix: in hb_pp_processLine, detach trailing `//` comment tokens from
// the token chain before running #define / #translate / #command rule
// matching, then splice them back onto the expanded result so the
// lexer's comment-preservation hook (complex.c HB_PP_TOKEN_COMMENT
// branch) still captures them as AST nodes for .hb / .cs round-trip.
//
// This test exercises two patterns that previously hung:
//   - A plain #command match (SET CENTURY ON // ...)
//   - DEFAULT var TO value // ... — std.ch's DEFAULT command is the
//     pattern that hit buffprt/ttlcalc E0020 collateral errors.
//
// Hit in easiposx: startup.prg (40+s timeout), buffprt.prg,
// ttlcalc.prg — 3 files cleared.

// #include "set.ch"
// #include "common.ch"
// doesn't auto-load it (our transpiler does),
// so without this hbmk2 fails to compile the
// `DEFAULT var TO val` lines below.
public static partial class Program
{
    public static void Main(string[] args)
    {
        // #command match + trailing line comment.
        // not a set - __SetCentury
        HbRuntime.__SETCENTURY("ON");
        // needed for ASORT
        HbRuntime.SET(_SET_EXACT, "ON");

        Probe(1, true);
        Probe(2);
        Probe();
        return;
    }

    public static void Probe(decimal? nKind = null, bool? lReady = null)
    {
        // DEFAULT ... TO ... // comment — std.ch translate rule.
        // default kind
        if (nKind == null)
        {
            nKind = 0;
        }
        // not ready by default
        if (lReady == null)
        {
            lReady = false;
        }
        if (lReady == true)
        {
            HbRuntime.QOUT("kind=" + HbRuntime.LTRIM(HbRuntime.STR(nKind)) + " ready=Y");
        }
        else
        {
            HbRuntime.QOUT("kind=" + HbRuntime.LTRIM(HbRuntime.STR(nKind)) + " ready=N");
        }

        return;
    }
}
