using System;
using static HbRuntime;
using static Program;

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
        Dictionary<string, dynamic> hData = new Dictionary<string, dynamic> { { "a", 1 }, { "b", 2 }, { "c", 3 } };
        // note the duplicate 1
        dynamic[] aNums = new dynamic[] { 3, 1, 2, 1 };
        long i = default;
        string cOut = default;

        HbRuntime.QOut("round_2.5=" + HbRuntime.LTrim(HbRuntime.Str(HbRuntime.Round(2.5m, 0))));
        HbRuntime.QOut("round_0.5=" + HbRuntime.LTrim(HbRuntime.Str(HbRuntime.Round(0.5m, 0))));
        HbRuntime.QOut("round_neg=" + HbRuntime.LTrim(HbRuntime.Str(HbRuntime.Round(-2.5m, 0))));

        HbRuntime.QOut("len_hash=" + HbRuntime.LTrim(HbRuntime.Str(HbRuntime.Len(hData))));

        HbRuntime.ASort(aNums, ((Func<dynamic, dynamic, dynamic>)((x, y) => x < y)));
        cOut = "";
        for (i = 1; i <= HbRuntime.Len(aNums); i++)
        {
            cOut += HbRuntime.LTrim(HbRuntime.Str(aNums[i - 1]));
        }

        HbRuntime.QOut("sorted=" + cOut);

        HbRuntime.QOut("val_abc=" + HbRuntime.LTrim(HbRuntime.Str(HbRuntime.Val("12abc"))));
        // compared, not Str()'d — Str() of a fraction ignores SET DECIMALS
        HbRuntime.QOut("val_dec=" + (HbRuntime.Val("3.5x") == 3.5m ? "Y" : "N"));
        HbRuntime.QOut("val_neg=" + HbRuntime.LTrim(HbRuntime.Str(HbRuntime.Val("-7kg"))));
        return;
    }
}
