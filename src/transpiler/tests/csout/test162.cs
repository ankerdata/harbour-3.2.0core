// Test 162: HbRuntime's whole-number functions return long. An `i` name
// takes one without a cast; a local a call to one initialises, used as a
// subscript, is long (Pass 2.5 counts what hbfuncs.tab calls INTEGER as a
// whole number); a `/` of two keeps Harbour's fraction. A `+` of two is
// whole, of one and a literal a number, of a date and one a date.
public static partial class Program
{
    public static void Main(string[] args)
    {
        QOut(Find162(new List<dynamic> { "a", "bb", "ccc" }, "bb"), Find162(new List<dynamic> { "a" }, "z"));
        QOut(Half162(7), Half162(10));
        QOut(Parts162(new DateOnly(2026, 10, 30)));
        QOut(Quarter162(new DateOnly(2026, 10, 30)));
        QOut(Next162("abc"), Both162("ab", "cde"));
        QOut(DToS(Later162(new DateOnly(2026, 10, 30), 2.5m)));
    }

    public static dynamic Find162(List<dynamic> aList = default, string cWant = default)
    {
        long nPos = AScan(aList, cWant);

        if (nPos > 0)
        {
            return aList[I(nPos)];
        }

        return "-";
    }

    public static long Half162(decimal nTotal = default)
    {
        long iHalf = Int(nTotal / 2);

        return iHalf;
    }

    public static string Parts162(DateOnly dDay = default)
    {
        long iYear = Year(dDay);

        return Str(iYear, 4) + Str(Month(dDay), 3) + Str(Day(dDay), 3) + Str(DoW(dDay), 2);
    }

    public static string Quarter162(DateOnly dDay = default)
    {
        return Str((decimal)(Year(dDay)) / 4, 8, 2);
    }

    public static decimal Next162(string cText = default)
    {
        return Len(cText) + 1;
    }

    public static long Both162(string cA = default, string cB = default)
    {
        return Len(cA) + Len(cB);
    }

    public static DateOnly Later162(DateOnly dDay = default, decimal nDays = default)
    {
        return dDay.AddDays((int)(Int(nDays)));
    }
}
