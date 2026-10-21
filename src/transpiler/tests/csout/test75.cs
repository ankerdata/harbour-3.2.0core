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
public class Test75Dialog : IHbObject
{
    public OrderedDictionary<string, dynamic> hResultData;

    public dynamic ResultValue(dynamic cKey = default, dynamic xDefault = default) => ((hResultData != null && hb_HHasKey(hResultData, cKey)) ? ( hResultData[cKey]) : ( xDefault));
    public dynamic GetOrEmpty(dynamic cKey = default) => hb_HGetDef(hResultData, cKey, new OrderedDictionary<dynamic, dynamic>());
    public dynamic IsEmptyish(dynamic nVal = default) => nVal == 0 || ! (nVal != -1);
}

public static partial class Program
{
    public static void Main(string[] args)
    {
        var oDlg = new Test75Dialog();

        // hResultData is NIL here: iif's condition must short-circuit at
        // `!= NIL` and take the false branch without touching the hash
        // subscript. Exercises iif + NIL + .AND. + lazy branches.
        QOut("a=", oDlg.ResultValue("result", "fallback"));

        oDlg.hResultData = new OrderedDictionary<string, dynamic>
        {
            { "result", "done" },
            { "count", 2 }
        };
        QOut("b=", oDlg.ResultValue("result", "fallback"));
        QOut("c=", oDlg.ResultValue("missing", 42));

        // { => } as an INLINE argument: missing key falls back to an empty
        // hash, which must be a real (empty) hash, not a syntax error.
        QOut("d=", hb_HHasKey(oDlg.GetOrEmpty("nothere"), "x"));
        QOut("e=", Len(oDlg.GetOrEmpty("nothere")));

        // .OR. / .NOT. / <> in one body.
        QOut("f=", oDlg.IsEmptyish(0));
        QOut("g=", oDlg.IsEmptyish(-1));
        QOut("h=", oDlg.IsEmptyish(7));
    }
}
