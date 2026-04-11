using System;

// Test 16: STATIC, PUBLIC, and PRIVATE declarations with type inference
public static partial class Program
{
    static decimal nCount = 0;
    static string cLabel = "count";
    static decimal nStatic = 10;
    static string cStaticLabel = "static";
    public static decimal Counter()
    {
        nCount += 1;
        HbRuntime.QOUT(cLabel + "=" + HbRuntime.STR(nCount));

        return nCount;
    }

    public static void Main(string[] args)
    {
        decimal nPriv = 42;
        string cPrivName = "secret";
        decimal nPub = 100;
        string cPubName = "global";

        HbRuntime.QOUT("nStatic=" + HbRuntime.STR(nStatic));
        HbRuntime.QOUT("cStaticLabel=" + cStaticLabel);
        HbRuntime.QOUT("nPriv=" + HbRuntime.STR(nPriv));
        HbRuntime.QOUT("cPrivName=" + cPrivName);
        HbRuntime.QOUT("nPub=" + HbRuntime.STR(nPub));
        HbRuntime.QOUT("cPubName=" + cPubName);

        Counter();
        Counter();
        Counter();

        nStatic += 5;
        HbRuntime.QOUT("nStatic=" + HbRuntime.STR(nStatic));

        nPriv += 1;
        HbRuntime.QOUT("nPriv=" + HbRuntime.STR(nPriv));

        nPub -= 10;
        HbRuntime.QOUT("nPub=" + HbRuntime.STR(nPub));

        return;
    }
}
