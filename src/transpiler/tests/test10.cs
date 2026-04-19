using System;
using static HbRuntime;
using static Program;

// Test 10: CLASS methods, STATIC local, WITH OBJECT
// #include "hbclass.ch"
public class MyObj
{
    public decimal nValue { get; set; } = 0;

    public dynamic New()
    {
        this.nValue = 0;
        return this;
    }

    public dynamic SetValue(decimal nVal = default)
    {
        this.nValue = nVal;
        HbRuntime.QOUT("nValue=" + HbRuntime.STR(this.nValue));
        return this;
    }
}

public static partial class Program
{
    static decimal test10_Main_nCounter = 0;
    public static void Main(string[] args)
    {
        MyObj oObj = new MyObj();

        // WITH OBJECT test
        oObj.SetValue(42);

        test10_Main_nCounter = test10_Main_nCounter + 1;
        HbRuntime.QOUT("nCounter=" + HbRuntime.STR(test10_Main_nCounter));

        return;
    }
}
