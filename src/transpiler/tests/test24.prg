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
   LOCAL x := 1
   LOCAL aCodes := { ;            // comment after opening brace + ;
      "alpha",  ;                 // C1
      "bravo",  ;                 // C2
      "charlie" }                 // comment with no ; here
   LOCAL n

   IF x == 1                      // comment on IF line
      ? "one"
   ELSE                           // comment on ELSE line — the headliner
      ? "other"
   ENDIF                          // comment on ENDIF line

   DO CASE                        // comment on DO CASE
   CASE x == 1                    // comment on CASE
      ? "case one"
   OTHERWISE                      // comment on OTHERWISE
      ? "case other"
   ENDCASE                        // comment on ENDCASE

   FOR n := 1 TO Len( aCodes )    // comment on FOR
      ? aCodes[ n ]
   NEXT                           // comment on NEXT

   ? "array len:", Len( aCodes )
RETURN
