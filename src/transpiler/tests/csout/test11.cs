using System;
using static HbRuntime;
using static Program;

// Test 11: CLASS with METHOD/PROCEDURE, standalone FUNCTIONs, constructor pattern
public class Calculator : IHbObject
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
        QOut("nResult=" + Str(nResult));
        return this;
    }

    public virtual decimal GetResult()
    {
        return nResult;
    }

    public virtual void Reset()
    {
        nResult = 0;
        QOut("nResult=" + Str(nResult));
        return;
    }

    public virtual void Display(string cLabel = default)
    {
        QOut(cLabel + ": " + Str(nResult));
        return;
    }
}

public static partial class Program
{
    public static decimal CalcTotal(decimal nA = default, decimal nB = default, decimal nC = default)
    {
        decimal nResult = nA + nB;
        QOut("nResult=" + Str(nResult));
        nResult = nResult + nC;
        QOut("nResult=" + Str(nResult));
        return nResult;
    }

    public static string FormatPrice(decimal nPrice = default, string cCurrency = default)
    {
        decimal nFinal = nPrice * 1.15m;
        QOut("nFinal=" + Str(nFinal, 10, 2));
        return cCurrency + " " + Str(nFinal, 10, 2);
    }

    public static void Main(string[] args)
    {
        Calculator oCalc = new Calculator().New();

        oCalc.Add(10);
        oCalc.Add(20);
        oCalc.Display("Total");

        QOut("CalcTotal=" + Str(CalcTotal(1, 2, 3)));
        QOut("FormatPrice=" + FormatPrice(9.99m, "$"));

        return;
    }
}
