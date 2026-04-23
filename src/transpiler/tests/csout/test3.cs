using System;
using static HbRuntime;
using static Program;

// Test 3: DO CASE, FOR EACH, BEGIN SEQUENCE/RECOVER/ALWAYS, BREAK
public static partial class Program
{
    public static void Main(string[] args)
    {
        decimal nChoice = 2;
        dynamic[] aItems = new dynamic[] { "apple", "banana", "cherry" };
        string cItem = default;
        string cResult = "";
        dynamic oErr = default;

        // DO CASE
        if (nChoice == 1)
        {
            cResult = "first";
            HbRuntime.QOut("cResult=" + cResult);
        }
        else if (nChoice == 2)
        {
            cResult = "second";
            HbRuntime.QOut("cResult=" + cResult);
        }
        else
        {
            cResult = "other";
            HbRuntime.QOut("cResult=" + cResult);
        }

        // FOR EACH
        foreach (dynamic __hb_fe_cItem in aItems)
        {
            cItem = __hb_fe_cItem;
            cResult = cResult + cItem;
        }

        HbRuntime.QOut("cResult=" + cResult);

        // BEGIN SEQUENCE
        try
        {
            cResult = DoSomething();
            HbRuntime.QOut("cResult=" + cResult);
        }
        catch (Exception __hb_rec_oErr)
        {
            oErr = __hb_rec_oErr;
            cResult = "error caught";
            HbRuntime.QOut("cResult=" + cResult);
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
