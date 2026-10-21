// Test 3: DO CASE, FOR EACH, BEGIN SEQUENCE/RECOVER/ALWAYS, BREAK
public static partial class Program
{
    public static void Main(string[] args)
    {
        decimal nChoice = 2;
        var aItems = new List<dynamic> { "apple", "banana", "cherry" };
        string cResult = "";
        dynamic oErr = null;

        // DO CASE
        if (nChoice == 1)
        {
            cResult = "first";
            QOut("cResult=" + cResult);
        }
        else if (nChoice == 2)
        {
            cResult = "second";
            QOut("cResult=" + cResult);
        }
        else
        {
            cResult = "other";
            QOut("cResult=" + cResult);
        }

        // FOR EACH
        foreach (string cItem in HbEnumValues(aItems))
        {
            cResult = cResult + cItem;
        }

        QOut("cResult=" + cResult);

        // BEGIN SEQUENCE
        try
        {
            cResult = DoSomething();
            QOut("cResult=" + cResult);
        }
        catch (HbBreak __hb_brk1)
        {
            oErr = __hb_brk1.Value;
            cResult = "error caught";
            QOut("cResult=" + cResult);
        }
        finally
        {
            CleanUp();
        }
    }

    public static dynamic DoSomething()
    {
        throw new HbBreak();
    }

    public static dynamic CleanUp()
    {
        return null;
    }
}
