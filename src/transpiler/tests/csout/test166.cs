// Test 166: array names that declare their elements. `an`, `ai` and `ac`
// before a capital (a static's `san`, `sai`, `sac`) are an array of
// numbers, of whole numbers and of strings, as AS ARRAY OF NUMERIC,
// INTEGER and CHARACTER declare one: a List<decimal>, a List<long> and a
// List<string>, on a local, a static, a parameter, a member and a
// record's field, and a literal given one is built as it. AS ARRAY OF
// STRING is AS ARRAY OF CHARACTER. AAdd() puts a whole literal into a
// List<long>, hb_AIns() and hb_ADel() with lAutoSize work on a value
// list, which never holds NIL, and the literal default of hb_default() and
// hb_defaultValue() is the value's own type, a static left NIL included.
public sealed class Tally166
{
    public string cName;
    public List<decimal> anAmounts;

    public Tally166(string cName, List<decimal> anAmounts)
    {
        this.cName = cName;
        this.anAmounts = anAmounts;
    }
}

public class Till166 : IHbObject
{
    public List<string> acNames = new List<string>();

    public virtual Till166 Add166(string cName = default)
    {
        AAdd(acNames, cName);

        return this;
    }
}

public static partial class Program
{
    public static List<long> test166_saiSeen166 = new List<long>();
    public static List<long> test166_saiUnset166;
    public static void Main(string[] args)
    {
        var aiIds = new List<long> { 1, 2 };
        var anPrices = new List<decimal> { 1.5m, 2 };
        var aWords = new List<string> { "one" };
        var oTill166 = new Till166();
        var aTally = new Tally166(
            "lunch",
            new List<decimal> { 2.25m, 3 });
        List<long> aiUnset = hb_defaultValue(Unset166(), new List<long>());

        AAdd(aiIds, 3);
        hb_AIns(aiIds, 1, 7, true);
        AAdd(anPrices, 0.5m);
        hb_ADel(anPrices, 1, true);
        AAdd(test166_saiSeen166, Len(aiIds));
        oTill166.Add166("first").Add166("second");
        QOut(Sum166(aiIds), aiIds[I(1)], Total166(anPrices), Total166(aTally.anAmounts));
        QOut(Len(aWords), oTill166.acNames[I(2)], test166_saiSeen166[I(1)], Words166("a")[I(1)], Sum166(), Len(aiUnset));
    }

    public static long Sum166(List<long> aiValues = default)
    {
        long iSum = 0;

        hb_default(ref aiValues, new List<long>());
        foreach (long iValue in HbEnumValues(aiValues))
        {
            iSum += iValue;
        }

        return iSum;
    }

    public static decimal Total166(List<decimal> anValues = default)
    {
        decimal nTotal = 0;

        foreach (decimal nValue in HbEnumValues(anValues))
        {
            nTotal += nValue;
        }

        return nTotal;
    }

    public static List<long> Unset166()
    {
        return test166_saiUnset166;
    }

    public static List<string> Words166(string cWord = default)
    {
        var acWords = new List<string> { cWord };

        return acWords;
    }
}
