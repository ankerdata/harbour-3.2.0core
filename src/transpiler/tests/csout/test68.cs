using System;
using static HbRuntime;
using static Program;

// Test 68: ref-shim temp naming.
//
// Two things this guards about the `_hbref<base>_<pos>` temps the by-ref
// shim emits:
//   1. Two by-ref args in ONE call get distinct temps via the <pos>
//      suffix while sharing one <base> (here _hbref0_1 and _hbref0_2).
//   2. Sequential calls — each wrapped in its own `{ }` scope — REUSE the
//      base rather than incrementing it: the base tracks shim-block
//      nesting depth, not a monotonic counter, so both calls below emit
//      _hbref0_… (a nested shim would be _hbref1_…).
//
// MinMax has two USUAL by-ref outputs (x-prefixed); the caller passes
// numeric locals, so each @arg mismatches the `ref dynamic` parameter and
// is shimmed. The values must round-trip identically through -GT and -GS.
public static partial class Program
{
    public static dynamic MinMax(dynamic[] aData, ref dynamic xLo, ref dynamic xHi)
    {
        decimal i = default;
        xLo = aData[0];
        xHi = aData[0];
        for (i = 2; i <= HbRuntime.Len(aData); i++)
        {
            if (aData[(int)(i) - 1] < xLo)
            {
                xLo = aData[(int)(i) - 1];
            }

            if (aData[(int)(i) - 1] > xHi)
            {
                xHi = aData[(int)(i) - 1];
            }
        }

        return null;
    }

    public static void Main(string[] args)
    {
        decimal nLo = default;
        decimal nHi = default;
        // two refs in one call
        {
            dynamic _hbref0_1 = nLo;
            dynamic _hbref0_2 = nHi;
            MinMax(new dynamic[] { 3, 1, 4, 1, 5 }, ref _hbref0_1, ref _hbref0_2);
            nLo = _hbref0_1;
            nHi = _hbref0_2;
        }
        HbRuntime.QOut("a lo=" + HbRuntime.Str(nLo, 2) + " hi=" + HbRuntime.Str(nHi, 2));
        // sequential call — reuses base 0
        {
            dynamic _hbref0_1 = nLo;
            dynamic _hbref0_2 = nHi;
            MinMax(new dynamic[] { 9, 2, 6, 8, 7 }, ref _hbref0_1, ref _hbref0_2);
            nLo = _hbref0_1;
            nHi = _hbref0_2;
        }
        HbRuntime.QOut("b lo=" + HbRuntime.Str(nLo, 2) + " hi=" + HbRuntime.Str(nHi, 2));
        return;
    }
}
