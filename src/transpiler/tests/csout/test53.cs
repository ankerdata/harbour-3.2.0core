using System;
using static HbRuntime;
using static Program;

// Test 53: value-typed Hungarian parameters (n/l/d/t prefix) are
// non-nullable in the C# emit, even when the body has a NIL guard.
//
// Per Alex's strict-typing rule: a Hungarian-typed value parameter
// declares the user's intent — it MUST be a number/logical/date/
// datetime, never NIL. Without this guard, the existing nilable
// inference (`if param == NIL` in the body, hb_default(@param,...))
// promoted these params to `decimal? = null` etc., and every typed
// call site downstream ate a CS1503 (`decimal? → decimal`).
//
// 48 `decimal? → decimal` errors and 26 `bool? → bool` errors in
// easipos collapsed to zero with this single hb_refTabSetNilable
// guard. The cascade also cleared ~90 indirect downstream errors
// (191 total).
//
// This test verifies that:
//   - A value-typed Hungarian param emits as the bare type
//     (decimal nFoo, bool lBar — not decimal?, bool?)
//   - Cross-function calls don't produce CS1503 between two
//     Hungarian-typed params
public static partial class Program
{
    public static void Main(string[] args)
    {
        HbRuntime.QOut("result1=" + HbRuntime.LTrim(HbRuntime.Str(AddPair(2, 3))));
        HbRuntime.QOut("result2=" + HbRuntime.LTrim(HbRuntime.Str(AddPair(10, 20))));
        HbRuntime.QOut("result3=" + (IsLargeFlag(150, true) ? "Y" : "N"));
        HbRuntime.QOut("result4=" + (IsLargeFlag(50, false) ? "Y" : "N"));
        return;

        /* `nA`/`nB`: numeric per Hungarian. Even if some caller passed a
   NIL, the strict-typing rule treats the source as the bug —
   the param signature stays `decimal nA, decimal nB`. */
    }
    public static decimal AddPair(decimal nA = default, decimal nB = default)
    {
        return nA + nB;

        /* `nValue`: numeric, `lFlag`: logical. The body has a guard pattern
   that previously triggered nilable inference. With the strict
   rule, the params remain non-nullable; calls from this file
   that pass these params on to other typed callees don't CS1503. */
    }
    public static bool IsLargeFlag(decimal nValue = default, bool lFlag = default)
    {
        /* Stale Clipper habit (`if param == NIL`) — the strict rule
      makes this branch unreachable for typed Hungarian params,
      but it doesn't change the param signature. */
        if (!lFlag)
        {
            return false;
        }

        return nValue > 100;
    }
}
