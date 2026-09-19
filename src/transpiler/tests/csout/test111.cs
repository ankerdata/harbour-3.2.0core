using System;
using static HbRuntime;
using static Program;

// Test 111: a FOR loop counts in the direction of its step's sign.
//
// Harbour's code generator takes the direction from a constant step
// (hb_compExprAsNumSign) and tests any other step's sign at run time on
// every pass (HB_P_FORTEST). The emitter knew only a negative integer
// literal: `STEP nStep` with a negative nStep emitted `nI <= nEnd` and
// looped forever (hbtest's TFORNEXTX hung the RTL harness), and
// `STEP -0.5` never ran. Now a constant step picks `<=` or `>=` as
// before, and any other step is `HbRuntime.ForTest( nI, nEnd, nStep )`,
// the end evaluated before the step as Harbour evaluates them.
public static partial class Program
{
    public static void Main(string[] args)
    {
        decimal nStep = -1;
        decimal nI = default;
        string cTrail = "";

        for (nI = 5; HbRuntime.ForTest(nI, 1, nStep); nI += nStep)
        {
            cTrail += HbRuntime.Str(nI, 1);
        }

        HbRuntime.QOut(cTrail, nI);

        cTrail = "";
        for (nI = 1; HbRuntime.ForTest(nI, 5, nStep); nI += nStep)
        {
            cTrail += HbRuntime.Str(nI, 1);
        }

        HbRuntime.QOut("[" + cTrail + "]", nI);

        cTrail = "";
        for (nI = 1; nI <= 2; nI += 0.5m)
        {
            cTrail += HbRuntime.Str(nI, 3, 1) + " ";
        }

        HbRuntime.QOut(cTrail);

        cTrail = "";
        for (nI = 2; nI >= 1; nI += -0.5m)
        {
            cTrail += HbRuntime.Str(nI, 3, 1) + " ";
        }

        HbRuntime.QOut(cTrail);

        // the step is read again on every pass
        cTrail = "";
        nStep = 1;
        for (nI = 1; HbRuntime.ForTest(nI, 10, nStep); nI += nStep)
        {
            cTrail += HbRuntime.Str(nI, 2) + " ";
            nStep = nStep * 2;
        }

        HbRuntime.QOut(cTrail);

        return;
    }
}
