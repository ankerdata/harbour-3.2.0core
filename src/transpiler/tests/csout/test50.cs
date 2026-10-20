// Test 50: Function-pointer typing via 'f' prefix.
//
// In Harbour, @FuncName() takes the address of a function and yields
// a function pointer; the receiving parameter is invoked with
// fName:exec(args). The 'f' Hungarian prefix marks the parameter /
// variable as a function pointer (distinct from a code block, which
// uses 'b' and Eval).
//
// Coverage:
//   LOCAL fAdd := @AddOne()           -- prefix + initialiser via @
//   LOCAL fGreet                      -- prefix only, assigned later
//   PARAM fOp / fHandler              -- f-prefix parameter
//   STATIC sfPrint := @PrintTag()     -- s<H> two-letter convention
//   DATA fHook                        -- class member f-prefix
//   :exec(args) invocation            -- runtime dispatch
//
// Pattern observed in easipos adtdata.prg:
//   WriteFixedTotals(oTransaction, @ADTFixed(), cRecType, nSign)
//   PROCEDURE WriteFixedTotals(oTransaction, fFixedOutput, ...)
//      ...fFixedOutput:exec(cRecType, ...)
//
// All declarations must pass the W0021 Hungarian audit silently and
// runtime evaluation must produce identical output in the .prg and
// the .cs builds.
public class Widget : IHbObject
{
    public string cLabel = "";
    public dynamic fHook;

    public virtual Widget New(string cLabel = default, dynamic fHook = default)
    {
        this.cLabel = cLabel;
        this.fHook = fHook;
        return this;
    }

    public virtual dynamic Fire()
    {
        if (fHook != null)
        {
            QOut("fire: " + Eval(fHook, cLabel));
        }

        return null;
    }
}

public static partial class Program
{
    public static dynamic test50_sfPrint = FuncPtr("PrintTag");
    public static void Main(string[] args)
    {
        dynamic fAdd = FuncPtr("AddOne");
        dynamic fGreet;
        Widget oWidget;

        fGreet = FuncPtr("SayHello");

        QOut("fAdd(2,3)=" + LTrim(Str(Eval(fAdd, 2, 3))));
        QOut("fGreet=>" + Eval(fGreet, "world"));
        QOut("sfPrint=>" + Eval(test50_sfPrint, "hello"));
        QOut("Apply=>" + Apply(FuncPtr("AddOne"), 10, 20));

        oWidget = new Widget().New("OK", FuncPtr("PrintTag"));
        oWidget.Fire();

        Invoke(FuncPtr("SayHello"), "param-passed");
    }

    public static decimal AddOne(decimal nX = default, decimal nY = default)
    {
        return nX + nY;
    }

    public static string SayHello(string cName = default)
    {
        return "Hello, " + cName;
    }

    public static string PrintTag(string cTag = default)
    {
        return "tag:" + cTag;
    }

    public static string Apply(dynamic fOp = default, decimal nX = default, decimal nY = default)
    {
        return LTrim(Str(Eval(fOp, nX, nY)));
    }

    public static void Invoke(dynamic fHandler = default, string cArg = default)
    {
        QOut("Invoke=>" + Eval(fHandler, cArg));
    }
}
