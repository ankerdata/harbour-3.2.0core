using System;
using static HbRuntime;

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
        decimal i = default;

        for (i = 1; i <= nMax; i++)
        {
            // LOOP = continue
            if (i % 2 != 0)
            {
                continue;
            }

            nSum += i;
        }

        HbRuntime.QOUT("nSum=" + HbRuntime.STR(nSum));

        return nSum;
    }

    public static decimal FindFirst(dynamic[] aItems = default, string cTarget = default)
    {
        string cItem = default;
        decimal nPos = 0;
        decimal i = default;

        // Nested FOR with EXIT
        for (i = 1; i <= HbRuntime.LEN(aItems); i++)
        {
            if (aItems[(int)(i) - 1] == cTarget)
            {
                nPos = i;
                HbRuntime.QOUT("nPos=" + HbRuntime.STR(nPos));
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
                    HbRuntime.QOUT("cResult=" + cResult);
                    break;
                }
                else if (nX > 10)
                {
                    if (nX > 50)
                    {
                        cResult = "large";
                        HbRuntime.QOUT("cResult=" + cResult);
                    }
                    else if (nX > 25)
                    {
                        cResult = "medium";
                        HbRuntime.QOUT("cResult=" + cResult);
                    }
                    else
                    {
                        cResult = "smallish";
                        HbRuntime.QOUT("cResult=" + cResult);
                    }

                    break;
                }
                else
                {
                    nX *= 2;
                    HbRuntime.QOUT("nX=" + HbRuntime.STR(nX));
                }
            }
        }
        else
        {
            cResult = "non-positive";
            HbRuntime.QOUT("cResult=" + cResult);
        }

        return cResult;
    }

    public static void Main(string[] args)
    {
        HbRuntime.QOUT("Classify(-5)=" + Classify(-5));
        HbRuntime.QOUT("Classify(0)=" + Classify(0));
        HbRuntime.QOUT("Classify(42)=" + Classify(42));
        HbRuntime.QOUT("SumEvenTo(10)=" + HbRuntime.STR(SumEvenTo(10)));
        HbRuntime.QOUT("FindFirst=" + HbRuntime.STR(FindFirst(new dynamic[] { "a", "b", "c" }, "b")));
        HbRuntime.QOUT("DeepNest(5)=" + DeepNest(5));
        HbRuntime.QOUT("DeepNest(30)=" + DeepNest(30));
        HbRuntime.QOUT("DeepNest(200)=" + DeepNest(200));

        return;
    }
}
