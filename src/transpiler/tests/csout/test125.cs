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
public class Stock125 : IHbObject
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
        AAdd(aTarget, "x");
        AAdd(aTarget, "y");
        return null;
    }

    public static void Main(string[] args)
    {
        List<dynamic> aRows125 = new List<dynamic>
        {
            new List<dynamic> {  },
            new List<dynamic> {  }
        };
        List<dynamic> aAlias125 = default;
        List<dynamic> aGrid125 = Array(2, 3);
        List<dynamic> aSeen125 = new List<dynamic> { 1, 2 };
        Stock125 oStock125 = new Stock125().Fill125();
        dynamic x = default;

        // an element grows in place
        AAdd(aRows125[Ix(1)], "a");
        AAdd(aRows125[Ix(1)], "b");
        QOut(Len(aRows125[Ix(1)]), aRows125[Ix(1)][Ix(2)]);
        hb_AIns(aRows125[Ix(1)], 1, "z", true);
        QOut(Len(aRows125[Ix(1)]), aRows125[Ix(1)][Ix(1)]);

        // a second holder sees the change, whoever makes it
        aAlias125 = aRows125[Ix(2)];
        ASize(aRows125[Ix(2)], 3);
        QOut(Len(aAlias125));
        test125_AddTwice125(aAlias125);
        QOut(Len(aRows125[Ix(2)]), aRows125[Ix(2)][Ix(5)]);
        hb_ADel(aAlias125, 1, true);
        QOut(Len(aRows125[Ix(2)]));

        // a member
        AAdd(oStock125.aItems125, "first");
        QOut(Len(oStock125.aItems125), oStock125.aItems125[Ix(1)]);

        // every dimension of Array()
        QOut(Len(aGrid125), Len(aGrid125[Ix(2)]), ValType(aGrid125[Ix(2)][Ix(3)]));

        // FOR EACH sees an element its body adds
        foreach (dynamic __hb_fe_x in HbEnumValues(aSeen125))
        {
            x = __hb_fe_x;
            if (x < 4)
            {
                AAdd(aSeen125, x + 2);
            }
        }

        QOut(Len(aSeen125), aSeen125[Ix(5)]);

        return;
    }
}
