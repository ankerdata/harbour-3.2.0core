// Test 7: #define constants, #include directives
public static partial class Program
{
    public static void Main(string[] args)
    {
        string cVersion = Test7PrgConst.APP_VERSION;
        QOut("cVersion=" + cVersion);

        return;
    }
}
