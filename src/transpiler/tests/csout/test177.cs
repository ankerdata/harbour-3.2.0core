// Test 177: a whole number earned from another. `j` starts at `i + 1` and
// `k` at `j + 1`: each is a subscript and so a long, but only once the
// one it starts from is known to be whole, which the first reading of the
// routine does not know yet. A local set from a loop variable (`nFound177
// := i`) earns it the same way. One that takes a fraction stays a
// decimal, and so does what is set from it.
public static partial class Program
{
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
    }
}
