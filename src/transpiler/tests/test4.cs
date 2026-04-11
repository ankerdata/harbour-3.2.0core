using System;

// Test 4: SWITCH/CASE/OTHERWISE/EXIT
public static partial class Program
{
    public static void Main(string[] args)
    {
        decimal nVal = 2;
        string cResult;

        switch (nVal)
        {
            case 1:
                cResult = "one";
                HbRuntime.QOUT("cResult=" + cResult);
                break;
            case 2:
                cResult = "two";
                HbRuntime.QOUT("cResult=" + cResult);
                break;
            default:
                cResult = "other";
                HbRuntime.QOUT("cResult=" + cResult);
                break;
        }

        return;
    }
}
