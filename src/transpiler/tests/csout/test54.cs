using System;
using static HbRuntime;
using static Program;

// Test 54: paren preservation around binary infix ops nested in
// higher-precedence parents (typically unary `!` or `-`).
//
// Bug pattern: source `IF !(cPath + aFile[1] == cZipFile)` was
// emitted as C# `if (!cPath + aFile[1] == cZipFile)`, dropping
// the outer parens. The unary `!` then bound to `cPath` only,
// surfacing as CS0023 "operator ! cannot be applied to type
// string". Same shape with `-(a+b)` etc.
//
// 113 of 114 CS0023 errors in easipos collapsed once the paren-
// emit was fixed. Pattern observed in zipunzip.prg, repeated
// across 13 files.
public static partial class Program
{
    public static void Main(string[] args)
    {
        string cPath = "/tmp/";
        dynamic[] aFile = new dynamic[] { "report.zip" };
        string cZipFile = "/tmp/report.zip";
        decimal nA = 5;
        decimal nB = 3;

        /* The headline case: `!` over a comparison whose left side is
      itself a binary op. Three-level nesting. */
        if (!(cPath + aFile[0] == cZipFile))
        {
            HbRuntime.QOut("different");
        }
        else
        {
            HbRuntime.QOut("match");
        }

        /* Negation around an arithmetic expression. */
        HbRuntime.QOut("neg= " + HbRuntime.LTrim(HbRuntime.Str(-(nA + nB))));

        /* `!` over a logical AND — both sides are comparisons. */
        if (!(nA > 0 && nB > 0))
        {
            HbRuntime.QOut("one zero");
        }
        else
        {
            HbRuntime.QOut("both pos");
        }

        /* `!` over a logical OR. */
        if (!(nA == 0 || nB == 0))
        {
            HbRuntime.QOut("neither zero");
        }
        else
        {
            HbRuntime.QOut("some zero");
        }

        return;
    }
}
