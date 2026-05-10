using System;
using System.Collections.Generic;
using static HbRuntime;
using static Program;

// Test 9: Type inference from initializers and Hungarian notation
// #include "hbclass.ch"
public class Inherited
{
    public static decimal nVersion = 1.0m;

}

public class Person : Inherited
{
    public decimal nAge = 0;
    public string cName = "";
    public DateOnly dBirth;
    public bool lActive = true;
    public dynamic[] aItems = System.Array.Empty<dynamic>();
    public dynamic oParent;
    public Dictionary<string, dynamic> hConfig;
    public dynamic bCallback;
    public dynamic xUnknown;
    public static decimal nCount = 0;

    public dynamic New()
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
        Dictionary<string, dynamic> hMap = new Dictionary<string, dynamic> { { "key", "value" } };

        // Type from Hungarian prefix (no initializer)
        decimal nTotal = default;
        string cResult = default;
        bool lDone = default;
        dynamic[] aBuffer = default;
        dynamic oConnection = default;
        DateOnly dToday = default;
        Dictionary<string, dynamic> hSettings = default;
        dynamic bAction = default;

        // No prefix, no initializer — fallback
        dynamic x = default;
        dynamic counter = default;
        dynamic Temp = default;

        HbRuntime.QOut("nCount=" + HbRuntime.Str(nCount));
        HbRuntime.QOut("nPrice=" + HbRuntime.Str(nPrice, 10, 2));
        HbRuntime.QOut("cName=" + cName);
        HbRuntime.QOut("lFound=" + (lFound ? ".T." : ".F."));

        return;
    }
}
