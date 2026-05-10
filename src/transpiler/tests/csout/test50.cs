using System;
using static HbRuntime;
using static Program;

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
// runtime evaluation must produce identical output across the .prg,
// .hb, and .cs pipelines.

// #include "hbclass.ch"
public class Widget
{
    public string cLabel = "";
    public dynamic fHook;

    public dynamic New(string cLabel = default, dynamic fHook = default)
    {
        this.cLabel = cLabel;
        this.fHook = fHook;
        return this;
    }

    public dynamic Fire()
    {
        if (this.fHook != null)
        {
            HbRuntime.QOut("fire: " + HbRuntime.Eval(this.fHook, this.cLabel));
        }

        return null;
    }
}

public static partial class Program
{
    public static dynamic test50_sfPrint = HbRuntime.FuncPtr("PrintTag");
    public static void Main(string[] args)
    {
        dynamic fAdd = HbRuntime.FuncPtr("AddOne");
        dynamic fGreet = default;
        Widget oWidget = default;

        fGreet = HbRuntime.FuncPtr("SayHello");

        HbRuntime.QOut("fAdd(2,3)=" + HbRuntime.LTrim(HbRuntime.Str(HbRuntime.Eval(fAdd, 2, 3))));
        HbRuntime.QOut("fGreet=>" + HbRuntime.Eval(fGreet, "world"));
        HbRuntime.QOut("sfPrint=>" + HbRuntime.Eval(test50_sfPrint, "hello"));
        HbRuntime.QOut("Apply=>" + Apply(HbRuntime.FuncPtr("AddOne"), 10, 20));

        oWidget = (Widget)new Widget().New("OK", HbRuntime.FuncPtr("PrintTag"));
        oWidget.Fire();

        Invoke(HbRuntime.FuncPtr("SayHello"), "param-passed");
        return;
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
        return HbRuntime.LTrim(HbRuntime.Str(HbRuntime.Eval(fOp, nX, nY)));
    }

    public static void Invoke(dynamic fHandler = default, string cArg = default)
    {
        HbRuntime.QOut("Invoke=>" + HbRuntime.Eval(fHandler, cArg));
        return;
    }
}
