#include "astype.ch"
// Test 24: comment preservation vs. parser interactions.
//
// When -k comment preservation is on (the transpiler always runs
// with it, so that Harbour comments can be emitted as C# `//`
// comments in the generated output), two patterns used to break
// the parser:
//
//   1. `ELSE  // trailing comment` — the lexer peeks at `pToken->pNext`
//      to classify ELSE and demand an end-of-command token. A
//      COMMENT token sitting there made the peek fail and ELSE got
//      demoted to IDENTIFIER, producing E0020 "Incomplete statement".
//      Same mechanism breaks ENDIF / ENDCASE / ENDDO / ELSEIF / CASE /
//      OTHERWISE / ENDSEQ / ENDSWITCH / ENDWITH / ALWAYS whenever
//      they have a trailing `//` or `&&` comment on the same line.
//
//   2. `{ ;` array literal with `// comment` on each continuation
//      line — hb_pp_tokenAddNext flushes a CmdSep `;` token whenever
//      fCanNextLine is TRUE, which made emitting the COMMENT token
//      inject a spurious statement separator and break the logical
//      line. Produced E0030 "syntax error at ';'" mid-array.
//
// Both are common idioms in real Harbour code. A regression here
// would block transpiling most mid-sized codebases.
#include "common.ch"

PROCEDURE Main()
   LOCAL x := 1 AS NUMERIC

   // comment after opening brace + ;
   // C1
   // C2
   // comment with no ; here
   LOCAL aCodes := {"alpha", "bravo", "charlie"} AS ARRAY
   LOCAL n AS NUMERIC

   // comment on IF line
   IF x == 1
      QOut("one")
      // comment on ELSE line — the headliner
   ELSE
      QOut("other")
   ENDIF
   // comment on ENDIF line

   DO CASE
      CASE x == 1
         QOut("case one")
      OTHERWISE
         // comment on OTHERWISE
         QOut("case other")
   ENDCASE
   // comment on ENDCASE

   // comment on FOR
   FOR n := 1 TO Len(aCodes)
      QOut(aCodes[n])
      // comment on NEXT
   NEXT

   QOut("array len:", Len(aCodes))
RETURN
