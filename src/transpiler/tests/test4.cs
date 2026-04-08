using System;

// Test 4: SWITCH/CASE/OTHERWISE/EXIT
public static class Program
{
    public static void Main(string[] args)
    {
        decimal nVal = 2;
        string cResult;

        switch (nVal)
        {
            case 1:
                cResult = "one";
                HbRuntime.QOut("cResult=" + cResult);
                break;
            case 2:
                cResult = "two";
                HbRuntime.QOut("cResult=" + cResult);
                break;
            default:
                cResult = "other";
                HbRuntime.QOut("cResult=" + cResult);
                break;
        }

        return;
    }
}
