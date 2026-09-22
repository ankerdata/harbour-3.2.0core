using System;
using static HbRuntime;
using static Program;

// Test 114: BREAK, BEGIN SEQUENCE and the Error object (family 9).
//
// BREAK emitted `throw new Exception(x)`, which binds an Error object to
// Exception(string) and throws at run time; RECOVER USING got the .NET
// exception instead of the BREAK's value; and BEGIN SEQUENCE WITH <block>
// was parsed and then dropped. Now (Alex, 2026-09-22) BREAK throws
// HbBreak carrying its value and a plain sequence catches only that, so
// a runtime error goes on to the error block at the entry point, as in
// Harbour; WITH { |e| break(e) }, the one error block the transpiler
// accepts (any other is E0101), catches every exception, RECOVER USING
// getting the Error object HbError.From makes of it. A RECOVER USING
// target that commits to no type is dynamic, whatever its initializer.
public static partial class Program
{
    public static void Main(string[] args)
    {
        dynamic oError = default;
        dynamic xGot = "unset";
        decimal nZero = 0;
        string cTrail = "";
        dynamic bOld = default;

        // BREAK with a value, from a called function
        try
        {
            test114_Brk114Throw(5);
        }
        catch (HbBreak __hb_brk1)
        {
            xGot = __hb_brk1.Value;
        }
        HbRuntime.QOut("value:", xGot);

        // an Error object built and broken, as easiudm.prg's print errors are
        try
        {
            test114_Brk114Throw(test114_Brk114Udm());
        }
        catch (HbBreak __hb_brk1)
        {
            oError = __hb_brk1.Value;
            HbRuntime.QOut("udm:", oError.subSystem, oError.subCode, oError.description);
        }

        // Break() in a codeblock: reaches the sequence through Eval() as the
        // BREAK it is, not wrapped by reflection
        try
        {
            HbRuntime.Eval(((Func<dynamic>)(() => HbRuntime.Break("from a block"))));
        }
        catch (HbBreak __hb_brk1)
        {
            xGot = __hb_brk1.Value;
        }
        HbRuntime.QOut("block:", xGot);

        // a bare BREAK: RECOVER USING gets NIL
        try
        {
            throw new HbBreak();
        }
        catch (HbBreak __hb_brk1)
        {
            xGot = __hb_brk1.Value;
        }
        HbRuntime.QOut("bare:", HbRuntime.ValType(xGot));

        // WITH { |e| break(e) }: a runtime error reaches RECOVER as an Error
        try
        {
            HbRuntime.QOut(1 / nZero);
        }
        catch (Exception __hb_ex1) when (__hb_ex1 is not HbQuit)
        {
            oError = HbError.From(__hb_ex1);
            HbRuntime.QOut("zero:", oError.genCode, oError.subSystem, oError.subCode, oError.description, oError.operation);
        }

        // WITH { || break() }: the error is NIL, an explicit BREAK keeps its value
        try
        {
            HbRuntime.QOut(1 / nZero);
        }
        catch (Exception __hb_ex1) when (__hb_ex1 is not HbQuit)
        {
            xGot = HbBreak.ValueOf(__hb_ex1);
            HbRuntime.QOut("nil block:", HbRuntime.ValType(xGot));
        }

        try
        {
            test114_Brk114Throw("own");
        }
        catch (Exception __hb_ex1) when (__hb_ex1 is not HbQuit)
        {
            xGot = HbBreak.ValueOf(__hb_ex1);
            HbRuntime.QOut("own value:", xGot);
        }

        // no RECOVER: the BREAK ends at END; ALWAYS always runs, and without
        // RECOVER lets the BREAK go on outward
        try
        {
            cTrail += "a";
            test114_Brk114Throw(1);
        }
        catch (HbBreak)
        {
        }
        try
        {
            try
            {
                cTrail += "b";
                test114_Brk114Throw("c");
            }
            finally
            {
                cTrail += "w";
            }
        }
        catch (HbBreak __hb_brk1)
        {
            xGot = __hb_brk1.Value;
            cTrail += xGot;
        }

        HbRuntime.QOut("trail:", cTrail);

        // a BREAK inside RECOVER goes to the next sequence out
        try
        {
            try
            {
                test114_Brk114Throw("in");
            }
            catch (HbBreak __hb_brk2)
            {
                xGot = __hb_brk2.Value;
                test114_Brk114Throw(xGot + "out");
            }
        }
        catch (HbBreak __hb_brk1)
        {
            xGot = __hb_brk1.Value;
            HbRuntime.QOut("nested:", xGot);
        }

        // ErrorBlock(): the one it replaced comes back
        bOld = HbRuntime.ErrorBlock(((Func<dynamic, dynamic>)((oErr) => test114_Brk114Handler(oErr))));
        HbRuntime.QOut("error block:", HbRuntime.Eval(HbRuntime.ErrorBlock(), test114_Brk114Udm()));
        HbRuntime.ErrorBlock(bOld);

        return;
    }

    public static void test114_Brk114Throw(dynamic xValue = default)
    {
        throw new HbBreak(xValue);
    }

    public static dynamic test114_Brk114Udm()
    {
        dynamic oError = HbRuntime.ErrorNew();

        oError.severity = 2;
        oError.subSystem = "UDMPRINT";
        oError.subCode = 7;
        oError.description = "Paper out";
        oError.canRetry = true;

        return oError;
    }

    public static dynamic test114_Brk114Handler(dynamic oErr = default)
    {
        return "handled " + oErr.subSystem;
    }
}
