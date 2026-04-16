using System;
using static HbRuntime;

// Test 3: DO CASE, FOR EACH, BEGIN SEQUENCE/RECOVER/ALWAYS, BREAK
public static partial class Program
{
    public static void Main(string[] args)
    {
        decimal nChoice = 2;
        dynamic[] aItems = new dynamic[] { "apple", "banana", "cherry" };
        string cResult = "";

        // DO CASE
        if (nChoice == 1)
        {
            cResult = "first";
            HbRuntime.QOUT("cResult=" + cResult);
        }
        else if (nChoice == 2)
        {
            cResult = "second";
            HbRuntime.QOUT("cResult=" + cResult);
        }
        else
        {
            cResult = "other";
            HbRuntime.QOUT("cResult=" + cResult);
        }

        // FOR EACH
        foreach (dynamic cItem in aItems)
        {
            cResult = cResult + cItem;
        }

        HbRuntime.QOUT("cResult=" + cResult);

        // BEGIN SEQUENCE
        try
        {
            cResult = DoSomething();
            HbRuntime.QOUT("cResult=" + cResult);
        }
        catch (Exception oErr)
        {
            cResult = "error caught";
            HbRuntime.QOUT("cResult=" + cResult);
        }
        finally
        {
            CleanUp();
        }

        return;
    }

    public static dynamic DoSomething()
    {
        throw new Exception();

        return null;
    }

    public static dynamic CleanUp()
    {
        return null;
    }
}
