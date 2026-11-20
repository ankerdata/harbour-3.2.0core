// Test 184: a function that answers a value of its default's type.
// `xDefault AS T` and `AS T` make the function generic: Pick184() is
// `T Pick184<T>( ..., T xDefault )`, so a call that passes "" answers a
// string, one that passes .F. a logical, and C# checks what is done with
// each. A whole literal is a long, a decimal one a decimal; a whole
// decimal in the hash is taken for a whole T; a NIL in the hash is the
// type's zero for a number or a logical. Item184() is generic twice over,
// by its list and by its default.
public static partial class Program
{
    public static void Main(string[] args)
    {
        var hcData184 = new OrderedDictionary<string, dynamic>
        {
            { "name", "Ann" },
            { "age", 41 },
            { "price", 12.50m },
            { "open", true },
            { "whole", 7.00m },
            { "none", null },
            { "tags", new List<dynamic> { "a", "b" } }
        };
        var acWords184 = new List<string> { "one", "two" };
        var aiIds184 = new List<long> { 10, 20 };

        // the value's type is the default's
        string cName184 = Pick184(hcData184, "name", "");
        long iAge184 = Pick184(hcData184, "age", 0L);
        decimal nPrice184 = Pick184(hcData184, "price", 0.00m);
        bool lOpen184 = Pick184(hcData184, "open", false);
        QOut(Upper(cName184), iAge184 + 1, nPrice184 * 2, lOpen184);

        // a key the hash has not: the default itself

        QOut(Pick184(hcData184, "missing", "-"), Pick184(hcData184, "missing", -1L), Pick184(hcData184, "missing", true));

        // used where it stands
        QOut(Len(Pick184(hcData184, "name", "")), Pick184(hcData184, "age", 0L) * 2);

        // a whole decimal for a whole T; NIL for a number and a logical is
        // the type's zero in C#, and empty in both

        QOut(Pick184(hcData184, "whole", 0L) == 7, Empty(Pick184(hcData184, "none", 0L)), Empty(Pick184(hcData184, "none", false)));

        // a list as the default
        List<dynamic> aTags184 = Pick184(hcData184, "tags", new List<dynamic>());
        QOut(Len(aTags184), Len(Pick184(hcData184, "missing", new List<dynamic>())));

        // T from the list and from the default
        QOut(Item184(acWords184, 2, ""), Item184(acWords184, 5, "none"));
        QOut(Item184(aiIds184, 1, 0L) + 1, Item184(aiIds184, 9, -1L));
    }

    public static T Pick184<T>(OrderedDictionary<string, dynamic> hcData = default, string cKey = default, T xDefault = default)
    {
        if (hb_HHasKey(hcData, cKey))
        {
            return HbAs<T>(hcData[cKey]);
        }

        return xDefault;
    }

    public static T Item184<T>(List<T> aArray = default, long iIndex = default, T xDefault = default)
    {
        if (iIndex >= 1 && iIndex <= Len(aArray))
        {
            return aArray[I(iIndex)];
        }

        return xDefault;
    }
}
