// Test 165: a generic function. `AS ARRAY OF T`, T a single capital
// letter, is a type parameter: Join165() is `List<T> Join165<T>( List<T>,
// List<T> )`, so one function joins whole numbers, strings, rows of strings
// and plain arrays, and C# checks each call. A literal argument, an empty
// one too, is the list another argument makes T; so is the result.
public static partial class Program
{
    public static void Main(string[] args)
    {
        var aIds = new List<long> { 1, 2 };
        var aNames = new List<string> { "one" };
        var aRows = new List<List<string>>
        {
            new List<string> { "nId", "cName" }
        };
        var aMixed = new List<dynamic> { 1, "two" };
        long iSum = 0;

        Join165(aIds, new List<long> { 3, 4 });
        Join165(aNames, Words165());
        Join165(aRows, new List<List<string>>
        {
            new List<string> { "7", "seven" }
        });
        Join165(aMixed, new List<dynamic> { true, 4.5m });
        List<long> aAll = Join165(aIds, new List<long> { 5 });
        foreach (long iId in HbEnumValues(aAll))
        {
            iSum += iId;
        }

        List<string> aCopy = Join165(new List<string>(), aNames);
        QOut(Len(aIds), iSum, aNames[I(3)], Upper(aRows[I(2)][I(2)]), Len(aMixed), ValType(aMixed[I(4)]));
        QOut(Len(aCopy), Upper(aCopy[I(2)]));
    }

    public static List<T> Join165<T>(List<T> aInto = default, List<T> aFrom = default)
    {
        for (long i = 1; i <= Len(aFrom); i++)
        {
            AAdd(aInto, aFrom[I(i)]);
        }

        return aInto;
    }

    public static List<string> Words165()
    {
        return new List<string> { "two", "three" };
    }
}
