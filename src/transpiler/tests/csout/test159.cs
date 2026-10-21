// Test 159: a LOCAL with no initialiser is `T name;` where C# sees it
// assigned before every read, and keeps `= default` (NIL) where a read may
// come first.
//
// Assigned first, both branches of an IF, every CASE with an OTHERWISE, a
// FOR's counter, a codeblock created after the assignment: no default.
// An IF without ELSE, a loop body's assignment read after the loop, a
// SEQUENCE body's read after END, `@x` (C# `ref` needs it assigned) and
// an assignment in the right operand of .AND.: a default. The default is
// the type's value written out: 0, false, null, and a date's `default`.
public static partial class Program
{
    public static void Main(string[] args)
    {
        QOut(Assigned159(true), Assigned159(false));
        QOut(Branches159(1), Branches159(2), Branches159(3));
        QOut(Loops159(3), Loops159(0));
        QOut(Blocks159());
        QOut(Sequence159());
        QOut(ByRef159());
        QOut(AndRight159(true), AndRight159(false));
        QOut(Defaults159(true), Defaults159(false));
    }

    public static string Assigned159(bool lFlag = default)
    {
        string cName;
        decimal nCount;

        cName = "first";
        nCount = Len(cName);
        if (lFlag)
        {
            cName += "!";
        }

        return cName + Str(nCount, 2);
    }

    public static string Branches159(decimal nKind = default)
    {
        string cBoth;
        string cCase;
        string cMaybe = null;

        if (nKind == 1)
        {
            cBoth = "one";
        }
        else
        {
            cBoth = "other";
        }

        if (nKind == 1)
        {
            cCase = "a";
        }
        else if (nKind == 2)
        {
            cCase = "b";
        }
        else
        {
            cCase = "c";
        }

        if (nKind == 3)
        {
            cMaybe = "three";
        }

        return cBoth + cCase + (cMaybe == null ? "-" : cMaybe);
    }

    public static string Loops159(decimal nTimes = default)
    {
        long i;
        decimal nSum;
        dynamic xLast = null;

        nSum = 0;
        for (i = 1; i <= nTimes; i++)
        {
            xLast = i;
            nSum += xLast;
        }

        return Str(nSum, 3) + (xLast == null ? " none" : Str(xLast, 3));
    }

    public static string Blocks159()
    {
        decimal nBase;
        dynamic bAdd;

        nBase = 10;
        bAdd = ((Func<dynamic, dynamic>)((n) => n + nBase));

        return Str(Eval(bAdd, 5), 3);
    }

    public static string Sequence159()
    {
        string cDone = null;
        string cAfter;

        try
        {
            cDone = "body";
        }
        catch (HbBreak)
        {
        }
        cAfter = "after";

        return (cDone == null ? "nil" : cDone) + " " + cAfter;
    }

    public static string ByRef159()
    {
        dynamic xValue = null;

        Bump159(ref xValue);

        return Str(xValue, 2);
    }

    public static void Bump159(ref dynamic xCount)
    {
        xCount = (xCount == null ? 1 : xCount + 1);
    }

    public static string AndRight159(bool lGo = default)
    {
        string cSet = null;

        if (lGo && (cSet = "set") == "set")
        {
            return cSet;
        }

        return "no";
    }

    public static string Defaults159(bool lSet = default)
    {
        decimal nCount = 0;
        long iTimes = 0;
        bool lSeen = false;
        DateOnly dWhen = default;
        string cName = null;
        List<dynamic> aRows = null;
        OrderedDictionary<string, dynamic> hMap = null;

        if (lSet)
        {
            nCount = 1;
            iTimes = 2;
            lSeen = true;
            dWhen = Date();
            cName = "set";
            aRows = new List<dynamic> { 1 };
            hMap = new OrderedDictionary<string, dynamic> { { "k", 1 } };
        }

        return (Empty(nCount) ? "e" : "s") + (Empty(iTimes) ? "e" : "s") + (Empty(lSeen) ? "e" : "s") + (Empty(dWhen) ? "e" : "s") + (Empty(cName) ? "e" : "s") + (Empty(aRows) ? "e" : "s") + (Empty(hMap) ? "e" : "s");
    }
}
