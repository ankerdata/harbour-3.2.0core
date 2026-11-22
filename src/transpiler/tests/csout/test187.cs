// Test 187: a shape's OPTIONAL keys. `Key AS TYPE OPTIONAL` is a key a
// message may go without: a literal may leave it out, and it is there, to
// hb_HHasKey(), Len() and hb_jsonEncode(), once it is given a value, as a
// key of Harbour's hash is. A number, a logical and text; set by a literal,
// by a subscript and inside an IF; read back typed and through an untyped
// value. The keys are set in the declared order here: on the wire a shape's
// keys stand in that order in C#, and in the order they were set in Harbour.
public sealed class Reply187 : HbShape
{
    public string Type;
    long? _Count;
    public long Count { get => Set( _Count ); set => _Count = value; }
    [HbOptionalKey] public string Note;
    bool? _Paid;
    public bool Paid { get => Set( _Paid ); set => _Paid = value; }
    public decimal Total;
}

public static partial class Program
{
    public static void Main(string[] args)
    {
        var hcBare = new Reply187
        {
            Type = "bare",
            Total = 1.50m
        };
        var hcFull = new Reply187
        {
            Type = "full",
            Count = 3,
            Note = "all",
            Paid = true,
            Total = 2.25m
        };
        var hcBuilt = new Reply187();

        QOut(hb_jsonEncode(hcBare));
        QOut(hb_jsonEncode(hcFull));
        QOut(hb_HHasKey(hcBare, "Count"), hb_HHasKey(hcFull, "Count"), Len(hcBare), Len(hcFull));
        QOut(hcFull.Count + 1, hcFull.Note, hcFull.Paid);

        hcBare.Count = 7;
        hcBare.Paid = false;
        QOut(hcBare.Count * 2, hcBare.Paid, hb_HHasKey(hcBare, "Note"), Len(hcBare));

        hcBuilt.Type = "built";
        if (hcFull.Paid)
        {
            hcBuilt.Note = "paid";
        }

        if (hcBare.Paid)
        {
            hcBuilt.Paid = true;
        }

        hcBuilt.Total = 0.75m;
        QOut(hb_jsonEncode(hcBuilt));

        dynamic xHeld = hcBuilt;
        QOut(hb_HGetDef(xHeld, "Count", -1), hb_HGetDef(xHeld, "Note", "none"), Len(hb_HKeys(xHeld)));
        xHeld["Count"] = 12;
        QOut(hb_HGetDef(xHeld, "Count", -1), hcBuilt.Count, Len(xHeld));
    }
}
