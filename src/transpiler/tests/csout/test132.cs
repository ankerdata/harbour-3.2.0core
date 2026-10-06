using System;
using static HbRuntime;
using static Program;

// Test 132: a method's return type reaches its caller through a send.
//
// The inference read only member rows (`Class::member`) for a send, never a
// method's row (`Class::Class__Method`), so `RETURN oObj:Method()` and
// `x := oObj:Method()` learned nothing of what the method returns, where a
// call to a function passes its return type on. Read132() now returns
// string and its local is decimal.

// #include "hbclass.ch"
public class Meter132 : IHbObject
{
    public decimal nReading132 = 41;

    public virtual decimal Reading132()
    {
        return nReading132 + 1;
    }

    public virtual string Label132()
    {
        return "meter";
    }
}

public static partial class Program
{
    public static string Read132(Meter132 oMeter132 = default)
    {
        decimal xReading = oMeter132.Reading132();
        return oMeter132.Label132() + " " + HbRuntime.hb_ntos(xReading);
    }

    public static void Main(string[] args)
    {
        HbRuntime.QOut(Read132(new Meter132()));
        return;
    }
}
