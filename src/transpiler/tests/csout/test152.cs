// Test 152: the scan knows Harbour's Error class, HbRuntime's HbError.
//
// `oError` held Harbour's error object everywhere (ErrorNew(), a RECOVER
// USING variable, the WITH block's parameter, the parameter of every
// error routine) and was `dynamic` everywhere: the scan knew no class
// named Error, so the name rule had nothing to type it as, `AS CLASS
// Error` was W0040, and every member read went through the binder (in
// EasiPOS, 112 declarations). hbclasses.tab, the runtime's own classes
// in the --extern= table's format and loaded with it, declares Error as
// the C# HbError with its typed members, so `oError` is an HbError by
// its name, its members are typed, and a codeblock whose parameter is
// named oError is typed too. The name is the way: Harbour's own -w3
// refuses `AS CLASS Error` (W0025, no such class declared to it).
public static partial class Program
{
    public static string Describe152(HbError oError = default)
    {
        return oError.subSystem + "/" + LTrim(Str(oError.subCode)) + ": " + oError.description;
    }

    public static HbError Make152(string cText = default)
    {
        HbError oError = ErrorNew();
        oError.subSystem = "TEST";
        oError.subCode = 152;
        oError.description = cText;
        oError.canRetry = false;
        return oError;
    }

    public static void Main(string[] args)
    {
        HbError oError = Make152("made by hand");
        Func<HbError, string> bShow = ((Func<HbError, string>)((oError) => Describe152(oError)));
        decimal nZero = 0;

        QOut(Describe152(oError));
        QOut(Eval(bShow, oError), oError.canRetry);

        try
        {
            QOut(1 / nZero);
        }
        catch (Exception __hb_ex1) when (__hb_ex1 is not HbQuit)
        {
            oError = HbError.From(__hb_ex1);
            QOut(Describe152(oError), oError.operation);
        }

        try
        {
            throw new HbBreak(Make152("broken"));
        }
        catch (HbBreak __hb_brk1)
        {
            oError = __hb_brk1.Value;
            QOut(oError.description, Len(oError.description));
        }

        return;
    }
}
