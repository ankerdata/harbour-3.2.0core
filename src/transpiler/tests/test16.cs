using System;
using static HbRuntime;
using static Program;

// Test 16: STATIC, PUBLIC, and PRIVATE declarations with type inference
public static partial class Program
{
    static decimal test16_Counter_nCount = 0;
    static string test16_Counter_cLabel = "count";
    static dynamic test16_nPriv;
    static dynamic test16_cPrivName;
    static decimal test16_Main_nStatic = 10;
    static string test16_Main_cStaticLabel = "static";
    public static dynamic nPub;
    public static dynamic cPubName;
    public static decimal Counter()
    {
        test16_Counter_nCount = test16_Counter_nCount + 1;
        HbRuntime.QOUT(test16_Counter_cLabel + "=" + HbRuntime.STR(test16_Counter_nCount));

        return test16_Counter_nCount;
    }

    public static void Main(string[] args)
    {
        test16_nPriv = 42;
        test16_cPrivName = "secret";
        nPub = 100;
        cPubName = "global";

        HbRuntime.QOUT("nStatic=" + HbRuntime.STR(test16_Main_nStatic));
        HbRuntime.QOUT("cStaticLabel=" + test16_Main_cStaticLabel);
        HbRuntime.QOUT("nPriv=" + HbRuntime.STR(test16_nPriv));
        HbRuntime.QOUT("cPrivName=" + test16_cPrivName);
        HbRuntime.QOUT("nPub=" + HbRuntime.STR(nPub));
        HbRuntime.QOUT("cPubName=" + cPubName);

        Counter();
        Counter();
        Counter();

        test16_Main_nStatic = test16_Main_nStatic + 5;
        HbRuntime.QOUT("nStatic=" + HbRuntime.STR(test16_Main_nStatic));

        test16_nPriv = test16_nPriv + 1;
        HbRuntime.QOUT("nPriv=" + HbRuntime.STR(test16_nPriv));

        nPub = nPub - 10;
        HbRuntime.QOUT("nPub=" + HbRuntime.STR(nPub));

        return;
    }
}
