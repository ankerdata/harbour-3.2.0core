using System;

// Test 5: FOR with STEP, FOR counting down, FOR EACH DESCEND
public static partial class Program
{
    public static void Main(string[] args)
    {
        decimal i;
        decimal nSum = 0;
        dynamic[] aItems = new dynamic[] { "apple", "banana", "cherry" };

        // FOR with STEP
        for (i = 0; i <= 20; i += 5)
        {
            nSum += i;
        }

        HbRuntime.QOut("nSum after STEP=" + HbRuntime.Str(nSum));

        // FOR counting down with negative STEP
        for (i = 10; i >= 1; i += -1)
        {
            nSum += i;
        }

        HbRuntime.QOut("nSum after DOWN=" + HbRuntime.Str(nSum));

        // FOR EACH with DESCEND
        foreach (dynamic cItem in aItems.Reverse())
        {
            nSum += HbRuntime.Len(cItem);
        }

        HbRuntime.QOut("nSum=" + HbRuntime.Str(nSum));

        return;
    }
}
