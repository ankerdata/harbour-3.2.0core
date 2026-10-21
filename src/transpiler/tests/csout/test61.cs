// Test 61: more HbRuntime correctness fixes.
//
//   AScan  — the codeblock form: scan evaluating a block per element.
//   AClone — duplicates nested arrays recursively, not shallowly.
//   Empty  — an empty hash is Empty().
//   Right  — a non-positive count yields "" (not an exception).
//   StrZero— a negative number keeps its sign: StrZero(-5,4) -> "-005".
//   DToC   — honours SET DATEFORMAT.
//
// Not covered here (no Harbour program shows them):
// GETMEMBER/SETMEMBER reaching the dynamic bag, and hb_mutexLock's
// timeout — both fixed in HbRuntime alongside these.
public static partial class Program
{
    public static void Main(string[] args)
    {
        var aData = new List<dynamic> { 10, 20, 30 };
        var aNested = new List<dynamic>
        {
            1,
            new List<dynamic> { 2, 3 }
        };
        var hEmpty = new OrderedDictionary<string, dynamic>();

        // AScan with a codeblock — 20 is at index 2
        QOut("ascan_blk=" + LTrim(Str(AScan(aData, ((Func<dynamic, bool>)((x) => x == 20))))));

        // AClone must deep-copy the nested array
        List<dynamic> aCopy = AClone(aNested);
        // must not touch the original
        aCopy[I(2)][I(1)] = 99;
        // still 2
        QOut("aclone=" + LTrim(Str(aNested[I(2)][I(1)])));

        // Empty() of an empty hash
        QOut("empty_hash=" + (Empty(hEmpty) ? "Y" : "N"));

        // Right() with a negative count
        QOut("right_neg=[" + Right("hello", -2) + "]");

        // StrZero of a negative number
        QOut("strzero=" + StrZero(-5, 4, 0));

        // DToC honours SET DATEFORMAT
        Set(_SET_DATEFORMAT, "dd.mm.yyyy");
        QOut("dtoc=" + DToC(SToD("20240115")));
    }
}
