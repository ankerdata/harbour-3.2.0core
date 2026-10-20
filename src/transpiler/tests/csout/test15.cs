// Test 15: Array and hash operations
public static partial class Program
{
    public static void Main(string[] args)
    {
        List<dynamic> aNumbers = new List<dynamic> { 10, 20, 30, 40, 50 };
        List<dynamic> aNames = new List<dynamic> { "Alice", "Bob", "Charlie" };
        OrderedDictionary<string, dynamic> hPerson = new OrderedDictionary<string, dynamic>
        {
            { "name", "John" },
            { "age", 30 }
        };
        OrderedDictionary<string, dynamic> hConfig = new OrderedDictionary<string, dynamic>
        {
            { "debug", true },
            { "timeout", 60 }
        };
        List<dynamic> aMatrix = new List<dynamic>
        {
            new List<dynamic> { 1, 2 },
            new List<dynamic> { 3, 4 },
            new List<dynamic> { 5, 6 }
        };
        List<dynamic> aEmpty = new List<dynamic> {  };
        decimal nTotal = 0;
        decimal nLen;
        decimal nPos;
        long i;
        string cName;
        decimal nItem;
        string cText = "  Hello World  ";
        string cUpper;
        string cTrimmed;
        string cSub;

        // Array subscript access
        nTotal = aNumbers[I(1)] + aNumbers[I(3)] + aNumbers[I(5)];
        QOut("nTotal=" + Str(nTotal));

        // Array subscript assignment
        aNumbers[I(2)] = 99;
        QOut("aNumbers[2]=" + Str(aNumbers[I(2)]));

        // Array length
        nLen = Len(aNumbers);
        QOut("nLen=" + Str(nLen));

        // FOR loop with array subscript
        nTotal = 0;
        for (i = 1; i <= Len(aNumbers); i++)
        {
            nTotal += aNumbers[I(i)];
        }

        QOut("nTotal=" + Str(nTotal));

        // FOR EACH over array
        nTotal = 0;
        foreach (decimal __hb_fe_nItem in HbEnumValues(aNumbers))
        {
            nItem = __hb_fe_nItem;
            nTotal += nItem;
        }

        QOut("nTotal=" + Str(nTotal));

        // Nested array access
        nTotal = aMatrix[I(2)][I(1)];
        QOut("nTotal=" + Str(nTotal));

        // Hash access
        cName = hPerson["name"];
        QOut("cName=" + cName);

        // Hash assignment
        hPerson["age"] = 31;
        QOut("age=" + Str(hPerson["age"]));

        // String operations via mapped functions
        cUpper = Upper(cText);
        QOut("cUpper=" + cUpper);
        cTrimmed = AllTrim(cText);
        QOut("cTrimmed=" + cTrimmed);
        cSub = SubStr(cText, 3, 5);
        QOut("cSub=" + cSub);
        nPos = Len(cText);
        QOut("nPos=" + Str(nPos));
    }
}
