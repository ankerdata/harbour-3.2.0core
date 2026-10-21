// Test 161: a LOCAL is declared in the innermost block that holds all its
// uses, just before the first, where the C# runs the same; joined to its
// first assignment; a FOR's counter in the loop's header; a FOR EACH
// variable as the foreach's own.
//
// Moved: a constant-initialised local first read in a branch; one first
// assigned there (one declaration, `var` for a `new` of its type); one
// first assigned in a loop's body; one used in both branches of an inner
// IF (before that IF); a counter nothing after its loop reads; a FOR EACH
// variable the body only reads (the foreach's own) and one it assigns
// (declared from the element); a RECOVER USING variable (before the try);
// one first assigned at the routine's own level (declared there); one in
// a routine that reads `MEMVAR->x`, which names no LOCAL.
// At the top: a local a loop reads before assigning (Harbour keeps the
// last pass's value), one a codeblock made in a loop uses (each pass's
// block sees the one variable), one initialised by a call, a counter read
// after its loop.
public static partial class Program
{
    public static dynamic cShared161;
    public static void Main(string[] args)
    {
        QOut(Branch161(true), Branch161(false));
        QOut(Loop161(3));
        QOut(Counter161(4));
        QOut(Each161(new List<dynamic> { "a", "bb", "ccc" }));
        QOut(Kept161(3));
        QOut(Recover161(true), Recover161(false));
        QOut(Top161());
        QOut(Alias161(true), Alias161(false));
    }

    public static string Branch161(bool lGo = default)
    {
        string cOut = "";

        if (lGo)
        {
            decimal nLimit = 10;
            cOut += Str(nLimit, 3);
            string cName = "go";
            var aList = new List<dynamic>();
            AAdd(aList, cName);
            cOut += cName + Str(Len(aList), 2);
        }

        if (Len(cOut) >= 0)
        {
            string cBoth;
            if (lGo)
            {
                cBoth = " then";
            }
            else
            {
                cBoth = " else";
            }

            cOut += cBoth;
        }

        return cOut;
    }

    public static decimal Loop161(decimal nTimes = default)
    {
        decimal nLast = 0;
        decimal nSum = 0;

        for (long i = 1; i <= nTimes; i++)
        {
            decimal nSquare = i * i;
            nSum += nSquare;
            if (i > 1)
            {
                nSum += nLast;
            }

            nLast = i;
        }

        return nSum;
    }

    public static string Counter161(decimal n = default)
    {
        long j;
        string cOut = "";

        for (long i = 1; i <= n; i++)
        {
            cOut += Str(i, 1);
        }

        for (j = 1; j <= n; j += 2)
        {
            cOut += ".";
        }

        return cOut + Str(j, 2);
    }

    public static string Each161(List<dynamic> aItems = default)
    {
        string cOut = "";

        foreach (string cItem in HbEnumValues(aItems))
        {
            cOut += cItem;
        }

        foreach (string __hb_fe_cWord in HbEnumValues(aItems))
        {
            string cWord = __hb_fe_cWord;
            cWord = Upper(cWord);
            cOut += cWord;
        }

        return cOut;
    }

    public static string Kept161(decimal n = default)
    {
        decimal nStart = Len("abc");
        decimal nShared;
        var aBlocks = new List<dynamic>();
        string cOut = "";

        for (long k = 1; k <= n; k++)
        {
            nShared = k;
            AAdd(aBlocks, ((Func<decimal>)(() => nShared)));
        }

        if (n > 0)
        {
            cOut += Str(nStart, 2);
        }

        AEval(aBlocks, ((Func<dynamic, dynamic>)((b) => cOut += Str(Eval(b), 2))));

        return cOut;
    }

    public static string Recover161(bool lFail = default)
    {
        string cOut = "none";

        if (lFail)
        {
            dynamic xValue;
            try
            {
                throw new HbBreak("boom");
            }
            catch (HbBreak __hb_brk1)
            {
                xValue = __hb_brk1.Value;
                cOut = xValue;
            }
        }

        return cOut;
    }

    public static string Top161()
    {
        string cHead = "top";
        decimal nCount = Len(cHead);

        return cHead + Str(nCount, 2);
    }

    public static string Alias161(bool lGo = default)
    {
        string cOut = "-";

        cShared161 = "pub";

        if (lGo)
        {
            string cName = cShared161;
            cOut += cName;
        }

        return cOut;
    }
}
