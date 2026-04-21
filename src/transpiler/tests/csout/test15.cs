using System;
using static HbRuntime;
using static Program;

// Test 15: Array and hash operations
public static partial class Program
{
    public static void Main(string[] args)
    {
        dynamic[] aNumbers = new dynamic[] { 10, 20, 30, 40, 50 };
        dynamic[] aNames = new dynamic[] { "Alice", "Bob", "Charlie" };
        Dictionary<dynamic, dynamic> hPerson = new Dictionary<dynamic, dynamic> { { "name", "John" }, { "age", 30 } };
        Dictionary<dynamic, dynamic> hConfig = new Dictionary<dynamic, dynamic> { { "debug", true }, { "timeout", 60 } };
        dynamic[] aMatrix = new dynamic[] { new dynamic[] { 1, 2 }, new dynamic[] { 3, 4 }, new dynamic[] { 5, 6 } };
        dynamic[] aEmpty = new dynamic[] {  };
        decimal nTotal = 0;
        decimal nLen = default;
        decimal nPos = default;
        decimal i = default;
        string cName = default;
        string cText = "  Hello World  ";
        string cUpper = default;
        string cTrimmed = default;
        string cSub = default;

        // Array subscript access
        nTotal = aNumbers[0] + aNumbers[2] + aNumbers[4];
        HbRuntime.QOut("nTotal=" + HbRuntime.Str(nTotal));

        // Array subscript assignment
        aNumbers[1] = 99;
        HbRuntime.QOut("aNumbers[2]=" + HbRuntime.Str(aNumbers[1]));

        // Array length
        nLen = HbRuntime.Len(aNumbers);
        HbRuntime.QOut("nLen=" + HbRuntime.Str(nLen));

        // FOR loop with array subscript
        nTotal = 0;
        for (i = 1; i <= HbRuntime.Len(aNumbers); i++)
        {
            nTotal += aNumbers[(int)(i) - 1];
        }

        HbRuntime.QOut("nTotal=" + HbRuntime.Str(nTotal));

        // FOR EACH over array
        nTotal = 0;
        foreach (dynamic nItem in aNumbers)
        {
            nTotal += nItem;
        }

        HbRuntime.QOut("nTotal=" + HbRuntime.Str(nTotal));

        // Nested array access
        nTotal = aMatrix[1][0];
        HbRuntime.QOut("nTotal=" + HbRuntime.Str(nTotal));

        // Hash access
        cName = hPerson["name"];
        HbRuntime.QOut("cName=" + cName);

        // Hash assignment
        hPerson["age"] = 31;
        HbRuntime.QOut("age=" + HbRuntime.Str(hPerson["age"]));

        // String operations via mapped functions
        cUpper = HbRuntime.Upper(cText);
        HbRuntime.QOut("cUpper=" + cUpper);
        cTrimmed = HbRuntime.AllTrim(cText);
        HbRuntime.QOut("cTrimmed=" + cTrimmed);
        cSub = HbRuntime.SubStr(cText, 3, 5);
        HbRuntime.QOut("cSub=" + cSub);
        nPos = HbRuntime.Len(cText);
        HbRuntime.QOut("nPos=" + HbRuntime.Str(nPos));

        return;
    }
}
