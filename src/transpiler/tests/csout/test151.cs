using System;
using static HbRuntime;
using static Program;

// Test 151: every array subscript goes through Ix().
//
// Harbour's subscript is 1-based, a List's 0-based. The emitter rebased
// it by hand: a literal decremented where it stood (`aCSV[19]` became
// `aCSV[18]`), anything else `[(int)(…) - 1]`. That truncated a
// fractional index silently, reported Harbour's index 0 as C#'s -1, cut
// the link between a literal and its source line, and read as noise
// (`[(int)(i + 1) - 1]`). Now every array subscript is `[Ix(…)]`, the
// literal with its source value: HbRuntime's Ix() answers the 0-based
// int and throws on an index below 1 and on a fraction. A hash's key, a
// FOR's bounds and HbRuntime's own 1-based positions are untouched.
// A fraction is the one deliberate divergence: Harbour truncates it, the
// C# reports it, so that case runs as C# beside the Harbour line.

// #include "hbclass.ch"
public class Shelf151 : IHbObject
{
    public List<dynamic> aSlots;

    public dynamic Slot(dynamic n = default) => aSlots[Ix( n )];
    public dynamic Second() => aSlots[Ix(2)];
}

public static partial class Program
{
    public static void Main(string[] args)
    {
        List<dynamic> aList = new List<dynamic> { 10, 20, 30, 40 };
        List<dynamic> aGrid = new List<dynamic> { new List<dynamic> { 1, 2 }, new List<dynamic> { 3, 4 } };
        OrderedDictionary<long, dynamic> hnByNo = new OrderedDictionary<long, dynamic> { { 1, "one" }, { 2, "two" } };
        OrderedDictionary<string, dynamic> hByName = new OrderedDictionary<string, dynamic> { { "x", 7 } };
        long iSlot = default;
        decimal nPos = HbRuntime.Val("2");
        HbError oError = default;
        Shelf151 oShelf = new Shelf151();

        oShelf.aSlots = new List<dynamic> { "a", "b", "c" };
        // literals keep their value
        HbRuntime.QOut(aList[Ix(1)], aList[Ix(4)]);
        // a whole-number local
        for (iSlot = 1; iSlot <= HbRuntime.Len(aList); iSlot++)
        {
            HbRuntime.QOut(iSlot, aList[Ix(iSlot)]);
        }

        // a decimal local, an expression
        HbRuntime.QOut(aList[Ix(nPos)], aList[Ix(nPos + 1)]);
        // a define
        HbRuntime.QOut(aList[Ix(Test151PrgConst.POS151_THIRD)]);
        // two dimensions
        HbRuntime.QOut(aGrid[Ix(2)][Ix(1)], aGrid[Ix(1)][Ix(2)]);
        // assignment targets
        aList[Ix(nPos)] = 25;
        aList[Ix(3)] += 5;
        HbRuntime.QOut(aList[Ix(2)], aList[Ix(3)]);
        // hash keys pass as they are
        HbRuntime.QOut(hnByNo[2], hByName["x"]);
        // INLINE bodies
        HbRuntime.QOut(oShelf.Slot(3), oShelf.Second());
        // a 1-based position from the runtime
        HbRuntime.QOut(aList[Ix(HbRuntime.AScan(aList, 40))]);

        try
        {
            // index 0: Harbour's bound error
            HbRuntime.QOut(aList[Ix(0)]);
        }
        catch (Exception __hb_ex1) when (__hb_ex1 is not HbQuit)
        {
            oError = HbError.From(__hb_ex1);
            HbRuntime.QOut("index 0:", (oError == null ? "no error" : "bound error"));
        }

        nPos = HbRuntime.Val("9");
        try
        {
            // past the end: the List's
            HbRuntime.QOut(aList[Ix(nPos)]);
        }
        catch (Exception __hb_ex1) when (__hb_ex1 is not HbQuit)
        {
            oError = HbError.From(__hb_ex1);
            HbRuntime.QOut("index 9:", (oError == null ? "no error" : "bound error"));
        }

        // a fraction: Harbour truncates 2.5 to 2 and reads on, the C# reports it

        try
        {
            Ix(2.5m);
            HbRuntime.QOut("fraction:", "no error");
        }
        catch (System.ArgumentOutOfRangeException)
        {
            HbRuntime.QOut("fraction:", "error");
        }

        return;
    }
}
