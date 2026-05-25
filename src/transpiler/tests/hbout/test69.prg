#include "astype.ch"
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

FUNCTION Probe69( /*@*/xVal AS USUAL ) AS LOGICAL
   // USUAL by-ref output
   xVal := xVal + 1
RETURN xVal > 0

PROCEDURE Main()
   // numeric -> mismatches `ref dynamic`, shimmed
   LOCAL nN := 1 AS NUMERIC
   // hoisted: outer block shims @nN
   IF Probe69(@nN)
      // nested statement: inner block shims @nN
      Probe69(@nN)
   ENDIF

   QOut("nN=" + Str(nN, 2))
RETURN
