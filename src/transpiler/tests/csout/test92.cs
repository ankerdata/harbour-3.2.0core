using System;
using System.Collections.Generic;
using static HbRuntime;
using static Program;

// Test 92: `::Super:Method(...)` with dynamic arguments.
//
// C# binds a `base.Method(...)` call statically and cannot when any
// argument is `dynamic` (CS1971: "needs to be dynamically dispatched,
// but cannot be because it is part of a base access expression"). A
// parameter typed OBJECT — an o-name with no class of that name, or a
// method slot no typed caller ever refined — is `dynamic` in C#, and
// every Dialog subclass in the easipos corpus forwards two of them
// through ::Super:New (17 CS1971). The emitter now casts a dynamic-
// typed variable argument in a base call: `(object)` for a dynamic
// slot (statically bound, converts to dynamic for free), the slot's
// own type for a typed one. Concrete-typed arguments are left alone so
// a real mismatch still reads as CS1503.
// #include "hbclass.ch"
public class Gadget
{
    public string cName = "gadget";

}

public class Crate
{
    public dynamic oItem;
    public decimal nCount = 0;

    public dynamic New(dynamic oItem = default, decimal nCount = default)
    {
        this.oItem = oItem;
        this.nCount = nCount;
        return this;

        /* oItem is OBJECT (no class named Item) and xCount is USUAL: both
   dynamic, into a dynamic slot and a decimal slot respectively. Main
   passes an `x` local (USUAL by prefix, and that is sticky across the
   later assignment) so the call site cannot sharpen oItem — a
   `Tandem()` receiver is typed since test93 — and the slot stays
   dynamic, which is the shape this test is about. */
    }
}

public class Tandem : Crate
{

    public dynamic New(dynamic oItem = default, dynamic xCount = default)
    {
        base.New((object)oItem, (decimal)xCount);
        return this;
    }
}

public static partial class Program
{
    public static void Main(string[] args)
    {
        dynamic xItem = default;
        Tandem oTandem = default;
        xItem = new Gadget();
        oTandem = (Tandem)new Tandem().New(xItem, 2);
        HbRuntime.QOut("count=" + HbRuntime.LTrim(HbRuntime.Str(oTandem.nCount)) + " item=" + oTandem.oItem.cName);
        return;
    }
}
