// Test 15: Array and hash operations
public static partial class Program
{
    public static void Main(string[] args)
    {
        var aNumbers = new List<dynamic> { 10, 20, 30, 40, 50 };
        var aNames = new List<dynamic> { "Alice", "Bob", "Charlie" };
        var hPerson = new OrderedDictionary<string, dynamic>
        {
            { "name", "John" },
            { "age", 30 }
        };
        var hConfig = new OrderedDictionary<string, dynamic>
        {
            { "debug", true },
            { "timeout", 60 }
        };
        var aMatrix = new List<dynamic>
        {
            new List<dynamic> { 1, 2 },
            new List<dynamic> { 3, 4 },
            new List<dynamic> { 5, 6 }
        };
        var aEmpty = new List<dynamic>();
        string cText = "  Hello World  ";

        // Array subscript access
        decimal nTotal = aNumbers[I(1)] + aNumbers[I(3)] + aNumbers[I(5)];
        QOut("nTotal=" + Str(nTotal));

        // Array subscript assignment
        aNumbers[I(2)] = 99;
        QOut("aNumbers[2]=" + Str(aNumbers[I(2)]));

        // Array length
        decimal nLen = Len(aNumbers);
        QOut("nLen=" + Str(nLen));

        // FOR loop with array subscript
        nTotal = 0;
        for (long i = 1; i <= Len(aNumbers); i++)
        {
            nTotal += aNumbers[I(i)];
        }

        QOut("nTotal=" + Str(nTotal));

        // FOR EACH over array
        nTotal = 0;
        foreach (decimal nItem in HbEnumValues(aNumbers))
        {
            nTotal += nItem;
        }

        QOut("nTotal=" + Str(nTotal));

        // Nested array access
        nTotal = aMatrix[I(2)][I(1)];
        QOut("nTotal=" + Str(nTotal));

        // Hash access
        string cName = hPerson["name"];
        QOut("cName=" + cName);

        // Hash assignment
        hPerson["age"] = 31;
        QOut("age=" + Str(hPerson["age"]));

        // String operations via mapped functions
        string cUpper = Upper(cText);
        QOut("cUpper=" + cUpper);
        string cTrimmed = AllTrim(cText);
        QOut("cTrimmed=" + cTrimmed);
        string cSub = SubStr(cText, 3, 5);
        QOut("cSub=" + cSub);
        decimal nPos = Len(cText);
        QOut("nPos=" + Str(nPos));
    }
}
