#include "astype.ch"
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

#include "set.ch"

PROCEDURE Main()
   LOCAL aData := {10, 20, 30} AS ARRAY
   LOCAL aNested := {1, {2, 3}} AS ARRAY
   LOCAL aCopy AS ARRAY
   LOCAL hEmpty := {=>} AS HASH

   // AScan with a codeblock — 20 is at index 2
   QOut("ascan_blk=" + LTrim(Str(AScan(aData, {|x| x == 20}))))

   // AClone must deep-copy the nested array
   aCopy := AClone(aNested)
   // must not touch the original
   aCopy[2][1] := 99
   // still 2
   QOut("aclone=" + LTrim(Str(aNested[2][1])))

   // Empty() of an empty hash
   QOut("empty_hash=" + IIF(Empty(hEmpty), "Y", "N"))

   // Right() with a negative count
   QOut("right_neg=[" + Right("hello", -2) + "]")

   // StrZero of a negative number
   QOut("strzero=" + StrZero(-5, 4, 0))

   // DToC honours SET DATEFORMAT
   Set(_SET_DATEFORMAT, "dd.mm.yyyy")
   QOut("dtoc=" + DToC(SToD("20240115")))
RETURN
