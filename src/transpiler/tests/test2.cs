using System;

// Test 2: FUNCTION, FOR/NEXT, IF/ELSEIF/ELSE, DO WHILE, EXIT
public static class Program
{
    public static void Main(string[] args)
    {
        decimal nSum = 0;
        decimal i;
        string cResult;

        for (i = 1; i <= 10; i++)
        {
            nSum += i;
        }

        HbRuntime.QOut("nSum=" + HbRuntime.Str(nSum));

        if (nSum > 50)
        {
            cResult = "big";
            HbRuntime.QOut("cResult=" + cResult);
        }
        else if (nSum > 20)
        {
            cResult = "medium";
            HbRuntime.QOut("cResult=" + cResult);
        }
        else
        {
            cResult = "small";
            HbRuntime.QOut("cResult=" + cResult);
        }

        while (nSum > 0)
        {
            nSum -= 10;
            if (nSum < 0)
            {
                break;
            }
        }

        HbRuntime.QOut("nSum=" + HbRuntime.Str(nSum));

        return;
    }
}
