// Test 167: Array( n ) and `LOCAL a[ n ]` for a typed list. An array name
// (`an`, `ai`, `ac`) or AS ARRAY OF given Array( n ), or declared with a
// dimension, is that list in C#, a List<decimal>, List<long> or
// List<string> of n blanks (zero, or null for a string) where Harbour's
// elements are NIL until filled. A plain name's Array( n ) stays an array
// of NIL.
public static partial class Program
{
    public static void Main(string[] args)
    {
        List<string> acLabels = ArrayOf<string>(3);

        List<decimal> anAmounts = ArrayOf<decimal>(3);
        AFill(anAmounts, 0);
        anAmounts[I(2)] = 1.50m;

        List<long> aiCounts = ArrayOf<long>(2);
        for (long i = 1; i <= Len(aiCounts); i++)
        {
            aiCounts[I(i)] = i * 10;
        }

        acLabels[I(1)] = "one";
        acLabels[I(3)] = "three";

        List<string> aWords = ArrayOf<string>(2);
        aWords[I(2)] = "second";

        List<dynamic> aPlain = Array(2);
        aPlain[I(1)] = "mixed";
        aPlain[I(2)] = 7;

        QOut(Len(anAmounts), anAmounts[I(1)], anAmounts[I(2)]);
        QOut(Len(aiCounts), aiCounts[I(1)], aiCounts[I(2)]);
        QOut(Len(acLabels), acLabels[I(1)], acLabels[I(2)] == null, acLabels[I(3)]);
        QOut(Len(aWords), aWords[I(1)] == null, aWords[I(2)]);
        QOut(Len(aPlain), aPlain[I(1)], aPlain[I(2)]);
    }
}
