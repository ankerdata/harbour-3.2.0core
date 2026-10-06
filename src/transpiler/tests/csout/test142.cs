using System;
using System.Collections.Generic;
using static HbRuntime;
using static Program;

// Test 142: a member declared a dynamic class reaches its bag.
//
// A dynamic class (one that sends ::&( name ) to itself, EasiPOS's
// SQLtTable) is its own C# type, and a member no class declares goes
// through `((dynamic)recv)` to the instance's bag (test134). That held for
// a local and for a member named for its class, but not for a member whose
// name says nothing and whose declaration gives the class (`VAR oOrmTable
// AS CLASS SQLtTable`, EasiPOS's BrowseDialog): the send was written
// plainly on the declared type, CS1061. Peek142() reads such a field (it
// is never called: the bag only holds what Put142() put there, and Harbour
// has no such member otherwise); Count142() reads a declared one, checked.
// And a constructor whose New() answers its own class is written without
// a cast, while an inherited New() keeps one. And a method sent to a
// receiver C# cannot type (an element of an undeclared array) with an
// argument past its parameters runs, the extra dropped as Harbour drops
// it: the DLR cannot bind such a call, and HbRuntime's fallback used to
// report "Parameter count mismatch" (HbDynamicObject.TryInvokeMember).

// #include "../include/astype.ch"
// #include "hbclass.ch"
public class Bag142 : HbDynamicObject
{
    public string cLabel142 = "";
    public decimal nCount142 = 0;

    public virtual Bag142 Put142(string cName = default, dynamic xValue = default)
    {
        HbRuntime.SETMEMBER(this, cName, xValue);
        nCount142 += 1;
        return this;
    }

    public virtual string Repeat142(string cWhat = default, decimal nTimes = 1)
    {
        return HbRuntime.Replicate(cWhat, nTimes);
    }
}

public class Holder142 : IHbObject
{
    public Bag142 oHeld;

    public virtual Holder142 New(dynamic oHeld = default)
    {
        this.oHeld = oHeld;
        return this;
    }

    public virtual decimal Count142()
    {
        return oHeld.nCount142;
    }

    public virtual string Peek142()
    {
        return ((dynamic)oHeld).cUndeclared142;
    }
}

public class Keeper142 : Holder142
{

}

public static partial class Program
{
    public static void Main(string[] args)
    {
        Holder142 oHolder = new Holder142().New(new Bag142().Put142("cLabel142", "box"));
        Keeper142 oKeeper = (Keeper142)new Keeper142().New(new Bag142());
        List<dynamic> aLoose = new List<dynamic> { new Bag142() };

        HbRuntime.QOut(oHolder.Count142(), oKeeper.Count142(), oHolder.oHeld.cLabel142);
        HbRuntime.QOut(aLoose[0].Repeat142("ab", 2, "extra"), aLoose[0].Repeat142("c"));
        return;
    }
}
