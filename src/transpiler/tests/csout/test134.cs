using System;
using System.Collections.Generic;
using static HbRuntime;
using static Program;

// Test 134: a dynamic class is its own C# type.
//
// A class that sends ::&( name ) to itself extends HbDynamicObject in C#
// (EasiPOS's SQLtTable), so an instance may hold members no declaration
// names. Every slot, local and return of such a class used to be emitted
// `dynamic`, which left each use unchecked so that the few undeclared ones
// would compile. It is its own type now: declared members and methods are
// checked when C# builds, and only a member no class declares goes through
// `((dynamic)recv)`, as Extra134() shows (it is never called: Harbour has
// no such member at run time, where SQLtTable's come from its metaclass).
//
// And only a macro send to Self makes a class dynamic. Viewer134 sends one
// to its member, which says nothing of Viewer134 itself (EasiPOS's
// BrowseDialog, `::oOrmTable:&( cField )`).

// #include "hbclass.ch"
public class Bag134 : HbDynamicObject
{
    public string cLabel134 = "";
    public decimal nCount134 = 0;

    public virtual Bag134 Put134(string cName = default, dynamic xValue = default)
    {
        HbRuntime.SETMEMBER(this, cName, xValue);
        return this;
    }

    public virtual dynamic Get134(string cName = default)
    {
        return HbRuntime.GETMEMBER(this, cName);
    }

    public virtual Bag134 Touch134()
    {
        nCount134 += 1;
        return this;
    }
}

public class Viewer134
{
    public Bag134 oBag134;

    public virtual Viewer134 New(Bag134 oBag134 = default)
    {
        this.oBag134 = oBag134;
        return this;
    }

    public virtual dynamic Show134(string cName = default)
    {
        return HbRuntime.GETMEMBER(oBag134, cName);

        // declared members and methods on a Bag134 receiver: checked in C#
    }
}

public static partial class Program
{
    public static string Label134(Bag134 oBag134 = default)
    {
        return oBag134.Touch134().cLabel134 + "/" + HbRuntime.hb_ntos(oBag134.nCount134);

        // a member no class declares, which only a dynamic class's bag could hold
    }
    public static string Extra134(Bag134 oBag134 = default)
    {
        return ((dynamic)oBag134).cExtra134;
    }

    public static void Main(string[] args)
    {
        Bag134 oStore134 = new Bag134();
        Viewer134 oShow134 = default;

        oStore134.Put134("cLabel134", "tin");
        oShow134 = (Viewer134)new Viewer134().New(oStore134);
        HbRuntime.QOut(oShow134.Show134("cLabel134"));
        HbRuntime.QOut(Label134(oStore134));
        HbRuntime.QOut(oStore134.Get134("nCount134"));
        return;
    }
}
