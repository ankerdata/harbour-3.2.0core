using System;
using System.Collections.Generic;
using static HbRuntime;
using static Program;

// Test 138 (multi-file pair): the classes, in a file the scan reaches after
// their users (test138a.prg says why).

// #include "../include/astype.ch"
// #include "hbclass.ch"
public class Line138 : IHbObject
{
    public decimal nKind = 0;

}

public class PrintLine138 : Line138
{
    public decimal nValue = 0;

}

public class FcnLine138 : PrintLine138
{

    public virtual FcnLine138 New(decimal nValue = default)
    {
        nKind = 1;
        this.nValue = nValue;
        return this;
    }
}

public class GenLine138 : PrintLine138
{

    public virtual GenLine138 New(decimal nValue = default)
    {
        nKind = 2;
        this.nValue = nValue;
        return this;
    }
}

public class Till138 : IHbObject
{
    public List<dynamic> aLines = new List<dynamic>();

}

public static partial class Program
{
    public static void Main(string[] args)
    {
        Till138 oTill138 = new Till138();
        HbRuntime.AAdd(oTill138.aLines, new FcnLine138().New(5));
        HbRuntime.AAdd(oTill138.aLines, new GenLine138().New(7));
        HbRuntime.QOut(FirstTotal138(oTill138), SumValues138(oTill138));
        HbRuntime.QOut(LaterTotal138(new FcnLine138().New(2), new GenLine138().New(3)));
        return;
    }
}
