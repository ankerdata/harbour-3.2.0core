using System;
using System.Collections.Generic;
using static HbRuntime;
using static Program;

// Test 136: declared types, `AS CLASS X` and `AS ARRAY OF …` (plan C11).
//
// A name types a variable as far as a prefix can: `o<Class>` names a
// class, `a` an array. Where the name cannot say more, the declaration
// does, in every position: a local, a parameter, a file or routine
// static, a class member (`VAR`), a function's or a method's return.
// A declared variable is what its declaration says, and nothing in the
// body moves it. An array declared `AS ARRAY OF CLASS X` keeps its
// List<dynamic> storage, but a read of an element is an X: a send on one
// is cast, `((Gizmo136)aGizmos[i]).nSize`, and checked when C# builds,
// and a FOR EACH variable over it is an X. The element type lives only
// where the source declares it; into anything undeclared an
// ARRAY OF X is a plain array again.
//
// Harbour accepts the declarations in fewer places (none on a member,
// a method or a function's return) and wants a forward declaration of a
// class from another file; include/astype.ch removes them for it, as
// EasiPOS's include/astype.ch does, so the generated C is the same.

// #include "../include/astype.ch"
// #include "hbclass.ch"
public class Gizmo136
{
    public string cName = "";
    public decimal nSize = 0;

    public virtual Gizmo136 New(string cName = default, decimal nSize = default)
    {
        this.cName = cName;
        this.nSize = nSize;
        return this;
    }

    public virtual string Label()
    {
        return cName + "(" + HbRuntime.hb_ntos(nSize) + ")";

        // A member only a subclass declares, read through an element declared
        // Gizmo136, is read as it stands: the element is `dynamic` in C# (a
        // List<dynamic> slot), so it needs no cast (test138).
    }
}

public class Bigizmo136 : Gizmo136
{
    public string cColour = "red";

}

public class Crate136
{
    public List<dynamic> aGizmos = new List<dynamic>();
    public Gizmo136 oFirst;

    public virtual Crate136 Add(Gizmo136 oGizmo = default)
    {
        HbRuntime.AAdd(aGizmos, oGizmo);
        if (oFirst == null)
        {
            oFirst = oGizmo;
        }

        return this;
    }

    public virtual Gizmo136 Largest()
    {
        return Biggest136(aGizmos);
    }
}

public static partial class Program
{
    public static Gizmo136 test136_soSpare136;
    public static List<dynamic> test136_saShelf136;
    public static decimal test136_Total136_snCalls;
    public static void Main(string[] args)
    {
        Crate136 oCrate = new Crate136();
        Gizmo136 oAny = default;
        List<dynamic> aNames = new List<dynamic> {  };
        Gizmo136 oItem = default;
        long iPos = default;

        oCrate.Add((Gizmo136)new Gizmo136().New("bolt", 3));
        oCrate.Add((Gizmo136)new Gizmo136().New("beam", 9));
        oCrate.Add((Gizmo136)new Gizmo136().New("nut", 1));
        oCrate.Add((Bigizmo136)new Bigizmo136().New("plate", 5));

        foreach (Gizmo136 __hb_fe_oItem in HbRuntime.HbEnumValues(oCrate.aGizmos))
        {
            oItem = __hb_fe_oItem;
            HbRuntime.AAdd(aNames, oItem.Label());
        }

        HbRuntime.QOut(aNames[1], HbRuntime.Len(aNames[1]));

        for (iPos = 1; iPos <= HbRuntime.Len(oCrate.aGizmos); iPos++)
        {
            HbRuntime.QOut(((Gizmo136)oCrate.aGizmos[(int)iPos - 1]).cName, ((Gizmo136)oCrate.aGizmos[(int)iPos - 1]).nSize);
        }

        HbRuntime.QOut(oCrate.aGizmos[3].cColour, ((Gizmo136)oCrate.aGizmos[3]).nSize);

        oAny = oCrate.Largest();
        HbRuntime.QOut(oAny.Label());
        HbRuntime.QOut(oCrate.oFirst.Label());

        test136_saShelf136 = oCrate.aGizmos;
        test136_soSpare136 = test136_saShelf136[2];
        HbRuntime.QOut(test136_soSpare136.Label());
        HbRuntime.QOut(HbRuntime.Len(test136_Names136(test136_saShelf136)), test136_Names136(test136_saShelf136)[0]);
        HbRuntime.QOut(test136_Total136());
        return;
    }

    public static Gizmo136 Biggest136(List<dynamic> aList = default)
    {
        Gizmo136 oBest = default;
        Gizmo136 oOne = default;
        foreach (Gizmo136 __hb_fe_oOne in HbRuntime.HbEnumValues(aList))
        {
            oOne = __hb_fe_oOne;
            if (oBest == null || oOne.nSize > oBest.nSize)
            {
                oBest = oOne;
            }
        }

        return oBest;
    }

    public static List<dynamic> test136_Names136(List<dynamic> aList = default)
    {
        List<dynamic> aOut = new List<dynamic> {  };
        long iPos = default;
        for (iPos = 1; iPos <= HbRuntime.Len(aList); iPos++)
        {
            HbRuntime.AAdd(aOut, ((Gizmo136)aList[(int)iPos - 1]).cName);
        }

        return aOut;
    }

    public static decimal test136_Total136()
    {
        decimal nTotal = 0;
        Gizmo136 oG = default;
        foreach (Gizmo136 __hb_fe_oG in HbRuntime.HbEnumValues(test136_saShelf136))
        {
            oG = __hb_fe_oG;
            nTotal += oG.nSize;
        }

        test136_Total136_snCalls = 1;
        return nTotal;
    }
}
