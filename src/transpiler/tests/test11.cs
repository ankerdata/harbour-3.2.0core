using System;
using static HbRuntime;

// Test 11: CLASS with METHOD/PROCEDURE, standalone FUNCTIONs, constructor pattern
// #include "hbclass.ch"
public class Calculator
{
    public decimal nResult { get; set; } = 0;

    public dynamic New()
    {
        this.nResult = 0;
        return this;
    }

    public dynamic Add(decimal nValue = default)
    {
        this.nResult = this.nResult + nValue;
        HbRuntime.QOUT("nResult=" + HbRuntime.STR(this.nResult));
        return this;
    }

    public decimal GetResult()
    {
        return this.nResult;
    }

    public void Reset()
    {
        this.nResult = 0;
        HbRuntime.QOUT("nResult=" + HbRuntime.STR(this.nResult));
        return;
    }

    public void Display(string cLabel = default)
    {
        HbRuntime.QOUT(cLabel + ": " + HbRuntime.STR(this.nResult));
        return;
    }
}

public static partial class Program
{
    public static decimal CalcTotal(decimal nA = default, decimal nB = default, decimal nC = default)
    {
        decimal nResult = nA + nB;
        HbRuntime.QOUT("nResult=" + HbRuntime.STR(nResult));
        nResult = nResult + nC;
        HbRuntime.QOUT("nResult=" + HbRuntime.STR(nResult));
        return nResult;
    }

    public static string FormatPrice(decimal nPrice = default, string cCurrency = default)
    {
        decimal nFinal = nPrice * 1.15m;
        HbRuntime.QOUT("nFinal=" + HbRuntime.STR(nFinal, 10, 2));
        return cCurrency + " " + HbRuntime.STR(nFinal, 10, 2);
    }

    public static void Main(string[] args)
    {
        Calculator oCalc = new Calculator();

        oCalc.Add(10);
        oCalc.Add(20);
        oCalc.Display("Total");

        HbRuntime.QOUT("CalcTotal=" + HbRuntime.STR(CalcTotal(1, 2, 3)));
        HbRuntime.QOUT("FormatPrice=" + FormatPrice(9.99m, "$"));

        return;
    }
}
