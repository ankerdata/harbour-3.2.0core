// Test 155: records and fixed arrays (plan C12 / C16).
//
// An array used as a record declares its positions once, by name and
// type: `RECORD FieldRow( cName, cType, iLen, iDec )`, each field typed by
// its prefix or an `AS` of its own. A variable, parameter, member or
// return declared `AS RECORD X` holds one; `AS FIXED ARRAY OF RECORD X`
// an array of them that never grows or shrinks. Harbour sees arrays
// (astype.ch removes the RECORD line and reads the declarations as AS
// ARRAY), C# a class of the fields
// built by its constructor from the `{ … }` literal, and a C# array for
// FIXED. A read with a constant subscript — a literal or a #define — is
// the field itself (`aRow.iLen`), typed and checked when C# builds. The
// class is plain, fields and a constructor and nothing else (Alex), so a
// subscript that is not a constant is W0046 (tests/errors/
// fixed_record.prg), and code that holds a row as `dynamic` (Dump155
// below, whose x parameter takes anything) declares it before reading
// it. The rows are shared, as Harbour's are: a write through one holder
// shows through the other.
// Harbour compiles this file itself for the reference run: astype.ch
// removes the RECORD lines and reads the declarations as AS ARRAY.
public sealed class FieldRow
{
    public string cName;
    public string cType;
    public long iLen;
    public long iDec;

    public FieldRow(string cName, string cType, long iLen, long iDec)
    {
        this.cName = cName;
        this.cType = cType;
        this.iLen = iLen;
        this.iDec = iDec;
    }
}

public sealed class IndexRows
{
    public string[] aNames;
    public string[] aExpressions;
    public string[] aUnique;

    public IndexRows(string[] aNames, string[] aExpressions, string[] aUnique)
    {
        this.aNames = aNames;
        this.aExpressions = aExpressions;
        this.aUnique = aUnique;
    }
}

public sealed class TableDef
{
    public string cAlias;
    public FieldRow[] aFields;
    public IndexRows aIndexes;
    public OrderedDictionary<string, dynamic> hHash;
    public string cDatabase;

    public TableDef(string cAlias, FieldRow[] aFields, IndexRows aIndexes, OrderedDictionary<string, dynamic> hHash, string cDatabase)
    {
        this.cAlias = cAlias;
        this.aFields = aFields;
        this.aIndexes = aIndexes;
        this.hHash = hHash;
        this.cDatabase = cDatabase;
    }
}

public static partial class Program
{
    public static FieldRow[] test155_saFields = new FieldRow[]
    {
        new FieldRow("PLUNO", "N", 16, 0),
        new FieldRow("CAPTION", "C", 40, 0),
        new FieldRow("PRICE1", "N", 12, 3)
    };
    public static void Main(string[] args)
    {
        TableDef aDef = PluDef155();
        FieldRow aRow = default;
        FieldRow[] aFields = aDef.aFields;
        // a list of records, grown
        List<dynamic> aMore = new List<dynamic>
        {
            new FieldRow("TAX", "N", 1, 0)
        };
        long iPos = default;

        // fields of the def, the fixed array's length
        QOut(aDef.cAlias, aDef.cDatabase, Len(aDef.aFields));
        // the fixed array, a row's field by define
        QOut(Len(aFields), aFields[Ix(1)].cName);
        // nested: a field's field, a fixed array of strings in a record
        QOut(aDef.aFields[Ix(3)].iLen, aDef.aIndexes.aNames[Ix(2)]);
        // a computed subscript, then a constant
        for (iPos = 1; iPos <= Len(aFields); iPos++)
        {
            QOut(iPos, aFields[Ix(iPos)].cName, aFields[Ix(iPos)].iLen + aFields[Ix(iPos)].iDec);
        }

        // the loop variable declared a record
        foreach (FieldRow __hb_fe_aRow in HbEnumValues(aFields))
        {
            aRow = __hb_fe_aRow;
            QOut(aRow.cType, Width155(aRow));
        }

        // a row shared with the static
        aRow = aFields[Ix(2)];
        aRow.iLen = 45;
        // seen through every holder
        QOut(test155_saFields[Ix(2)].iLen, aDef.aFields[Ix(2)].iLen);
        QOut(AScan(aFields, ((Func<FieldRow, bool>)((a) => a.cName == "PRICE1"))));
        QOut(hb_HGet(aDef.hHash, "PLUNO"), Len(aDef.aIndexes.aNames));
        // the fixed array to code that takes anything
        Dump155(aDef.aFields);
        // a record into a list: the literal is its constructor
        AAdd(aMore, new FieldRow("KP", "C", 8, 0));
        aMore[Ix(2)].iLen = 9;
        QOut(Len(aMore), aMore[Ix(1)].cName, aMore[Ix(2)].iLen, Width155(aMore[Ix(2)]));
        foreach (FieldRow __hb_fe_aRow in HbEnumValues(aMore))
        {
            aRow = __hb_fe_aRow;
            QOut("  more", aRow.cName, aRow.cType);
        }

        return;

        // a def, as pludef.prg's PLUDef() builds one: the literal is the record's
        // constructor, its fields' literals theirs
    }
    public static TableDef PluDef155()
    {
        TableDef aDef = new TableDef(
            "PLU",
            test155_saFields,
            new IndexRows(
                new string[] { "PLNUM", "PLCAP" },
                new string[] { "pluno", "caption" },
                new string[] { "PLNUM" }),
            new OrderedDictionary<string, dynamic>
            {
                { "PLUNO", "nNo" },
                { "CAPTION", "cName" }
            },
            "PRG");
        return aDef;

        // a record parameter: its fields typed
    }
    public static long Width155(FieldRow aRow = default)
    {
        long iWidth = (long)(aRow.iLen);
        if (aRow.iDec > 0)
        {
            iWidth += (long)(aRow.iDec + 1);
        }

        return iWidth;

        // a reader that takes anything: the fixed array is an array to it, and a
        // row it holds as `dynamic` is read once declared
    }
    public static void Dump155(dynamic xAny = default)
    {
        dynamic xItem = default;
        FieldRow aRow = default;
        QOut(ValType(xAny), Len(xAny));
        foreach (dynamic __hb_fe_xItem in HbEnumValues(xAny))
        {
            xItem = __hb_fe_xItem;
            aRow = xItem;
            QOut("  row", aRow.cName, aRow.iLen);
        }

        return;
    }
}
