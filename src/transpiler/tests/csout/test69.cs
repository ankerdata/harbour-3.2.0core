using System;
using static HbRuntime;
using static Program;

// Test 69: nested ref-shim blocks need the depth prefix.
//
// A ref-passing call in an IF condition is hoisted into a brace block;
// if a statement *inside* that IF body shims the SAME variable, its temp
// lives in a scope nested within the hoist block, so it can't reuse the
// outer name (C# CS0136 forbids shadowing a local in an enclosing block).
// The shim disambiguates by prefixing the nesting depth:
//
//     {
//         dynamic _hbref_nN = nN;                 // outer (depth 0)
//         var _hbcall_Probe69 = Probe69(ref _hbref_nN);
//         nN = _hbref_nN;
//         if (_hbcall_Probe69)
//         {
//             dynamic _hbref1_nN = nN;            // inner (depth 1)
//             Probe69(ref _hbref1_nN);
//             nN = _hbref1_nN;
//         }
//     }
//
// Both Probe69 calls increment nN by 1, so the result must round-trip
// identically through -GT and -GS — proving the two temps don't collide.
public static partial class Program
{
    public static bool Probe69(ref dynamic xVal)
    {
        // USUAL by-ref output
        xVal = xVal + 1;
        return xVal > 0;
    }

    public static void Main(string[] args)
    {
        // numeric -> mismatches `ref dynamic`, shimmed
        decimal nN = 1;
        // hoisted: outer block shims @nN
        {
            dynamic _hbref_nN = nN;
            var _hbcall_Probe69 = Probe69(ref _hbref_nN);
            nN = _hbref_nN;
            if (_hbcall_Probe69)
            {
                // nested statement: inner block shims @nN
                {
                    dynamic _hbref1_nN = nN;
                    Probe69(ref _hbref1_nN);
                    nN = _hbref1_nN;
                }
            }
        }

        HbRuntime.QOut("nN=" + HbRuntime.Str(nN, 2));
        return;
    }
}
