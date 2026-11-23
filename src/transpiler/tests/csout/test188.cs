// Test 188: a shape of more than 64 keys, and a list of shapes of several
// kinds. Wide188 has 70 keys, the last of them OPTIONAL: every one is a
// field, read and written by its key, and the optional one is there once
// it is set. `AS ARRAY OF SHAPE` with no name holds any shape: lines of two
// kinds are added to one list, written as JSON in their order, and read
// back through the key they share.
public sealed class Wide188 : HbShape
{
    public long K01;
    public long K02;
    public long K03;
    public long K04;
    public long K05;
    public long K06;
    public long K07;
    public long K08;
    public long K09;
    public long K10;
    public long K11;
    public long K12;
    public long K13;
    public long K14;
    public long K15;
    public long K16;
    public long K17;
    public long K18;
    public long K19;
    public long K20;
    public long K21;
    public long K22;
    public long K23;
    public long K24;
    public long K25;
    public long K26;
    public long K27;
    public long K28;
    public long K29;
    public long K30;
    public long K31;
    public long K32;
    public long K33;
    public long K34;
    public long K35;
    public long K36;
    public long K37;
    public long K38;
    public long K39;
    public long K40;
    public long K41;
    public long K42;
    public long K43;
    public long K44;
    public long K45;
    public long K46;
    public long K47;
    public long K48;
    public long K49;
    public long K50;
    public long K51;
    public long K52;
    public long K53;
    public long K54;
    public long K55;
    public long K56;
    public long K57;
    public long K58;
    public long K59;
    public long K60;
    public long K61;
    public long K62;
    public long K63;
    public long K64;
    public long K65;
    public long K66;
    public long K67;
    public long K68;
    public string Label;
    long? _Late;
    public long Late { get => Set( _Late ); set => _Late = value; }
}

public sealed class Item188 : HbShape
{
    public long Type;
    public string Name;
    public decimal Price;
}

public sealed class Text188 : HbShape
{
    public long Type;
    public string Text;
}

public sealed class Slip188 : HbShape
{
    public string Title;
    public List<HbShape> Lines;
}

public static partial class Program
{
    public static void Main(string[] args)
    {
        var hcWide = new Wide188
        {
            K01 = 3,
            K02 = 6,
            K03 = 9,
            K04 = 12,
            K05 = 15,
            K06 = 18,
            K07 = 21,
            K08 = 24,
            K09 = 27,
            K10 = 30,
            K11 = 33,
            K12 = 36,
            K13 = 39,
            K14 = 42,
            K15 = 45,
            K16 = 48,
            K17 = 51,
            K18 = 54,
            K19 = 57,
            K20 = 60,
            K21 = 63,
            K22 = 66,
            K23 = 69,
            K24 = 72,
            K25 = 75,
            K26 = 78,
            K27 = 81,
            K28 = 84,
            K29 = 87,
            K30 = 90,
            K31 = 93,
            K32 = 96,
            K33 = 99,
            K34 = 102,
            K35 = 105,
            K36 = 108,
            K37 = 111,
            K38 = 114,
            K39 = 117,
            K40 = 120,
            K41 = 123,
            K42 = 126,
            K43 = 129,
            K44 = 132,
            K45 = 135,
            K46 = 138,
            K47 = 141,
            K48 = 144,
            K49 = 147,
            K50 = 150,
            K51 = 153,
            K52 = 156,
            K53 = 159,
            K54 = 162,
            K55 = 165,
            K56 = 168,
            K57 = 171,
            K58 = 174,
            K59 = 177,
            K60 = 180,
            K61 = 183,
            K62 = 186,
            K63 = 189,
            K64 = 192,
            K65 = 195,
            K66 = 198,
            K67 = 201,
            K68 = 204,
            Label = "wide"
        };
        var aLines = new List<HbShape>();
        var hcItem = new Item188
        {
            Type = 1,
            Name = "tea",
            Price = 1.50m
        };
        var hcText = new Text188
        {
            Type = 3,
            Text = "thank you"
        };
        long iTypes = 0;

        QOut(Len(hcWide), hcWide.K01, hcWide.K64, hcWide.K65, hcWide.K68, hcWide.Label);
        QOut(hb_HHasKey(hcWide, "Late"), hb_HHasKey(hcWide, "K66"));
        hcWide.K66 = 1000;
        hcWide.Late = 7;
        QOut(hcWide.K66 + hcWide.Late, hb_HHasKey(hcWide, "Late"), Len(hcWide));
        QOut(Right(hb_jsonEncode(hcWide), 44));

        AAdd(aLines, hcItem);
        AAdd(aLines, hcText);
        AAdd(aLines, MakeItem188("scone", 2.25m));
        QOut(Len(aLines), hb_jsonEncode(aLines));
        foreach (HbShape hcLine in HbEnumValues(aLines))
        {
            iTypes += (long)(hb_HGet(hcLine, "Type"));
        }

        QOut(iTypes);

        var hcSlip = new Slip188
        {
            Title = "slip",
            Lines = aLines
        };
        QOut(hb_jsonEncode(hcSlip));
        QOut(Len(hcSlip.Lines), hb_HGet(hcSlip.Lines[I(2)], "Text"));
    }

    public static Item188 MakeItem188(string cName = default, decimal nPrice = default)
    {
        return new Item188
        {
            Type = 1,
            Name = cName,
            Price = nPrice
        };
    }
}
