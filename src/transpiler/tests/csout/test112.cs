// Test 112: hb_ADel / hb_AIns with lAutoSize shrink and grow the array.
//
// Harbour's hb_ADel( a, n, .T. ) deletes and shortens, hb_AIns( a, n, x,
// .T. ) lengthens and inserts. A C# array cannot change length in place,
// so the resizing overloads take the array by ref, as AAdd and ASize
// do — but the emitter passed `ref` to AAdd and ASize only, so every
// EasiPOS call (all pass .T.: the sale buffer's line delete and insert)
// left the array its old length: a NIL at the end after a delete, the
// last line lost after an insert. The scan's list of array mutators
// grows with it, so a routine that resizes its array parameter keeps
// that parameter `ref`.
public class SaleBuffer : IHbObject
{
    public List<dynamic> aLines = new List<dynamic>();
    public dynamic xQueue;

}

public static partial class Program
{
    public static void Main(string[] args)
    {
        List<dynamic> aNums = new List<dynamic> { 1, 2, 3, 4 };
        SaleBuffer oBuf = new SaleBuffer();

        hb_ADel(aNums, 2, true);
        QOut(Len(aNums), aNums[I(2)]);
        hb_AIns(aNums, 1, 9, true);
        QOut(Len(aNums), aNums[I(1)], aNums[I(4)]);
        hb_ADel(aNums, 1);
        QOut(Len(aNums), ValType(aNums[I(4)]));

        oBuf.aLines = new List<dynamic> { "a", "b", "c" };
        hb_AIns(oBuf.aLines, 2, "x", true);
        QOut(Len(oBuf.aLines), oBuf.aLines[I(2)], oBuf.aLines[I(4)]);
        hb_ADel(oBuf.aLines, 1, true);
        QOut(Len(oBuf.aLines), oBuf.aLines[I(1)]);

        oBuf.xQueue = new List<dynamic> { 10, 20 };
        hb_ADel(oBuf.xQueue, 1, true);
        QOut(Len(oBuf.xQueue), oBuf.xQueue[I(1)]);
        hb_AIns(oBuf.xQueue, 2, 30, true);
        QOut(Len(oBuf.xQueue), oBuf.xQueue[I(2)]);

        aNums = new List<dynamic> { 5, 6, 7 };
        BufferTrim(aNums);
        QOut(Len(aNums), aNums[I(1)]);
    }

    public static void BufferTrim(List<dynamic> aList)
    {
        hb_ADel(aList, 1, true);
    }
}
