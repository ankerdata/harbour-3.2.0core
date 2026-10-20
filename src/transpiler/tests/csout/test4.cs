using System;
using static HbRuntime;
using static Program;

// Test 4: SWITCH/CASE/OTHERWISE/EXIT
public static partial class Program
{
    public static void Main(string[] args)
    {
        decimal nVal = 2;
        string cResult = default;

        switch (nVal)
        {
            case 1:
                cResult = "one";
                QOut("cResult=" + cResult);
                break;
            case 2:
                cResult = "two";
                QOut("cResult=" + cResult);
                break;
            default:
                cResult = "other";
                QOut("cResult=" + cResult);
                break;
        }

        return;
    }
}
