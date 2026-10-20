// Test 52: `x`-prefix USUAL parameters are sticky in the reftab.
//
// When a parameter name starts with `x` or `X`, the user has chosen
// USUAL deliberately — typically because the body does VALTYPE()
// dispatch (the canonical Clipper/Harbour pattern for variant input).
// Without this guard, hb_refTabRefineParam would adopt the first
// concrete type observed at any call site (the "fresh USUAL slot →
// adopt new type" branch), narrowing `xFoo` to e.g. STRING. Other
// callers passing a different type then surface as CS1503 at C# emit.
//
// Pattern observed in easipos: msgresp.prg's MsgResponse(xAnswer1, ...)
// — body branches on VALTYPE(xAnswer1), callers pass either descm
// numerics like D_1Yes or strings from GetDescM(). Pre-fix, xAnswer1
// was typed STRING and 25+ call sites failed at the C# build.
//
// This test exercises a `xVal` parameter that the body dispatches
// on (string vs numeric) and call sites that pass each type. Both
// builds (.prg / .cs) must produce the same output.
public static partial class Program
{
    public static void Main(string[] args)
    {
        /* Mixed-type call sites — without the sticky guard, the first
      caller's type would narrow xVal and the second would CS1503. */
        QOut("string: " + Describe("hello"));
        QOut("number: " + Describe(42));
        QOut("logical:" + Describe(true));

        return;
    }

    public static dynamic Describe(dynamic xVal = default)
    {
        /* Body does VALTYPE() dispatch — the canonical USUAL pattern. */
        if (ValType(xVal) == "C")
        {
            return "string-" + xVal;
        }
        else if (ValType(xVal) == "N")
        {
            return "number-" + LTrim(Str(xVal));
        }
        else if (ValType(xVal) == "L")
        {
            return "bool-" + (xVal ? "T" : "F");
        }

        return "unknown";
    }
}
