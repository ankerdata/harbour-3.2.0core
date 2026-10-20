// Test 18: default parameters and middle-gap call sites.
//
// `nA` is non-nullable (numeric Hungarian, strict-typing rule). `xB`
// and `xC` are USUAL (`x` prefix) so they can legitimately be NIL
// when the caller omits them — the canonical Clipper pattern for
// truly-optional params. Renaming from the prior `nB`, `nC` matches
// the strict-typing rule that says Hungarian-typed value params are
// never NIL: a NIL guard on `nB` would now be unreachable because
// the C# emit defaults `nB` to 0.
public static partial class Program
{
    public static void Main(string[] args)
    {
        Fred(1);
        Fred(10, 20);
        Fred(100, 200, 300);
        // middle gap → named args in C#
        Fred(1000, xC: 3000);
    }

    public static void Fred(decimal nA = default, dynamic? xB = null, dynamic? xC = null)
    {
        QOut("a=" + Str(nA));
        if (xB != null)
        {
            QOut("b=" + Str(xB));
        }

        if (xC != null)
        {
            QOut("c=" + Str(xC));
        }
    }
}
