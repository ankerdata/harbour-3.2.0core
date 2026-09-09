using System;
using System.Collections.Generic;
using static HbRuntime;
using static Program;

// Test 90: middle gaps in method sends and constructor calls.
//
// A function call with a gap — `Foo(a, , c)` — has emitted the later
// arguments in named form since test18. A method send did not: for
// `Class():New(a, , c)` and `::Super:New(a, , c)` the emitter could not
// resolve the callee's reftab row (the receiver is a constructor call,
// or Super), fell back to positional emission and silently dropped the
// gap, so `c` landed in the second slot. In the easipos corpus that was
// eight dialog constructors with a flag or a title one slot to the
// left. The send resolver now follows a `Class()` receiver and the
// INHERIT chain (a subclass without its own New constructs through its
// parent's), and a gap that nothing can name is passed as null —
// Harbour's NIL — rather than dropped.
// #include "common.ch"
// #include "hbclass.ch"
public class Base
{
    public string cLabel = "";
    public bool lFlag = false;

    public dynamic New(string cLabel = default, decimal nUnused = 0, bool lFlag = false)
    {
        this.cLabel = cLabel + HbRuntime.LTrim(HbRuntime.Str(nUnused));
        this.lFlag = lFlag;
        return this;
    }

    public dynamic Show()
    {
        HbRuntime.QOut(this.cLabel + "/" + (this.lFlag ? "T" : "F"));
        return null;

        /* ::Super:New with a gap: .T. must reach lFlag, not nUnused. */
    }
}

public class Derived : Base
{

    public dynamic New(string cLabel = default)
    {
        base.New(cLabel, lFlag: true);
        return this;

        /* No New of its own: Leaf():New( ... ) resolves to Base's row. */
    }
}

public class Leaf : Base
{

}

public static partial class Program
{
    public static void Main(string[] args)
    {
        dynamic o = default;

        o = (Base)new Base().New("base", lFlag: true);
        o.Show();
        o = (Derived)new Derived().New("derived");
        o.Show();
        o = (Leaf)new Leaf().New("leaf", lFlag: true);
        o.Show();
        o = (Base)new Base().New("all", 7);
        o.Show();
        return;
    }
}
