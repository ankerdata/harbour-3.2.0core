// Test 158: an error in a thread keeps its traceback.
//
// hb_threadStart() calls the thread's function through FuncPtr(), and a
// block runs through InvokeBlock(); both make the call through a compiled
// delegate, so the error block, which runs before the stack unwinds, sees
// every frame between it and the thread's start: the routine that failed
// and the one that called it, as Harbour's does. ProcName() gives
// Harbour's upper-case names and C#'s declared ones, so they are printed
// upper-cased.
public static partial class Program
{
    public static void Main(string[] args)
    {
        dynamic pThread = default;

        pThread = hb_threadStart(FuncPtr("Thr158Run"));
        hb_threadWait(pThread, 5);
        QOut("main goes on");
    }

    public static void Thr158Run()
    {
        ErrorBlock(((Func<dynamic, dynamic>)((oErr) => Thr158Report(oErr))));
        Thr158Fail();
    }

    public static void Thr158Fail()
    {
        decimal nZero = 0;
        decimal nResult = 10 / nZero;

        QOut(nResult);
    }

    public static dynamic Thr158Report(dynamic oErr = default)
    {
        decimal n = 1;

        QOut("error:", oErr.subCode);
        while (!Empty(ProcName(n)))
        {
            QOut("called from", Upper(ProcName(n)));
            n++;
        }

        __Quit();

        return null;
    }
}
