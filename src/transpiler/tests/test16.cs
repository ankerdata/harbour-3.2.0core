using System;
using static HbRuntime;

// Test 16: STATIC, PUBLIC, and PRIVATE declarations with type inference
public static partial class Program
{
    static decimal test16_nCount = 0;
    static string test16_cLabel = "count";
    static decimal test16_nStatic = 10;
    static string test16_cStaticLabel = "static";
    public static decimal Counter()
    {
        test16_nCount += 1;
        HbRuntime.QOUT(test16_cLabel + "=" + HbRuntime.STR(test16_nCount));

        return test16_nCount;
    }

    public static void Main(string[] args)
    {
        decimal nPriv = 42;
        string cPrivName = "secret";
        decimal nPub = 100;
        string cPubName = "global";

        HbRuntime.QOUT("nStatic=" + HbRuntime.STR(test16_nStatic));
        HbRuntime.QOUT("cStaticLabel=" + test16_cStaticLabel);
        HbRuntime.QOUT("nPriv=" + HbRuntime.STR(nPriv));
        HbRuntime.QOUT("cPrivName=" + cPrivName);
        HbRuntime.QOUT("nPub=" + HbRuntime.STR(nPub));
        HbRuntime.QOUT("cPubName=" + cPubName);

        Counter();
        Counter();
        Counter();

        test16_nStatic += 5;
        HbRuntime.QOUT("nStatic=" + HbRuntime.STR(test16_nStatic));

        nPriv += 1;
        HbRuntime.QOUT("nPriv=" + HbRuntime.STR(nPriv));

        nPub -= 10;
        HbRuntime.QOUT("nPub=" + HbRuntime.STR(nPub));

        return;
    }
}
