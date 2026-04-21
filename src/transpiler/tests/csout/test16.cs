using System;
using static HbRuntime;
using static Program;

// Test 16: STATIC, PUBLIC, and PRIVATE declarations with type inference
public static partial class Program
{
    public static decimal test16_nCount = 0;
    public static string test16_cLabel = "count";
    public static dynamic test16_nPriv;
    public static dynamic test16_cPrivName;
    public static decimal test16_nStatic = 10;
    public static string test16_cStaticLabel = "static";
    public static dynamic nPub;
    public static dynamic cPubName;
    public static decimal Counter()
    {
        test16_nCount = test16_nCount + 1;
        HbRuntime.QOut(test16_cLabel + "=" + HbRuntime.Str(test16_nCount));

        return test16_nCount;
    }

    public static void Main(string[] args)
    {
        test16_nPriv = 42;
        test16_cPrivName = "secret";
        nPub = 100;
        cPubName = "global";

        HbRuntime.QOut("nStatic=" + HbRuntime.Str(test16_nStatic));
        HbRuntime.QOut("cStaticLabel=" + test16_cStaticLabel);
        HbRuntime.QOut("nPriv=" + HbRuntime.Str(test16_nPriv));
        HbRuntime.QOut("cPrivName=" + test16_cPrivName);
        HbRuntime.QOut("nPub=" + HbRuntime.Str(nPub));
        HbRuntime.QOut("cPubName=" + cPubName);

        Counter();
        Counter();
        Counter();

        test16_nStatic = test16_nStatic + 5;
        HbRuntime.QOut("nStatic=" + HbRuntime.Str(test16_nStatic));

        test16_nPriv = test16_nPriv + 1;
        HbRuntime.QOut("nPriv=" + HbRuntime.Str(test16_nPriv));

        nPub = nPub - 10;
        HbRuntime.QOut("nPub=" + HbRuntime.Str(nPub));

        return;
    }
}
