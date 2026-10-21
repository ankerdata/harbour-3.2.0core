// Test 156: typed hashes (plan C12 / C16). `AS HASH OF RECORD X` is a
// hash keyed by strings whose values are records, `AS INTEGER HASH OF
// CHARACTER` one keyed by whole numbers whose values are strings; C# gets
// OrderedDictionary<string, X> and OrderedDictionary<long, string>, the
// values typed in storage, as a FIXED array's are. A `{ k => v }` literal
// under the declaration is the dictionary of those types, each value a
// record's constructor; hb_HSet()'s value takes the type too. A read
// `h[ k ]` is the value, so `h[ k ][ FLD_LEN ]` is the record's field;
// FOR EACH over the hash types the loop variable. A key of the other
// kind is W0035, a value of another type W0041 (tests/errors/
// typed_hash.prg). Harbour sees a plain hash: astype.ch removes the
// declarations.
public sealed class FieldRow156
{
    public string cName;
    public string cType;
    public long iLen;
    public long iDec;

    public FieldRow156(string cName, string cType, long iLen, long iDec)
    {
        this.cName = cName;
        this.cType = cType;
        this.iLen = iLen;
        this.iDec = iDec;
    }
}

public sealed class Table156
{
    public string cAlias;
    public OrderedDictionary<string, FieldRow156> hFields;
    public OrderedDictionary<long, string> hnNames;

    public Table156(string cAlias, OrderedDictionary<string, FieldRow156> hFields, OrderedDictionary<long, string> hnNames)
    {
        this.cAlias = cAlias;
        this.hFields = hFields;
        this.hnNames = hnNames;
    }
}

public static partial class Program
{
    public static OrderedDictionary<string, FieldRow156> test156_shByName = new OrderedDictionary<string, FieldRow156>
    {
        { "PLUNO", new FieldRow156("PLUNO", "N", 16, 0) },
        { "CAPTION", new FieldRow156("CAPTION", "C", 40, 0) }
    };
    public static void Main(string[] args)
    {
        Table156 aTable = MakeTable156();
        var hnNames = new OrderedDictionary<long, string>
        {
            { 1, "one" },
            { 2, "two" }
        };

        // a record into the hash: its constructor
        hb_HSet(test156_shByName, "PRICE1", new FieldRow156("PRICE1", "N", 12, 3));
        // the value's field, by a define
        test156_shByName["PRICE1"].iLen = 15;
        QOut(Len(test156_shByName), test156_shByName["CAPTION"].iLen, test156_shByName["PRICE1"].iLen);
        // the values, typed
        foreach (FieldRow156 aRow in HbEnumValues(test156_shByName))
        {
            QOut("  row", aRow.cName, aRow.iLen);
        }

        foreach (string cKey in HbEnumValues(hb_HKeys(test156_shByName)))
        {
            QOut("  key", cKey, Width156(test156_shByName[cKey]));
        }

        hnNames[3] = "three";
        QOut(Len(hnNames), hnNames[2], hnNames[3]);
        QOut(aTable.cAlias, Len(aTable.hFields), aTable.hFields["CAPTION"].cName, aTable.hnNames[2]);
        QOut(Count156(aTable.hFields), Count156(test156_shByName));

        // a record holding typed hashes, built from one literal
    }
    public static Table156 MakeTable156()
    {
        return new Table156(
            "PLU",
            new OrderedDictionary<string, FieldRow156>
            {
                { "CAPTION", new FieldRow156("CAPTION", "C", 40, 0) }
            },
            new OrderedDictionary<long, string>
            {
                { 1, "a" },
                { 2, "b" }
            });
    }

    public static dynamic Width156(FieldRow156 aRow = default)
    {
        return aRow.iLen + aRow.iDec;

        // a typed hash parameter: FOR EACH over its values
    }
    public static long Count156(OrderedDictionary<string, FieldRow156> hRows = default)
    {
        long iTotal = 0;
        foreach (FieldRow156 aRow in HbEnumValues(hRows))
        {
            iTotal += (long)(aRow.iLen);
        }

        return iTotal;
    }
}
