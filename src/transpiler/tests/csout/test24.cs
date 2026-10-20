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
public static partial class Program
{
    public static void Main(string[] args)
    {
        decimal x = 1;

        // comment after opening brace + ;
        // C1
        // C2
        // comment with no ; here
        List<dynamic> aCodes = new List<dynamic> { "alpha", "bravo", "charlie" };
        long n = default;

        // comment on IF line
        if (x == 1)
        {
            QOut("one");
            // comment on ELSE line — the headliner
        }
        else
        {
            QOut("other");
        }
        // comment on ENDIF line

        if (x == 1)
        {
            QOut("case one");
        }
        else
        {
            // comment on OTHERWISE
            QOut("case other");
        }
        // comment on ENDCASE

        // comment on FOR
        for (n = 1; n <= Len(aCodes); n++)
        {
            QOut(aCodes[Ix(n)]);
            // comment on NEXT
        }

        QOut("array len:", Len(aCodes));
        return;
    }
}
