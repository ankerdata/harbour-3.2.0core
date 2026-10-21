// Test 5: FOR with STEP, FOR counting down, FOR EACH DESCEND
public static partial class Program
{
    public static void Main(string[] args)
    {
        long i;
        decimal nSum = 0;
        var aItems = new List<dynamic> { "apple", "banana", "cherry" };

        // FOR with STEP
        for (i = 0; i <= 20; i += 5)
        {
            nSum = nSum + i;
        }

        QOut("nSum after STEP=" + Str(nSum));

        // FOR counting down with negative STEP
        for (i = 10; i >= 1; i += -1)
        {
            nSum = nSum + i;
        }

        QOut("nSum after DOWN=" + Str(nSum));

        // FOR EACH with DESCEND
        foreach (string cItem in HbEnumValues(aItems, true))
        {
            nSum = nSum + Len(cItem);
        }

        QOut("nSum=" + Str(nSum));
    }
}
