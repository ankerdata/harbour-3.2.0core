using System;

// Test 11: CLASS with METHOD/PROCEDURE, standalone FUNCTIONs, constructor pattern
// #include "hbclass.ch"
public class Calculator
{
    public decimal nResult { get; set; } = 0;

    public object New()
    {
        this.nResult = 0;
        return this;
    }

    public object Add(decimal nValue)
    {
        this.nResult = this.nResult + nValue;
        HbRuntime.QOut("nResult=" + HbRuntime.Str(this.nResult));
        return this;
    }

    public decimal GetResult()
    {
        return this.nResult;
    }

    public void Reset()
    {
        this.nResult = 0;
        HbRuntime.QOut("nResult=" + HbRuntime.Str(this.nResult));
        return;
    }

    public void Display(string cLabel)
    {
        HbRuntime.QOut(cLabel + ": " + HbRuntime.Str(this.nResult));
        return;
    }
}

public static class Program
{
    public static decimal CalcTotal(decimal nA, decimal nB, decimal nC)
    {
        decimal nResult = nA + nB;
        HbRuntime.QOut("nResult=" + HbRuntime.Str(nResult));
        nResult += nC;
        HbRuntime.QOut("nResult=" + HbRuntime.Str(nResult));
        return nResult;
    }

    public static string FormatPrice(decimal nPrice, string cCurrency)
    {
        decimal nFinal = nPrice * 1.15m;
        HbRuntime.QOut("nFinal=" + HbRuntime.Str(nFinal, 10, 2));
        return cCurrency + " " + HbRuntime.Str(nFinal, 10, 2);
    }

    public static void Main(string[] args)
    {
        Calculator oCalc = new Calculator();

        oCalc.Add(10);
        oCalc.Add(20);
        oCalc.Display("Total");

        HbRuntime.QOut("CalcTotal=" + HbRuntime.Str(CalcTotal(1, 2, 3)));
        HbRuntime.QOut("FormatPrice=" + FormatPrice(9.99m, "$"));

        return;
    }
}
