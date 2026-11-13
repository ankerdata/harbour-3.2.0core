// Test 172: a function the program defines keeps its own return type.
//
// hbfuncs.tab types the functions the transpiler knows from outside the
// program, hbnf's FT_Elapsed() among them: an array. A program that
// defines FT_Elapsed() itself calls its own (test108), and its own return
// type is the call's: here a string and, through a second function, a
// whole number, where the table's ARRAY made the caller's local a list.
public static partial class Program
{
    public static void Main(string[] args)
    {
        string cSpan = FT_Elapsed(3, 5);

        QOut(Upper(cSpan));
        QOut(Len(FT_Elapsed(1, 12)));
        QOut(SpanDays172(3, 5) + 1);
    }

    public static string FT_Elapsed(long iFrom = default, long iTo = default)
    {
        return hb_ntos(iTo - iFrom) + " days";
    }

    public static decimal SpanDays172(long iFrom = default, long iTo = default)
    {
        return Val(FT_Elapsed(iFrom, iTo));
    }
}
