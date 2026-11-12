// Test 170: a record written as JSON is the array Harbour holds. A record
// is a class in C#, and hb_jsonEncode() has no form for an object, so it
// would write null where Harbour writes the positions: the encoder writes
// a record as the array of its fields, on its own, in a list, nested in
// another record and as a hash's value.
public sealed class Plug170
{
    public string cName;
    public long iPins;
    public bool lEarthed;

    public Plug170(string cName, long iPins, bool lEarthed)
    {
        this.cName = cName;
        this.iPins = iPins;
        this.lEarthed = lEarthed;
    }
}

public sealed class Lead170
{
    public Plug170 aPlug;
    public decimal nMetres;
    public List<string> acColours;

    public Lead170(Plug170 aPlug, decimal nMetres, List<string> acColours)
    {
        this.aPlug = aPlug;
        this.nMetres = nMetres;
        this.acColours = acColours;
    }
}

public static partial class Program
{
    public static void Main(string[] args)
    {
        var aPlug = new Plug170("Schuko", 2, true);
        var aPlugs = new List<Plug170>
        {
            new Plug170("BS 1363", 3, true),
            new Plug170("Europlug", 2, false)
        };
        var aLead = new Lead170(
            aPlug,
            1.5m,
            new List<string> { "brown", "blue" });
        var hStock = new OrderedDictionary<string, dynamic>
        {
            { "plug", aPlug },
            { "count", 2 }
        };

        QOut(hb_jsonEncode(aPlug));
        QOut(hb_jsonEncode(aPlugs));
        QOut(hb_jsonEncode(aLead));
        QOut(hb_jsonEncode(hStock));
        QOut(hb_jsonEncode(new List<dynamic> { aPlug, "spare" }));
    }
}
