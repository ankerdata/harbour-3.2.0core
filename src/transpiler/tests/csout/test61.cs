using System;
using static HbRuntime;
using static Program;

// Test 61: more HbRuntime correctness fixes.
//
//   AScan  — the codeblock form: scan evaluating a block per element.
//   AClone — duplicates nested arrays recursively, not shallowly.
//   Empty  — an empty hash is Empty().
//   Right  — a non-positive count yields "" (not an exception).
//   StrZero— a negative number keeps its sign: StrZero(-5,4) -> "-005".
//   DToC   — honours SET DATEFORMAT.
//
// Not covered here (can't round-trip through Harbour standalone):
// GETMEMBER/SETMEMBER reaching the dynamic bag, and hb_mutexLock's
// timeout — both fixed in HbRuntime alongside these.

// #include "set.ch"
public static partial class Program
{
    public static void Main(string[] args)
    {
        dynamic[] aData = new dynamic[] { 10, 20, 30 };
        dynamic[] aNested = new dynamic[] { 1, new dynamic[] { 2, 3 } };
        dynamic[] aCopy = default;
        Dictionary<string, dynamic> hEmpty = new Dictionary<string, dynamic> {  };

        // AScan with a codeblock — 20 is at index 2
        HbRuntime.QOut("ascan_blk=" + HbRuntime.LTrim(HbRuntime.Str(HbRuntime.AScan(aData, ((Func<dynamic, dynamic>)((x) => x == 20))))));

        // AClone must deep-copy the nested array
        aCopy = HbRuntime.AClone(aNested);
        // must not touch the original
        aCopy[1][0] = 99;
        // still 2
        HbRuntime.QOut("aclone=" + HbRuntime.LTrim(HbRuntime.Str(aNested[1][0])));

        // Empty() of an empty hash
        HbRuntime.QOut("empty_hash=" + (HbRuntime.Empty(hEmpty) ? "Y" : "N"));

        // Right() with a negative count
        HbRuntime.QOut("right_neg=[" + HbRuntime.Right("hello", -2) + "]");

        // StrZero of a negative number
        HbRuntime.QOut("strzero=" + HbRuntime.StrZero(-5, 4, 0));

        // DToC honours SET DATEFORMAT
        HbRuntime.Set(_SET_DATEFORMAT, "dd.mm.yyyy");
        HbRuntime.QOut("dtoc=" + HbRuntime.DToC(HbRuntime.SToD("20240115")));
        return;
    }
}
