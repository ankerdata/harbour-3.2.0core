// Test 189: a shape's key that takes anything, `Key AS USUAL`. Wrap189 is
// an envelope: what it is of is text, and what it carries is whatever that
// says: a shape, a list, a number, text, nothing. The key is a field that
// holds any of them, written as JSON as what it holds and read back by key;
// a function returns the envelope, built from its parameter, and an
// OPTIONAL key after it is still the last on the wire.
public sealed class Note189 : HbShape
{
    public string Title;
    public long Count;
}

public sealed class Wrap189 : HbShape
{
    public string Kind;
    public dynamic Body;
    long? _Serial;
    public long Serial { get => Set( _Serial ); set => _Serial = value; }
}

public static partial class Program
{
    public static void Main(string[] args)
    {
        var hcNote = new Note189
        {
            Title = "first",
            Count = 2
        };
        var acWords = new List<string> { "one", "two" };

        Wrap189 hcWrap = MakeWrap189("note", hcNote);
        QOut(hb_jsonEncode(hcWrap));
        QOut(Len(hcWrap), hb_HGet(hcWrap.Body, "Title"), hb_HHasKey(hcWrap, "Serial"));

        hcWrap = MakeWrap189("words", acWords);
        QOut(hb_jsonEncode(hcWrap), Len(hcWrap.Body));

        hcWrap = MakeWrap189("number", 12.5m);
        QOut(hb_jsonEncode(hcWrap), hcWrap.Body * 2);

        hcWrap = MakeWrap189("text", "plain");
        QOut(hb_jsonEncode(hcWrap), Upper(hcWrap.Body));

        hcWrap = MakeWrap189("nothing");
        QOut(hb_jsonEncode(hcWrap), hcWrap.Body == null);

        hcWrap.Body = true;
        hcWrap.Serial = 7;
        QOut(hb_jsonEncode(hcWrap), Len(hcWrap), hb_HKeyAt(hcWrap, 3));
    }

    public static Wrap189 MakeWrap189(string cKind = default, dynamic xBody = default)
    {
        var hcWrap = new Wrap189
        {
            Kind = cKind,
            Body = xBody
        };

        return hcWrap;
    }
}
