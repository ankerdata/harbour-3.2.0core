#include "astype.ch"
// Test 60: HbRuntime correctness fixes.
//
//   Round  — half-away-from-zero, not C#'s banker's rounding.
//            Round(2.5,0) is 3 and Round(-2.5,0) is -3 (banker's
//            would give 2 / -2); Round(0.5,0) is 1 (banker's: 0).
//   Len    — a hash's length is its key count, not 0.
//   ASort  — a sort block that ties on equal elements must not
//            produce an inconsistent comparer (Array.Sort throws).
//   Val    — parses the leading numeric run, ignoring trailing text.

PROCEDURE Main()
   LOCAL hData := {"a" => 1, "b" => 2, "c" => 3} AS HASH
   // note the duplicate 1
   LOCAL aNums := {3, 1, 2, 1} AS ARRAY
   LOCAL i AS NUMERIC
   LOCAL cOut AS STRING

   QOut("round_2.5=" + LTrim(Str(Round(2.5, 0))))
   QOut("round_0.5=" + LTrim(Str(Round(0.5, 0))))
   QOut("round_neg=" + LTrim(Str(Round(-2.5, 0))))

   QOut("len_hash=" + LTrim(Str(Len(hData))))

   ASort(aNums, , , {|x, y| x < y})
   cOut := ""
   FOR i := 1 TO Len(aNums)
      cOut += LTrim(Str(aNums[i]))
   NEXT

   QOut("sorted=" + cOut)

   QOut("val_abc=" + LTrim(Str(Val("12abc"))))
   // compared, not Str()'d — Str() of a fraction ignores SET DECIMALS
   QOut("val_dec=" + IIF(Val("3.5x") == 3.5, "Y", "N"))
   QOut("val_neg=" + LTrim(Str(Val("-7kg"))))
RETURN
