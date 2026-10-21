// Test 60: HbRuntime correctness fixes.
//
//   Round  — half-away-from-zero, not C#'s banker's rounding.
//            Round(2.5,0) is 3 and Round(-2.5,0) is -3 (banker's
//            would give 2 / -2); Round(0.5,0) is 1 (banker's: 0).
//   Len    — a hash's length is its key count, not 0.
//   ASort  — a sort block that ties on equal elements must not
//            produce an inconsistent comparer (Array.Sort throws).
//   Val    — parses the leading numeric run, ignoring trailing text.
public static partial class Program
{
    public static void Main(string[] args)
    {
        var hData = new OrderedDictionary<string, dynamic>
        {
            { "a", 1 },
            { "b", 2 },
            { "c", 3 }
        };
        // note the duplicate 1
        var aNums = new List<dynamic> { 3, 1, 2, 1 };

        QOut("round_2.5=" + LTrim(Str(Round(2.5m, 0))));
        QOut("round_0.5=" + LTrim(Str(Round(0.5m, 0))));
        QOut("round_neg=" + LTrim(Str(Round(-2.5m, 0))));

        QOut("len_hash=" + LTrim(Str(Len(hData))));

        ASort(aNums, null, null, ((Func<dynamic, dynamic, bool>)((x, y) => x < y)));
        string cOut = "";
        for (long i = 1; i <= Len(aNums); i++)
        {
            cOut += LTrim(Str(aNums[I(i)]));
        }

        QOut("sorted=" + cOut);

        QOut("val_abc=" + LTrim(Str(Val("12abc"))));
        // compared, not Str()'d — Str() of a fraction ignores SET DECIMALS
        QOut("val_dec=" + (Val("3.5x") == 3.5m ? "Y" : "N"));
        QOut("val_neg=" + LTrim(Str(Val("-7kg"))));
    }
}
