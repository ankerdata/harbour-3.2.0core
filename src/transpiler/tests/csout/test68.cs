using System;
using static HbRuntime;
using static Program;

// Test 68: ref-shim temp naming.
//
// The by-ref shim names each temp after the lvalue it backs — a plain
// @var becomes `_hbref_<var>`. Two things this guards:
//   1. Two by-ref args in ONE call get distinct, readable temps from
//      their variable names (here _hbref_nLo and _hbref_nHi).
//   2. Sequential calls — each wrapped in its own `{ }` scope — reuse the
//      same names rather than appending an ever-climbing counter; the
//      shim only adds a depth prefix (_hbref1_<var>) for a *nested* block.
//
// MinMax has two USUAL by-ref outputs (x-prefixed); the caller passes
// numeric locals, so each @arg mismatches the `ref dynamic` parameter and
// is shimmed. The values must round-trip identically through -GT and -GS.
public static partial class Program
{
    public static dynamic MinMax(dynamic[] aData, ref dynamic xLo, ref dynamic xHi)
    {
        long i = default;
        xLo = aData[0];
        xHi = aData[0];
        for (i = 2; i <= HbRuntime.Len(aData); i++)
        {
            if (aData[i - 1] < xLo)
            {
                xLo = aData[i - 1];
            }

            if (aData[i - 1] > xHi)
            {
                xHi = aData[i - 1];
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
            dynamic _hbref_nLo = nLo;
            dynamic _hbref_nHi = nHi;
            MinMax(new dynamic[] { 3, 1, 4, 1, 5 }, ref _hbref_nLo, ref _hbref_nHi);
            nLo = _hbref_nLo;
            nHi = _hbref_nHi;
        }
        HbRuntime.QOut("a lo=" + HbRuntime.Str(nLo, 2) + " hi=" + HbRuntime.Str(nHi, 2));
        // sequential call — reuses base 0
        {
            dynamic _hbref_nLo = nLo;
            dynamic _hbref_nHi = nHi;
            MinMax(new dynamic[] { 9, 2, 6, 8, 7 }, ref _hbref_nLo, ref _hbref_nHi);
            nLo = _hbref_nLo;
            nHi = _hbref_nHi;
        }
        HbRuntime.QOut("b lo=" + HbRuntime.Str(nLo, 2) + " hi=" + HbRuntime.Str(nHi, 2));
        return;
    }
}
