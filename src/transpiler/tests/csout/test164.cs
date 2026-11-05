// Test 164: typed lists. An array of strings, or of such arrays, is a C#
// List<string> / List<List<string>> where any other array is a
// List<dynamic> (plan C12). Declared, its literals are typed, an empty one
// too; an undeclared `a` local or parameter given one becomes one, as for a
// FIXED array, and so does an undeclared function's return; a row read from
// one is a List<string>; AAdd() and FOR EACH work on it as on any array, and
// AClone() gives back a copy of the same type.
public class Store164 : IHbObject
{
    public List<List<string>> aRows = new List<List<string>>();

    public virtual List<List<string>> Fetch164(string cWhere = default)
    {
        var aResult = new List<List<string>>
        {
            new List<string> { "cName" }
        };

        foreach (List<string> aRow in HbEnumValues(aRows))
        {
            if (aRow[I(1)] == cWhere)
            {
                AAdd(aResult, new List<string> { aRow[I(2)] });
            }
        }

        return aResult;
    }
}

public static partial class Program
{
    public static void Main(string[] args)
    {
        var oStore164 = new Store164();
        var aNames = new List<string>();

        AAdd(oStore164.aRows, new List<string> { "red", "apple" });
        AAdd(oStore164.aRows, new List<string> { "green", "pear" });
        AAdd(oStore164.aRows, new List<string> { "red", "cherry" });

        List<List<string>> aTable = oStore164.Fetch164("red");
        List<string> aRow = aTable[I(2)];
        QOut(Len(aTable), aRow[I(1)], Upper(aTable[I(3)][I(1)]));
        QOut(Count164(aTable), Count164(Rows164(false)), Last164(Rows164(true)));

        List<List<string>> aCopy = AClone(aTable);
        aCopy[I(2)][I(1)] = "pear";
        QOut(Len(aCopy), aCopy[I(2)][I(1)], aTable[I(2)][I(1)]);

        AAdd(aNames, "first");
        aNames = Names164(aNames);
        QOut(Len(aNames), aNames[I(2)]);
        QOut(Mixed164(true), Mixed164(false));
    }

    public static List<List<string>> Rows164(bool lAny = default)
    {
        if (!lAny)
        {
            return new List<List<string>>();
        }

        return new List<List<string>>
        {
            new List<string> { "cName" },
            new List<string> { "one" }
        };
    }

    public static decimal Count164(List<List<string>> aRows = default)
    {
        return Max(Len(aRows) - 1, 0);
    }

    public static string Last164(List<List<string>> aRows = default)
    {
        return Lower(aRows[I(Len(aRows))][I(1)]);
    }

    public static List<string> Names164(List<string> aList = default)
    {
        AAdd(aList, "second");

        return aList;

        // a local given a typed list in one place and a plain array in another is
        // dynamic, and stays so
    }
    public static long Mixed164(bool lRows = default)
    {
        dynamic aLines;

        if (lRows)
        {
            aLines = Rows164(true);
        }
        else
        {
            aLines = new List<dynamic> { 1, 2, 3 };
        }

        return Len(aLines);
    }
}
