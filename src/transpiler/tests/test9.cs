using System;
using System.Collections.Generic;

// Test 9: Type inference from initializers and Hungarian notation
// #include "hbclass.ch"
public class Inherited
{
    public static decimal nVersion = 1.0m;

}

public class Person : Inherited
{
    public decimal nAge { get; set; } = 0;
    public string cName { get; set; } = "";
    public DateTime dBirth { get; set; }
    public bool lActive { get; set; } = true;
    public dynamic[] aItems { get; set; } = Array.Empty<dynamic>();
    public object oParent { get; set; }
    public Dictionary<dynamic, dynamic> hConfig { get; set; }
    public dynamic bCallback { get; set; }
    public dynamic xUnknown { get; set; }
    public static decimal nCount = 0;

    public object New()
    {
        HbRuntime.QOut("New called");
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
        decimal nTotal;
        string cResult;
        bool lDone;
        dynamic[] aBuffer;
        object oConnection;
        DateTime dToday;
        Dictionary<dynamic, dynamic> hSettings;
        dynamic bAction;

        // No prefix, no initializer — fallback
        dynamic x;
        dynamic counter;
        dynamic Temp;

        HbRuntime.QOut("nCount=" + HbRuntime.Str(nCount));
        HbRuntime.QOut("nPrice=" + HbRuntime.Str(nPrice, 10, 2));
        HbRuntime.QOut("cName=" + cName);
        HbRuntime.QOut("lFound=" + (lFound ? ".T." : ".F."));

        return;
    }
}
