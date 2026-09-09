using System;
using static HbRuntime;
using static Program;

// Test 89: a function-final RETURN no path reaches, and SWITCH cases
// that already end in a jump.
//
// Harbour requires a FUNCTION to end with RETURN (level-1 warning, and
// the corpus builds with -w3 -es2) even when the statement before it
// never completes: IF/ELSE with every arm returning, DO CASE with an
// OTHERWISE that returns, DO WHILE .T. with no EXIT. C# reports CS0162
// on that RETURN. The emitter skips exactly that one statement, and
// nothing else — FirstOver below keeps its final RETURN because its
// loop has an EXIT.
//
// In a SWITCH, a comment preserved after a case's `return` or `exit`
// used to hide the jump from the emitter, which then added a second
// `break;` — CS0162 again. Comments no longer count.
public static partial class Program
{
    public static void Main(string[] args)
    {
        HbRuntime.QOut("sign=" + SignOf(5) + SignOf(-5) + SignOf(0));
        HbRuntime.QOut("kind=" + Kind(1) + Kind(2) + Kind(9));
        HbRuntime.QOut("count=" + HbRuntime.LTrim(HbRuntime.Str(CountTo(3))));
        HbRuntime.QOut("first=" + HbRuntime.LTrim(HbRuntime.Str(FirstOver(new dynamic[] { 1, 5, 9 }, 4))));
        HbRuntime.QOut("none=" + HbRuntime.LTrim(HbRuntime.Str(FirstOver(new dynamic[] { 1, 2 }, 4))));
        HbRuntime.QOut("name=" + Ordinal(1) + Ordinal(2) + Ordinal(3));
        return;

        /* Every arm returns: the final RETURN is not emitted. */
    }
    public static string SignOf(decimal n = default)
    {
        if (n > 0)
        {
            return "+";
        }
        else if (n < 0)
        {
            return "-";
        }
        else
        {
            return "0";
        }

        /* DO CASE with OTHERWISE, every case returning. */
    }
    public static string Kind(decimal n = default)
    {
        if (n == 1)
        {
            return "one";
        }
        else if (n == 2)
        {
            return "two";
        }
        else
        {
            return "many";
        }

        /* DO WHILE .T. with no EXIT: only RETURN leaves it. */
    }
    public static decimal CountTo(decimal nMax = default)
    {
        decimal n = 0;
        while (true)
        {
            n++;
            if (n >= nMax)
            {
                return n;
            }
        }

        /* Control: a DO WHILE .T. WITH an EXIT falls through, so the final
   RETURN stays — and the second call above returns through it. */
    }
    public static dynamic FirstOver(dynamic[] aList = default, decimal nOver = default)
    {
        long i = 0;
        while (true)
        {
            i++;
            if (i > HbRuntime.Len(aList))
            {
                break;
            }

            if (aList[i - 1] > nOver)
            {
                return aList[i - 1];
            }
        }

        return 0;

        /* SWITCH: `return` followed by a trailing comment, and `exit` followed
   by a commented-out case — neither gets a second break. */
    }
    public static string Ordinal(decimal n = default)
    {
        string cName = "?";

        switch (n)
        {
            case 1:
                // the first
                return "one";
            case 2:
                cName = "two";
                break;
                //CASE 3 ; cName := "three" ; EXIT
            default:
                cName = "many";
                break;
        }

        return cName;
    }
}
