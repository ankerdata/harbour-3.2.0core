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
// is shimmed. The values must come out the same in Harbour and in C#.
public static partial class Program
{
    public static dynamic MinMax(List<dynamic> aData, ref dynamic xLo, ref dynamic xHi)
    {
        long i;
        xLo = aData[I(1)];
        xHi = aData[I(1)];
        for (i = 2; i <= Len(aData); i++)
        {
            if (aData[I(i)] < xLo)
            {
                xLo = aData[I(i)];
            }

            if (aData[I(i)] > xHi)
            {
                xHi = aData[I(i)];
            }
        }

        return null;
    }

    public static void Main(string[] args)
    {
        decimal nLo = 0;
        decimal nHi = 0;
        // two refs in one call
        {
            dynamic _hbref_nLo = nLo;
            dynamic _hbref_nHi = nHi;
            MinMax(new List<dynamic> { 3, 1, 4, 1, 5 }, ref _hbref_nLo, ref _hbref_nHi);
            nLo = _hbref_nLo;
            nHi = _hbref_nHi;
        }
        QOut("a lo=" + Str(nLo, 2) + " hi=" + Str(nHi, 2));
        // sequential call — reuses base 0
        {
            dynamic _hbref_nLo = nLo;
            dynamic _hbref_nHi = nHi;
            MinMax(new List<dynamic> { 9, 2, 6, 8, 7 }, ref _hbref_nLo, ref _hbref_nHi);
            nLo = _hbref_nLo;
            nHi = _hbref_nHi;
        }
        QOut("b lo=" + Str(nLo, 2) + " hi=" + Str(nHi, 2));
    }
}
