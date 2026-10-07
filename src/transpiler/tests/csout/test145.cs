using System;
using static HbRuntime;
using static Program;

// Test 145: a codeblock whose last expression calls a PROCEDURE yields NIL.
//
// A PROCEDURE is void in C#, and the emitter wrote every block as `=> call`.
// With an argument dynamic the call bound at run time and failed there
// ("Cannot implicitly convert type 'void' to 'object'"), as EasiPOS's
// XZFirst() did with AEval( aMore, {|x| XZLine( …, RTrim( x ), .T. )} );
// with every argument typed it would not have compiled. The scan records a
// PROCEDURE in the reftab (flag N), and such a block is { call; return null; }.
public static partial class Program
{
    public static string test145_s_cLog145 = "";
    public static void Note145(string cText = default)
    {
        test145_s_cLog145 += "[" + cText + "]";
        return;
    }

    public static void test145_Quiet145(string cText = default)
    {
        test145_s_cLog145 += "(" + cText + ")";
        return;
    }

    public static void Main(string[] args)
    {
        List<dynamic> aWords145 = new List<dynamic> { "one", "two ", "three" };
        Func<dynamic, dynamic> bNote145 = ((Func<dynamic, dynamic>)((x) => { Note145(HbRuntime.RTrim(x)); return null; }));
        dynamic xResult = default;

        // x says nothing of its type: the call binds at run time
        HbRuntime.AEval(aWords145, ((Func<dynamic, dynamic>)((x) => { Note145(HbRuntime.RTrim(x)); return null; })));
        HbRuntime.QOut("AEval, bound at run time:", test145_s_cLog145);

        // cWord is a string: the call binds when C# compiles
        test145_s_cLog145 = "";
        HbRuntime.AEval(aWords145, ((Func<string, dynamic>)((cWord) => { Note145(cWord); return null; })));
        HbRuntime.QOut("AEval, bound when compiled:", test145_s_cLog145);

        test145_s_cLog145 = "";
        xResult = HbRuntime.Eval(bNote145, "four ");
        HbRuntime.QOut("Eval:", test145_s_cLog145, "value", (xResult == null ? "NIL" : "not NIL"));

        test145_s_cLog145 = "";
        HbRuntime.AEval(aWords145, ((Func<dynamic, dynamic>)((x) => { test145_Quiet145(x); return null; })));
        HbRuntime.QOut("a STATIC PROCEDURE:", test145_s_cLog145);

        // several expressions, the last a procedure
        test145_s_cLog145 = "";
        xResult = HbRuntime.Eval(((Func<dynamic, dynamic>)((x) => { Note145("a"); Note145(x); return null; })), "b");
        HbRuntime.QOut("a list:", test145_s_cLog145, "value", (xResult == null ? "NIL" : "not NIL"));

        return;
    }
}
