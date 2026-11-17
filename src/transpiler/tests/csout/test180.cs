// Test 180: a function pointer declared `AS FUNCTION Name` is a delegate of
// Name's signature: a parameter and a local. `@Other()` given to it is a
// lambda that calls Other, `f:exec( ... )` is a direct call, and a
// parameter the call leaves out is passed as its blank, a delegate having
// no optional parameters. A pointer with no declaration is the untyped
// one, called through Eval().
public static partial class Program
{
    public static void Main(string[] args)
    {
        dynamic fAny = FuncPtr(nameof(Triple180));

        QOut(Apply180((a1, a2) => Program.Double180(a1, a2), 4), Apply180((a1, a2) => Program.Triple180(a1, a2), 4));
        Func<long, string, string> fPick = (a1, a2) => Program.Triple180(a1, a2);
        QOut(fPick(5, "x"), fPick(6, default));
        fPick = (a1, a2) => Program.Double180(a1, a2);
        QOut(fPick(7, "y"), Eval(fAny, 8, "z"));
        Each180((a1) => Program.Show180(a1), new List<string> { "a", "b" });
    }

    public static string Double180(long iValue = default, string cTag = default)
    {
        return LTrim(Str(iValue * 2)) + (Empty(cTag) ? "" : cTag);
    }

    public static string Triple180(long iValue = default, string cTag = default)
    {
        return LTrim(Str(iValue * 3)) + (Empty(cTag) ? "" : cTag);
    }

    public static string Apply180(Func<long, string, string> fCalc = default, long iValue = default)
    {
        return fCalc(iValue, "!");
    }

    public static void Show180(string cText = default)
    {
        QOut("show:", cText);
    }

    public static void Each180(Action<string> fShow = default, List<string> acTexts = default)
    {
        foreach (string cText in HbEnumValues(acTexts))
        {
            fShow(cText);
        }
    }
}
