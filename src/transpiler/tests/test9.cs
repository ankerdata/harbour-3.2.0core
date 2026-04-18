using System;
using System.Collections.Generic;
using static HbRuntime;

// Test 9: Type inference from initializers and Hungarian notation
// #include "hbclass.ch"
public class Inherited
{
    public static decimal nVersion { get; set; } = 1.0m;

}

public class Person : Inherited
{
    public decimal nAge { get; set; } = 0;
    public string cName { get; set; } = "";
    public DateOnly dBirth { get; set; }
    public bool lActive { get; set; } = true;
    public dynamic[] aItems { get; set; } = Array.Empty<dynamic>();
    public dynamic oParent { get; set; }
    public Dictionary<dynamic, dynamic> hConfig { get; set; }
    public dynamic bCallback { get; set; }
    public dynamic xUnknown { get; set; }
    public static decimal nCount { get; set; } = 0;

    public dynamic New()
    {
        HbRuntime.QOUT("New called");
        return this;
    }
}

public static partial class Program
{
    public static void Main(string[] args)
    {
        // Type from initializer (most specific)
        decimal nCount = 0;
        decimal nPrice = 9.99m;
        string cName = "hello";
        bool lFound = true;
        dynamic[] aList = new dynamic[] { 1, 2, 3 };
        Dictionary<dynamic, dynamic> hMap = new Dictionary<dynamic, dynamic> { { "key", "value" } };

        // Type from Hungarian prefix (no initializer)
        decimal nTotal = default;
        string cResult = default;
        bool lDone = default;
        dynamic[] aBuffer = default;
        dynamic oConnection = default;
        DateOnly dToday = default;
        Dictionary<dynamic, dynamic> hSettings = default;
        dynamic bAction = default;

        // No prefix, no initializer — fallback
        dynamic x = default;
        dynamic counter = default;
        dynamic Temp = default;

        HbRuntime.QOUT("nCount=" + HbRuntime.STR(nCount));
        HbRuntime.QOUT("nPrice=" + HbRuntime.STR(nPrice, 10, 2));
        HbRuntime.QOUT("cName=" + cName);
        HbRuntime.QOUT("lFound=" + (lFound ? ".T." : ".F."));

        return;
    }
}
