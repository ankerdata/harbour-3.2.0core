using System;

// Test 10: CLASS methods, STATIC local, WITH OBJECT
// #include "hbclass.ch"
public class MyObj
{
    public decimal nValue { get; set; } = 0;

    public object New()
    {
        this.nValue = 0;
        return this;
    }

    public object SetValue(decimal nVal)
    {
        this.nValue = nVal;
        HbRuntime.QOut("nValue=" + HbRuntime.Str(this.nValue));
        return this;
    }
}

public static class Program
{
    static decimal nCounter = 0;
    public static void Main(string[] args)
    {
        MyObj oObj = new MyObj();

        // WITH OBJECT test
        oObj.SetValue(42);

        nCounter += 1;
        HbRuntime.QOut("nCounter=" + HbRuntime.Str(nCounter));

        return;
    }
}
