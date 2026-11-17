// Test 177: a whole number earned from another. `j` starts at `i + 1` and
// `k` at `j + 1`: each is a subscript and so a long, but only once the
// one it starts from is known to be whole, which the first reading of the
// routine does not know yet. A local set from a loop variable (`nFound177
// := i`) earns it the same way. One that takes a fraction stays a
// decimal, and so does what is set from it. A SWITCH subject is whole
// when its case labels are, and a loop variable set from a whole file
// static (`siStart177 + 1`) is whole too, also where the routine writes
// the variable back into the static (Resume177()).
public static partial class Program
{
    public static long test177_siStart177 = 1;
    public static long test177_siUnset177;
    public static void Main(string[] args)
    {
        var aRows = new List<dynamic> { "a", "b", "c", "d" };
        long nFound177 = 0;

        for (long i = 1; i <= Len(aRows); i++)
        {
            for (long j = i + 1; j <= Len(aRows); j++)
            {
                for (long k = j + 1; k <= Len(aRows); k++)
                {
                    QOut(aRows[I(i)] + aRows[I(j)] + aRows[I(k)]);
                }
            }

            if (aRows[I(i)] == "c")
            {
                nFound177 = i;
            }
        }

        QOut(aRows[I(nFound177)], nFound177);

        decimal nHalf177 = ((decimal)(Len(aRows)) / 8);
        decimal nNext177 = nHalf177 + 1;
        QOut(nHalf177 < 1, nNext177 > 1);

        for (long m = 1; m <= Test177PrgConst.KIND177_TWO; m++)
        {
            switch (m)
            {
                case Test177PrgConst.KIND177_ONE:
                    QOut("one", aRows[I(m)]);
                    break;
                case Test177PrgConst.KIND177_TWO:
                    QOut("two", aRows[I(m)]);
                    break;
            }
        }

        long n = test177_siStart177 + 1;
        QOut(aRows[I(n)]);
        test177_siUnset177 = 0;
        n = test177_siUnset177 + 2;
        QOut(aRows[I(n)]);
        QOut(Resume177(false), Resume177(true), test177_siUnset177);
    }

    public static string Resume177(bool lResume = default)
    {
        var aLines = new List<dynamic> { "x", "y", "z" };
        long i;
        string cOut = "";

        if (lResume)
        {
            i = test177_siUnset177 + 1;
        }
        else
        {
            i = 1;
        }

        while (true)
        {
            if (i <= Len(aLines) && !Empty(aLines[I(i)]))
            {
                cOut += aLines[I(i)];
                test177_siUnset177 = i;
            }
            else if (i > Len(aLines))
            {
                break;
            }

            i++;
        }

        return cOut;
    }
}
