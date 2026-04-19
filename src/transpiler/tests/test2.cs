using System;
using static HbRuntime;
using static Program;

// Test 2: FUNCTION, FOR/NEXT, IF/ELSEIF/ELSE, DO WHILE, EXIT
public static partial class Program
{
    public static void Main(string[] args)
    {
        decimal nSum = 0;
        decimal i = default;
        string cResult = default;

        for (i = 1; i <= 10; i++)
        {
            nSum = nSum + i;
        }

        HbRuntime.QOUT("nSum=" + HbRuntime.STR(nSum));

        if (nSum > 50)
        {
            cResult = "big";
            HbRuntime.QOUT("cResult=" + cResult);
        }
        else if (nSum > 20)
        {
            cResult = "medium";
            HbRuntime.QOUT("cResult=" + cResult);
        }
        else
        {
            cResult = "small";
            HbRuntime.QOUT("cResult=" + cResult);
        }

        while (nSum > 0)
        {
            nSum = nSum - 10;
            if (nSum < 0)
            {
                break;
            }
        }

        HbRuntime.QOUT("nSum=" + HbRuntime.STR(nSum));

        return;
    }
}
