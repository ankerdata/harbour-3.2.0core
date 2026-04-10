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
        HbRuntime.QOut(cLabel + "=" + HbRuntime.Str(nCount));

        return nCount;
    }

    public static void Main(string[] args)
    {
        decimal nPriv = 42;
        string cPrivName = "secret";
        decimal nPub = 100;
        string cPubName = "global";

        HbRuntime.QOut("nStatic=" + HbRuntime.Str(nStatic));
        HbRuntime.QOut("cStaticLabel=" + cStaticLabel);
        HbRuntime.QOut("nPriv=" + HbRuntime.Str(nPriv));
        HbRuntime.QOut("cPrivName=" + cPrivName);
        HbRuntime.QOut("nPub=" + HbRuntime.Str(nPub));
        HbRuntime.QOut("cPubName=" + cPubName);

        Counter();
        Counter();
        Counter();

        nStatic += 5;
        HbRuntime.QOut("nStatic=" + HbRuntime.Str(nStatic));

        nPriv += 1;
        HbRuntime.QOut("nPriv=" + HbRuntime.Str(nPriv));

        nPub -= 10;
        HbRuntime.QOut("nPub=" + HbRuntime.Str(nPub));

        return;
    }
}
