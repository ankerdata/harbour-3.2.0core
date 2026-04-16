using System;
using static HbRuntime;

// Test 21: class method with by-reference parameter.
//
// Exercises the (Class::Method)-keyed by-ref scan:
//
//   1. The scanner sees `oCalc:Adjust(@n)` and needs to mark the
//      parameter byref. Without method-keyed entries, the bitmap
//      would land on a bare `adjust` and could collide with any
//      free function or other class method called Adjust.
//
//   2. The C# emitter then has to find the same Class::Method entry
//      so the parameter declaration in `Adjust` gets `ref decimal`,
//      and the call site gets `oCalc.Adjust(ref n)`.
//
// The expected output is `40` because Adjust doubles its argument
// in place, and we then double it again.
// #include "hbclass.ch"
public class Calculator
{

    public object New()
    {
        return this;
    }

    public object Adjust(ref decimal nValue)
    {
        nValue = nValue * 2;
        return this;
    }
}

public static partial class Program
{
    public static void Main(string[] args)
    {
        Calculator oCalc = new Calculator();
        decimal n = 10;

        HbRuntime.QOUT("before: n=" + HbRuntime.STR(n));
        oCalc.Adjust(ref n);
        HbRuntime.QOUT("after:  n=" + HbRuntime.STR(n));
        oCalc.Adjust(ref n);
        HbRuntime.QOUT("after:  n=" + HbRuntime.STR(n));

        return;
    }
}
