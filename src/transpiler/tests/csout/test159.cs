// Test 159: a LOCAL with no initialiser is `T name;` where C# sees it
// assigned before every read, and keeps `= default` (NIL) where a read may
// come first.
//
// Assigned first, both branches of an IF, every CASE with an OTHERWISE, a
// FOR's counter, a codeblock created after the assignment: no default.
// An IF without ELSE, a loop body's assignment read after the loop, a
// SEQUENCE body's read after END, `@x` (C# `ref` needs it assigned) and
// an assignment in the right operand of .AND.: `= default`.
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
        string cMaybe = default;

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
        dynamic xLast = default;

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
        string cDone = default;
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
        dynamic xValue = default;

        Bump159(ref xValue);

        return Str(xValue, 2);
    }

    public static void Bump159(ref dynamic xCount)
    {
        xCount = (xCount == null ? 1 : xCount + 1);
    }

    public static string AndRight159(bool lGo = default)
    {
        string cSet = default;

        if (lGo && (cSet = "set") == "set")
        {
            return cSet;
        }

        return "no";
    }
}
