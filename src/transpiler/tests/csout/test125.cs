using System;
using static HbRuntime;
using static Program;

// Test 125: a Harbour array is a List<dynamic>, one object every holder
// shares.
//
// A C# array cannot grow: AAdd() and ASize() took theirs by `ref` and
// handed back a new one, so only the variable passed saw the change. An
// array held by a second variable, a member, or an element of another
// array kept its old length, and `AAdd( aX[ i ], y )` grew a copy nobody
// held (easipos's ORM Restructure read past an empty list for it). A
// List<dynamic> grows in place, as Harbour's array does, and needs no `ref`
// anywhere: not at the call, and not on a parameter only passed to AAdd().

// #include "hbclass.ch"
public class Stock125
{
    public List<dynamic> aItems125;

    public virtual Stock125 Fill125()
    {
        aItems125 = new List<dynamic> {  };
        return this;

        // an array parameter the routine only grows: a plain List<dynamic>
    }
}

public static partial class Program
{
    public static dynamic test125_AddTwice125(List<dynamic> aTarget = default)
    {
        HbRuntime.AAdd(aTarget, "x");
        HbRuntime.AAdd(aTarget, "y");
        return null;
    }

    public static void Main(string[] args)
    {
        List<dynamic> aRows125 = new List<dynamic> { new List<dynamic> {  }, new List<dynamic> {  } };
        List<dynamic> aAlias125 = default;
        List<dynamic> aGrid125 = HbRuntime.Array(2, 3);
        List<dynamic> aSeen125 = new List<dynamic> { 1, 2 };
        Stock125 oStock125 = new Stock125().Fill125();
        dynamic x = default;

        // an element grows in place
        HbRuntime.AAdd(aRows125[0], "a");
        HbRuntime.AAdd(aRows125[0], "b");
        HbRuntime.QOut(HbRuntime.Len(aRows125[0]), aRows125[0][1]);
        HbRuntime.hb_AIns(aRows125[0], 1, "z", true);
        HbRuntime.QOut(HbRuntime.Len(aRows125[0]), aRows125[0][0]);

        // a second holder sees the change, whoever makes it
        aAlias125 = aRows125[1];
        HbRuntime.ASize(aRows125[1], 3);
        HbRuntime.QOut(HbRuntime.Len(aAlias125));
        test125_AddTwice125(aAlias125);
        HbRuntime.QOut(HbRuntime.Len(aRows125[1]), aRows125[1][4]);
        HbRuntime.hb_ADel(aAlias125, 1, true);
        HbRuntime.QOut(HbRuntime.Len(aRows125[1]));

        // a member
        HbRuntime.AAdd(oStock125.aItems125, "first");
        HbRuntime.QOut(HbRuntime.Len(oStock125.aItems125), oStock125.aItems125[0]);

        // every dimension of Array()
        HbRuntime.QOut(HbRuntime.Len(aGrid125), HbRuntime.Len(aGrid125[1]), HbRuntime.ValType(aGrid125[1][2]));

        // FOR EACH sees an element its body adds
        foreach (dynamic __hb_fe_x in HbRuntime.HbEnumValues(aSeen125))
        {
            x = __hb_fe_x;
            if (x < 4)
            {
                HbRuntime.AAdd(aSeen125, x + 2);
            }
        }

        HbRuntime.QOut(HbRuntime.Len(aSeen125), aSeen125[4]);

        return;
    }
}
