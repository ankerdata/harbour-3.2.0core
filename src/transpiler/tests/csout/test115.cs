// Test 115: threads (family 10) — what the emitter does for them.
//
// THREAD STATIC was emitted as a plain static, one for every thread; it
// is [ThreadStatic] now. @Func() of a STATIC function named it unmangled,
// so HbRuntime.FuncPtr found nothing; it names <FileBase>_<Func> now, as a
// call does. A codeblock around a function Harbour returns NIL from —
// {|| hb_idleSleep( n ) } — was an expression lambda of a void call;
// HbRuntime's procedures return null now. A QUIT on a thread unwinds that
// thread alone (HbQuit), and a BEGIN SEQUENCE WITH does not take it.
public static partial class Program
{
    [ThreadStatic] public static decimal test115_snSeen = 0;
    public static void Main(string[] args)
    {
        dynamic pMtx = hb_mutexCreate();

        // each thread its own THREAD STATIC, starting from its declared value
        dynamic xGot = "none";
        test115_snSeen = 7;
        dynamic pThread = hb_threadStart(FuncPtr("test115_Thr115Bump"), pMtx);
        hb_threadWait(pThread, 5);
        hb_mutexSubscribe(pMtx, 5, ref xGot);
        QOut("thread static:", xGot, test115_snSeen);

        // a codeblock around a call Harbour returns NIL from
        pThread = hb_threadStart(((Func<dynamic>)(() => hb_idleSleep(0.01m))));
        QOut("block thread:", hb_threadWait(pThread, 5));

        // QUIT in a thread, inside BEGIN SEQUENCE WITH: the thread ends there,
        // the sequence does not take it, the program goes on
        xGot = "none";
        pThread = hb_threadStart(FuncPtr("test115_Thr115Quit"), pMtx);
        QOut("quit thread:", hb_threadWait(pThread, 5));
        hb_mutexSubscribe(pMtx, 0.2m, ref xGot);
        QOut("after quit:", xGot);
        QOut("main goes on");
    }

    public static void test115_Thr115Bump(dynamic pMtx = default)
    {
        test115_snSeen++;
        hb_mutexNotify(pMtx, test115_snSeen);
    }

    public static void test115_Thr115Quit(dynamic pMtx = default)
    {
        try
        {
            __Quit();
        }
        catch (Exception __hb_ex1) when (__hb_ex1 is not HbQuit)
        {
            hb_mutexNotify(pMtx, "recovered");
        }

        hb_mutexNotify(pMtx, "went on");
    }
}
