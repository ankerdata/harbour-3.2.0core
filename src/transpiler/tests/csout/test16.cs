// Test 16: STATIC, PUBLIC, and PRIVATE declarations with type inference
public static partial class Program
{
    public static decimal test16_Counter_nCount = 0;
    public static string test16_Counter_cLabel = "count";
    public static dynamic test16_nPriv;
    public static dynamic test16_cPrivName;
    public static decimal test16_Main_nStatic = 10;
    public static string test16_Main_cStaticLabel = "static";
    public static dynamic nPub;
    public static dynamic cPubName;
    public static decimal Counter()
    {
        test16_Counter_nCount = test16_Counter_nCount + 1;
        QOut(test16_Counter_cLabel + "=" + Str(test16_Counter_nCount));

        return test16_Counter_nCount;
    }

    public static void Main(string[] args)
    {
        test16_nPriv = 42;
        test16_cPrivName = "secret";
        nPub = 100;
        cPubName = "global";

        QOut("nStatic=" + Str(test16_Main_nStatic));
        QOut("cStaticLabel=" + test16_Main_cStaticLabel);
        QOut("nPriv=" + Str(test16_nPriv));
        QOut("cPrivName=" + test16_cPrivName);
        QOut("nPub=" + Str(nPub));
        QOut("cPubName=" + cPubName);

        Counter();
        Counter();
        Counter();

        test16_Main_nStatic = test16_Main_nStatic + 5;
        QOut("nStatic=" + Str(test16_Main_nStatic));

        test16_nPriv = test16_nPriv + 1;
        QOut("nPriv=" + Str(test16_nPriv));

        nPub = nPub - 10;
        QOut("nPub=" + Str(nPub));
    }
}
