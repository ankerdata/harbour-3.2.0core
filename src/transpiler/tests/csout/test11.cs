using System;
using static HbRuntime;
using static Program;

// Test 11: CLASS with METHOD/PROCEDURE, standalone FUNCTIONs, constructor pattern
// #include "hbclass.ch"
public class Calculator
{
    public decimal nResult = 0;

    public virtual Calculator New()
    {
        nResult = 0;
        return this;
    }

    public virtual Calculator Add(decimal nValue = default)
    {
        nResult = nResult + nValue;
        HbRuntime.QOut("nResult=" + HbRuntime.Str(nResult));
        return this;
    }

    public virtual decimal GetResult()
    {
        return nResult;
    }

    public virtual void Reset()
    {
        nResult = 0;
        HbRuntime.QOut("nResult=" + HbRuntime.Str(nResult));
        return;
    }

    public virtual void Display(string cLabel = default)
    {
        HbRuntime.QOut(cLabel + ": " + HbRuntime.Str(nResult));
        return;
    }
}

public static partial class Program
{
    public static decimal CalcTotal(decimal nA = default, decimal nB = default, decimal nC = default)
    {
        decimal nResult = nA + nB;
        HbRuntime.QOut("nResult=" + HbRuntime.Str(nResult));
        nResult = nResult + nC;
        HbRuntime.QOut("nResult=" + HbRuntime.Str(nResult));
        return nResult;
    }

    public static string FormatPrice(decimal nPrice = default, string cCurrency = default)
    {
        decimal nFinal = nPrice * 1.15m;
        HbRuntime.QOut("nFinal=" + HbRuntime.Str(nFinal, 10, 2));
        return cCurrency + " " + HbRuntime.Str(nFinal, 10, 2);
    }

    public static void Main(string[] args)
    {
        Calculator oCalc = (Calculator)new Calculator().New();

        oCalc.Add(10);
        oCalc.Add(20);
        oCalc.Display("Total");

        HbRuntime.QOut("CalcTotal=" + HbRuntime.Str(CalcTotal(1, 2, 3)));
        HbRuntime.QOut("FormatPrice=" + FormatPrice(9.99m, "$"));

        return;
    }
}
