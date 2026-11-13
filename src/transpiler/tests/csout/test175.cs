// Test 175: whole numbers in, a whole number out.
//
// Min(), Max(), Abs() and Mod() of whole numbers are whole: HbRuntime has
// each for longs beside the one for decimals, C# binds it when every
// argument is a long, and the scan types the call INTEGER, so an `i` name
// takes the result without a cast and a function that returns it is long.
// The same for a difference, a product and a remainder of two whole
// numbers, as a sum already was. A decimal among the arguments or the
// operands keeps the decimal one; a quotient is never whole. A remainder
// and a quotient are printed through Str(): Harbour shows either with
// two decimals, whatever its value.
public static partial class Program
{
    public static long Clamp175(long iValue = default, long iLow = default, long iHigh = default)
    {
        return Max(iLow, Min(iValue, iHigh));
    }

    public static decimal Span175(long iFrom = default, long iTo = default)
    {
        return Abs(iTo - iFrom) * 2 + Mod(iTo, 3);
    }

    public static decimal Share175(decimal nAmount = default, long iParts = default)
    {
        return Max(nAmount / iParts, 0.5m);
    }

    public static void Main(string[] args)
    {
        long iQty = 7;

        long iPick = Min(iQty, 3);
        long iLeft = iQty - iPick * 2 + iQty % 4;
        decimal nPart = Min(2.5m, iQty);

        QOut(iPick, Str(iLeft, 3), nPart);
        QOut(Clamp175(12, 1, 9), Clamp175(-4, 1, 9));
        QOut(Str(Span175(10, 4), 4));
        QOut(Str(Share175(10, 4), 6, 2), Str(Share175(1, 4), 6, 2));
        QOut(Abs(-3) + Abs(2.5m));
    }
}
