using System;
using static HbRuntime;
using static Program;

// Test 38: IIF as statement, with empty branches.
//
// Real-prg idiom from easiposx/easikds.prg:
//     iif(cond, sideEffect(), )
// Harbour accepts an empty 3rd arg (NIL) and uses iif purely for its
// side effect. Before the fix, the emitter produced `(a ? b : );`
// which C# rejects on two grounds: empty false-branch, and ternary
// used as statement (CS0201). Fix expands statement-position IIF to
// an if/else and substitutes default for empty branches.
public static partial class Program
{
    public static void Main(string[] args)
    {
        decimal n = 3;

        // Fires side effect when cond true, nothing else.
        if (n > 0)
            HbRuntime.QOut("pos3");

        // Empty when cond false — false-branch default case.
        if (n > 10)
            HbRuntime.QOut("big");

        // Empty true-branch — exercises both.
        if (n < 0)
            ;
        else
            HbRuntime.QOut("nonneg");

        // Classic both-branches form — should still work.
        if (n > 0)
            HbRuntime.QOut("yes");
        else
            HbRuntime.QOut("no");

        return;
    }
}
