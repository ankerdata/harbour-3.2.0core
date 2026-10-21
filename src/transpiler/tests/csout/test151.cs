// Test 151: every array subscript goes through I().
//
// Harbour's subscript is 1-based, a List's 0-based. Every array subscript
// is `[I(…)]`, a literal with its source value: HbRuntime's I() answers
// the 0-based int and throws on an index below 1 and on a fraction, so
// Harbour's index 0 is a bound error, not C#'s -1. A loop's `i` does not
// hide I(), C# comparing names with their case: `a[I(i)]`. A hash's key,
// a FOR's bounds and HbRuntime's own 1-based positions are untouched.
// A fraction is the one deliberate divergence: Harbour truncates it, the
// C# reports it, so that case runs as C# beside the Harbour line.
public class Shelf151 : IHbObject
{
    public List<dynamic> aSlots;

    public dynamic Slot(dynamic n = default) => aSlots[I( n )];
    public dynamic Second() => aSlots[I(2)];
}

public static partial class Program
{
    public static void Main(string[] args)
    {
        var aList = new List<dynamic> { 10, 20, 30, 40 };
        var aGrid = new List<dynamic>
        {
            new List<dynamic> { 1, 2 },
            new List<dynamic> { 3, 4 }
        };
        var hnByNo = new OrderedDictionary<long, dynamic>
        {
            { 1, "one" },
            { 2, "two" }
        };
        var hByName = new OrderedDictionary<string, dynamic> { { "x", 7 } };
        long iSlot = 0;
        decimal nPos = Val("2");
        HbError oError = null;
        var oShelf = new Shelf151();

        oShelf.aSlots = new List<dynamic> { "a", "b", "c" };
        // literals keep their value
        QOut(aList[I(1)], aList[I(4)]);
        // a whole-number local
        for (iSlot = 1; iSlot <= Len(aList); iSlot++)
        {
            QOut(iSlot, aList[I(iSlot)]);
        }

        // a decimal local, an expression
        QOut(aList[I(nPos)], aList[I(nPos + 1)]);
        // a define
        QOut(aList[I(Test151PrgConst.POS151_THIRD)]);
        // two dimensions
        QOut(aGrid[I(2)][I(1)], aGrid[I(1)][I(2)]);
        // assignment targets
        aList[I(nPos)] = 25;
        aList[I(3)] += 5;
        QOut(aList[I(2)], aList[I(3)]);
        // hash keys pass as they are
        QOut(hnByNo[2], hByName["x"]);
        // INLINE bodies
        QOut(oShelf.Slot(3), oShelf.Second());
        // a 1-based position from the runtime
        QOut(aList[I(AScan(aList, 40))]);

        try
        {
            // index 0: Harbour's bound error
            QOut(aList[I(0)]);
        }
        catch (Exception __hb_ex1) when (__hb_ex1 is not HbQuit)
        {
            oError = HbError.From(__hb_ex1);
            QOut("index 0:", (oError == null ? "no error" : "bound error"));
        }

        nPos = Val("9");
        try
        {
            // past the end: the List's
            QOut(aList[I(nPos)]);
        }
        catch (Exception __hb_ex1) when (__hb_ex1 is not HbQuit)
        {
            oError = HbError.From(__hb_ex1);
            QOut("index 9:", (oError == null ? "no error" : "bound error"));
        }

        // a fraction: Harbour truncates 2.5 to 2 and reads on, the C# reports it

        try
        {
            I(2.5m);
            HbRuntime.QOut("fraction:", "no error");
        }
        catch (System.ArgumentOutOfRangeException)
        {
            HbRuntime.QOut("fraction:", "error");
        }
    }
}
