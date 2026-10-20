// Test 13: Nested control flow, multiple returns, LOOP
public static partial class Program
{
    public static string Classify(decimal nVal = default)
    {
        // Multiple return paths
        if (nVal < 0)
        {
            return "negative";
        }
        else if (nVal == 0)
        {
            return "zero";
        }

        return "positive";
    }

    public static decimal SumEvenTo(decimal nMax = default)
    {
        decimal nSum = 0;
        long i = default;

        for (i = 1; i <= nMax; i++)
        {
            // LOOP = continue
            if (i % 2 != 0)
            {
                continue;
            }

            nSum += i;
        }

        QOut("nSum=" + Str(nSum));

        return nSum;
    }

    public static decimal FindFirst(List<dynamic> aItems = default, string cTarget = default)
    {
        string cItem = default;
        decimal nPos = 0;
        long i = default;

        // Nested FOR with EXIT
        for (i = 1; i <= Len(aItems); i++)
        {
            if (aItems[I(i)] == cTarget)
            {
                nPos = i;
                QOut("nPos=" + Str(nPos));
                break;
            }
        }

        return nPos;
    }

    public static string DeepNest(decimal nX = default)
    {
        string cResult = "";

        // Nested IF inside DO WHILE inside IF
        if (nX > 0)
        {
            while (nX > 0)
            {
                if (nX > 100)
                {
                    cResult = "huge";
                    QOut("cResult=" + cResult);
                    break;
                }
                else if (nX > 10)
                {
                    if (nX > 50)
                    {
                        cResult = "large";
                        QOut("cResult=" + cResult);
                    }
                    else if (nX > 25)
                    {
                        cResult = "medium";
                        QOut("cResult=" + cResult);
                    }
                    else
                    {
                        cResult = "smallish";
                        QOut("cResult=" + cResult);
                    }

                    break;
                }
                else
                {
                    nX *= 2;
                    QOut("nX=" + Str(nX));
                }
            }
        }
        else
        {
            cResult = "non-positive";
            QOut("cResult=" + cResult);
        }

        return cResult;
    }

    public static void Main(string[] args)
    {
        QOut("Classify(-5)=" + Classify(-5));
        QOut("Classify(0)=" + Classify(0));
        QOut("Classify(42)=" + Classify(42));
        QOut("SumEvenTo(10)=" + Str(SumEvenTo(10)));
        QOut("FindFirst=" + Str(FindFirst(new List<dynamic> { "a", "b", "c" }, "b")));
        QOut("DeepNest(5)=" + DeepNest(5));
        QOut("DeepNest(30)=" + DeepNest(30));
        QOut("DeepNest(200)=" + DeepNest(200));
    }
}
