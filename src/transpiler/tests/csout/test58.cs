// Test 58: `Self:` access to a CLASS VAR (static class-level member).
//
// Harbour's `CLASS VAR` is one value shared across every instance —
// it emits as a C# `static` field. Code reaches it with instance
// syntax (`::nTotal`), but C# rejects `this.staticField` (CS0176):
// a static member must be qualified by the type name.
//
// The transpiler now emits `Counter.nTotal`. Two code paths needed
// the fix and this test pins both:
//   1. a regular method body, via the AST SEND emitter
//   2. an INLINE method body, via the textual translator
//
// nLocal (a plain instance VAR) must stay `this.nLocal`.
//
// Doubled() also guards a regression: the INLINE translator rejects
// workarea-ALIAS bodies by scanning for `->`, but that scan must run
// AFTER the trailing-comment strip — a `->` in an INLINE line's `//`
// comment must not stub the method.

// Bump exercises the AST SEND path: a CLASS VAR and an instance VAR
// both written via `::`.
public class Counter : IHbObject
{
    public static decimal nTotal = 0;
    public decimal nLocal = 0;

    public dynamic Total() => Counter.nTotal;
    public dynamic Doubled() => Counter.nTotal * 2;
    public virtual Counter Bump()
    {
        // CLASS VAR: becomes Counter.nTotal
        Counter.nTotal = Counter.nTotal + 1;
        // instance:  stays this.nLocal
        nLocal = nLocal + 1;
        return this;
    }
}

public static partial class Program
{
    public static void Main(string[] args)
    {
        var oA = new Counter();
        var oB = new Counter();

        oA.Bump();
        oA.Bump();
        // nTotal is shared: now 3
        oB.Bump();

        // INLINE read of CLASS VAR
        QOut("a_total=" + LTrim(Str(oA.Total())));
        QOut("b_total=" + LTrim(Str(oB.Total())));
        QOut("a_local=" + LTrim(Str(oA.nLocal)));
        QOut("b_local=" + LTrim(Str(oB.nLocal)));
        // INLINE body, arrow in comment
        QOut("doubled=" + LTrim(Str(oA.Doubled())));
    }
}
