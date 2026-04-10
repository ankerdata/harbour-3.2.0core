using System;

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
        decimal i;

        for (i = 1; i <= nMax; i++)
        {
            // LOOP = continue
            if (i % 2 != 0)
            {
                continue;
            }

            nSum += i;
        }

        HbRuntime.QOut("nSum=" + HbRuntime.Str(nSum));

        return nSum;
    }

    public static decimal FindFirst(dynamic[] aItems = default, string cTarget = default)
    {
        string cItem;
        decimal nPos = 0;
        decimal i;

        // Nested FOR with EXIT
        for (i = 1; i <= HbRuntime.Len(aItems); i++)
        {
            if (aItems[(int)(i) - 1] == cTarget)
            {
                nPos = i;
                HbRuntime.QOut("nPos=" + HbRuntime.Str(nPos));
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
                    HbRuntime.QOut("cResult=" + cResult);
                    break;
                }
                else if (nX > 10)
                {
                    if (nX > 50)
                    {
                        cResult = "large";
                        HbRuntime.QOut("cResult=" + cResult);
                    }
                    else if (nX > 25)
                    {
                        cResult = "medium";
                        HbRuntime.QOut("cResult=" + cResult);
                    }
                    else
                    {
                        cResult = "smallish";
                        HbRuntime.QOut("cResult=" + cResult);
                    }

                    break;
                }
                else
                {
                    nX *= 2;
                    HbRuntime.QOut("nX=" + HbRuntime.Str(nX));
                }
            }
        }
        else
        {
            cResult = "non-positive";
            HbRuntime.QOut("cResult=" + cResult);
        }

        return cResult;
    }

    public static void Main(string[] args)
    {
        HbRuntime.QOut("Classify(-5)=" + Classify(-5));
        HbRuntime.QOut("Classify(0)=" + Classify(0));
        HbRuntime.QOut("Classify(42)=" + Classify(42));
        HbRuntime.QOut("SumEvenTo(10)=" + HbRuntime.Str(SumEvenTo(10)));
        HbRuntime.QOut("FindFirst=" + HbRuntime.Str(FindFirst(new dynamic[] { "a", "b", "c" }, "b")));
        HbRuntime.QOut("DeepNest(5)=" + DeepNest(5));
        HbRuntime.QOut("DeepNest(30)=" + DeepNest(30));
        HbRuntime.QOut("DeepNest(200)=" + DeepNest(200));

        return;
    }
}
