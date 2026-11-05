// Test 163: arrays of whole numbers and arrays of arrays, declared. `AS
// ARRAY OF INTEGER` (a type Harbour has not, so the lexer composes it as it
// does a typed hash) is a List<long>, an element a long; `AS ARRAY OF ARRAY
// OF CHARACTER`, sqlite3_get_table()'s rows of text, reads a row as an
// array of strings and a value as a string. On a member, a local, a
// parameter and a FIXED array.
public class Order163 : IHbObject
{
    public List<long> aIds = new List<long>();

    public virtual Order163 Add163(long iId = default)
    {
        AAdd(aIds, iId);

        return this;
    }

    public virtual long Last163()
    {
        long iLast = 0;

        if (Len(aIds) > 0)
        {
            iLast = aIds[I(Len(aIds))];
        }

        return iLast;
    }
}

public static partial class Program
{
    public static void Main(string[] args)
    {
        var oOrder163 = new Order163();
        var aTable = new List<List<string>>
        {
            new List<string> { "nId", "cName" },
            new List<string> { "7", "seven" },
            new List<string> { "9", "nine" }
        };
        var aFixed = new long[] { 3, 4 };

        oOrder163.Add163(5).Add163(6);
        QOut(oOrder163.Last163(), Sum163(oOrder163.aIds), aFixed[I(2)]);
        QOut(Name163(aTable, 2), Upper(aTable[I(3)][I(2)]), Len(aTable[I(2)]));
    }

    public static long Sum163(List<long> aIds = default)
    {
        long iSum = 0;

        for (long i = 1; i <= Len(aIds); i++)
        {
            iSum += aIds[I(i)];
        }

        return iSum;
    }

    public static string Name163(List<List<string>> aRows = default, long iRow = default)
    {
        List<string> aRow = aRows[I(iRow)];

        return aRow[I(2)] + "/" + aRows[I(iRow)][I(1)];
    }
}
