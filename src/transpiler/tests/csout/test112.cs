using System;
using static HbRuntime;
using static Program;

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
// #include "hbclass.ch"
public class SaleBuffer
{
    public dynamic[] aLines = System.Array.Empty<dynamic>();
    public dynamic xQueue;

}

public static partial class Program
{
    public static void Main(string[] args)
    {
        dynamic[] aNums = new dynamic[] { 1, 2, 3, 4 };
        SaleBuffer oBuf = new SaleBuffer();

        HbRuntime.hb_ADel(ref aNums, 2, true);
        HbRuntime.QOut(HbRuntime.Len(aNums), aNums[1]);
        HbRuntime.hb_AIns(ref aNums, 1, 9, true);
        HbRuntime.QOut(HbRuntime.Len(aNums), aNums[0], aNums[3]);
        HbRuntime.hb_ADel(ref aNums, 1);
        HbRuntime.QOut(HbRuntime.Len(aNums), HbRuntime.ValType(aNums[3]));

        oBuf.aLines = new dynamic[] { "a", "b", "c" };
        HbRuntime.hb_AIns(ref oBuf.aLines, 2, "x", true);
        HbRuntime.QOut(HbRuntime.Len(oBuf.aLines), oBuf.aLines[1], oBuf.aLines[3]);
        HbRuntime.hb_ADel(ref oBuf.aLines, 1, true);
        HbRuntime.QOut(HbRuntime.Len(oBuf.aLines), oBuf.aLines[0]);

        oBuf.xQueue = new dynamic[] { 10, 20 };
        HbRuntime.hb_ADel(ref oBuf.xQueue, 1, true);
        HbRuntime.QOut(HbRuntime.Len(oBuf.xQueue), oBuf.xQueue[0]);
        HbRuntime.hb_AIns(ref oBuf.xQueue, 2, 30, true);
        HbRuntime.QOut(HbRuntime.Len(oBuf.xQueue), oBuf.xQueue[1]);

        aNums = new dynamic[] { 5, 6, 7 };
        BufferTrim(ref aNums);
        HbRuntime.QOut(HbRuntime.Len(aNums), aNums[0]);

        return;
    }

    public static void BufferTrim(ref dynamic[] aList)
    {
        HbRuntime.hb_ADel(ref aList, 1, true);
        return;
    }
}
