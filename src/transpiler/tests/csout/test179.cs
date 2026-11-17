// Test 179: `@Name()` names its function to the C# compiler. One the
// program defines is FuncPtr( nameof( Name ) ), so a reference to a
// function that is not there fails the build; one of the runtime
// (`@Upper()`) is looked up in HbRuntime, where FuncPtr() by a name alone
// looks only in the program and found nothing.
public static partial class Program
{
    public static void Main(string[] args)
    {
        dynamic fOwn = FuncPtr(nameof(Double179));
        dynamic fCore = FuncPtr(typeof(HbRuntime), nameof(HbRuntime.Upper));

        QOut(Eval(fOwn, 4), Eval(fCore, "abc"), Apply179(FuncPtr(nameof(Double179)), 5));
    }

    public static decimal Double179(decimal nValue = default)
    {
        return nValue * 2;
    }

    public static dynamic Apply179(dynamic fAny = default, decimal nValue = default)
    {
        return Eval(fAny, nValue);
    }
}
