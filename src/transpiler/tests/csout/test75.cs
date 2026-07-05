using System;
using static HbRuntime;
using static Program;

// Test 75: hb_csTranslateInline coverage for constructs that used to
// leak raw Harbour into C# expression-bodied members: iif() (must
// become a lazy ternary), NIL, <> (the PP-canonical form of !=),
// .AND. / .OR. / .NOT., and the empty-hash literal { => }.
//
// The iif→ternary rewrite is streaming: the translator emits `((`,
// then rewrites that call's two top-level commas to `) ? (` / `) : (`
// and its closing paren to `))`, tracking (/[/{ depth so commas nested
// in inner calls or subscripts pass through untouched. Laziness is the
// point of using a real ternary: in ResultValue below, the hash
// subscript must not evaluate while hResultData is NIL — an eager
// IIF() helper function would throw where Harbour's iif does not.

// #include "hbclass.ch"
public class Test75Dialog
{
    public Dictionary<string, dynamic> hResultData;

    public dynamic ResultValue(dynamic cKey = default, dynamic xDefault = default) => ((this.hResultData != null && hb_HHasKey(this.hResultData, cKey)) ? ( this.hResultData[cKey]) : ( xDefault));
    public dynamic GetOrEmpty(dynamic cKey = default) => hb_HGetDef(this.hResultData, cKey, new Dictionary<dynamic, dynamic>());
    public dynamic IsEmptyish(dynamic nVal = default) => nVal == 0 || ! (nVal != -1);
}

public static partial class Program
{
    public static void Main(string[] args)
    {
        Test75Dialog oDlg = new Test75Dialog();

        // hResultData is NIL here: iif's condition must short-circuit at
        // `!= NIL` and take the false branch without touching the hash
        // subscript. Exercises iif + NIL + .AND. + lazy branches.
        HbRuntime.QOut("a=", oDlg.ResultValue("result", "fallback"));

        oDlg.hResultData = new Dictionary<string, dynamic> { { "result", "done" }, { "count", 2 } };
        HbRuntime.QOut("b=", oDlg.ResultValue("result", "fallback"));
        HbRuntime.QOut("c=", oDlg.ResultValue("missing", 42));

        // { => } as an INLINE argument: missing key falls back to an empty
        // hash, which must be a real (empty) hash, not a syntax error.
        HbRuntime.QOut("d=", HbRuntime.hb_HHasKey(oDlg.GetOrEmpty("nothere"), "x"));
        HbRuntime.QOut("e=", HbRuntime.Len(oDlg.GetOrEmpty("nothere")));

        // .OR. / .NOT. / <> in one body.
        HbRuntime.QOut("f=", oDlg.IsEmptyish(0));
        HbRuntime.QOut("g=", oDlg.IsEmptyish(-1));
        HbRuntime.QOut("h=", oDlg.IsEmptyish(7));

        return;
    }
}
