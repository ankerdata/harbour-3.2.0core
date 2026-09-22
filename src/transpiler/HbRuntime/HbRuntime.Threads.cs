using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;

// Threads and mutexes (family 10): ports of vm/thread.c as EasiPOS uses it.
// One part of HbRuntime; HbRuntime.cs says what the whole is.
//
// A thread hb_threadStart() starts is a background .NET thread — it ends
// with the program, as Harbour's end with the main thread — whose body
// runs through RunEntry, so a runtime error goes to that thread's error
// block and a stray BREAK or a QUIT ends that thread alone. It starts with
// a copy of its parent's SETs and no error block, as Harbour's does
// (hb_threadStateClone; __HBVMINIT gives each thread ErrorSys()'s block).
// A quit request (hb_threadQuitRequest) is honoured where the thread next
// waits in HbRuntime — hb_idleSleep, a mutex, hb_threadWait — which is as
// close as C# gets to Harbour's check between two instructions.

// A thread's handle: what hb_threadStart() and hb_threadSelf() return.
public sealed class HbThread
{
    internal HbThread(long nId) { this.nId = nId; }

    internal readonly long nId;             // hb_threadID(): creation order, main 1
    internal volatile bool lQuitRequested;  // hb_threadQuitRequest()
    internal volatile bool lFinished;

    public override string ToString() => "HbThread " + nId;
}

// QUIT: unwinds the thread — ALWAYS blocks run on the way — and ends it,
// the program when it is the main thread (hb_vmRequestQuit). No BEGIN
// SEQUENCE takes it.
public sealed class HbQuit : Exception
{
    public HbQuit() : base("QUIT") { }
}

// A Harbour mutex (hb_mutexCreate): a lock the thread holding it may take
// again, and a queue of notifications, hb_mutexNotify() adding to it and
// hb_mutexSubscribe() taking the oldest, waiting for one if it is empty.
public sealed class HbMutex
{
    internal readonly object sync = new();      // guards the rest; its Wait/Pulse is the condition
    internal Thread? owner;
    internal int nCount;                        // how often the owner holds it
    internal readonly Queue<object?> events = new();

    public override string ToString() => "HbMutex";
}

public static partial class HbRuntime
{
    // hvm.c s_threadNo: the last number given out
    static long s_threadNo;

    // hb_threadWait()'s condition: pulsed when a thread finishes
    static readonly object s_threadSync = new();

    [ThreadStatic] static HbThread? t_self;

    // The calling thread's handle. A thread hb_threadStart() did not start
    // is numbered the first time it is asked for; RunEntry and
    // hb_threadStart() ask on the main thread before anything runs, so it
    // is 1.
    static HbThread Self => t_self ??= new HbThread(Interlocked.Increment(ref s_threadNo));

    // A wait's slices: a quit request is noticed within one
    const int QuitSliceMs = 50;

    // hb_vmRequestQuery(): a quit request for this thread ends it here
    static void QuitCheck()
    {
        if (t_self is { lQuitRequested: true })
            throw new HbQuit();
    }

    // A Harbour timeout, in seconds, as milliseconds: NIL waits for ever
    // (-1), none or a negative one not at all
    static int TimeoutMs(object? nTimeout)
    {
        if (nTimeout is null || !IsNumeric(nTimeout))
            return Timeout.Infinite;
        double dSecs = Convert.ToDouble(nTimeout, INV);
        return dSecs <= 0 ? 0 : (int) Math.Min(dSecs * 1000, int.MaxValue - 1);
    }

    // Monitor.Wait on sync until done() or the timeout, in slices so a
    // quit request is seen; true when done() came true
    static bool WaitUntil(object sync, Func<bool> done, int nMs)
    {
        long nEnd = nMs == Timeout.Infinite ? long.MaxValue : Environment.TickCount64 + nMs;
        while (!done())
        {
            long nLeft = nEnd - Environment.TickCount64;
            if (nLeft <= 0)
                return false;
            Monitor.Wait(sync, (int) Math.Min(nLeft, QuitSliceMs));
            QuitCheck();
        }
        return true;
    }

    // ---- Threads ----

    // hb_threadStart( [<nAttrs>,] <@Func()> | <bBlock> | <cFunc> [, <params,...>] ):
    // the new thread's handle, NIL for nothing to start. The attributes
    // choose which memvars the thread inherits; C# has none to hand on.
    public static HbThread? hb_threadStart(params dynamic[] args)
    {
        int i = args.Length > 0 && IsNumeric((object) args[0]) ? 1 : 0;
        if (i >= args.Length)
            return null;
        object xStart = args[i];
        dynamic[] aParams = args[(i + 1)..];
        Delegate? start = xStart switch
        {
            Delegate d => d,
            string cFunc => FuncPtr(cFunc),
            _ => null,
        };
        if (start == null)
            return null;

        _ = Self;                                   // the parent is numbered first
        var sets = CloneSets();
        var h = new HbThread(Interlocked.Increment(ref s_threadNo));
        var thread = new Thread(() =>
        {
            t_self = h;
            t_sets = sets;
            try
            {
                RunEntry(() => InvokeBlock(start, aParams), true);
            }
            finally
            {
                lock (s_threadSync)
                {
                    h.lFinished = true;
                    Monitor.PulseAll(s_threadSync);
                }
            }
        })
        {
            IsBackground = true,
            Name = "Harbour thread " + h.nId,
        };
        thread.Start();
        return h;
    }

    // hb_threadSelf(): the calling thread's handle
    public static HbThread hb_threadSelf() => Self;

    // hb_threadID( [<pThread>] ): a thread's number, the calling thread's
    // by default; 0 for anything that is not a thread
    public static decimal hb_threadID() => Self.nId;
    public static decimal hb_threadID(object? pThread) =>
        pThread is HbThread h ? h.nId : pThread is null ? Self.nId : 0;

    // hb_threadWait( <pThread> | <aThreads>, [<nTimeout>], [<lAll>] ): wait
    // for one (or, with lAll, every) thread to finish. The 1-based index of
    // the first finished, or with lAll how many finished; 0 on a timeout.
    public static decimal hb_threadWait(object? xThreads, object? nTimeout = null, bool lAll = false)
    {
        HbThread[] aThreads = xThreads switch
        {
            HbThread h => new[] { h },
            System.Array a => a.OfType<HbThread>().ToArray(),
            _ => System.Array.Empty<HbThread>(),
        };
        if (aThreads.Length == 0)
            return 0;

        int nResult = 0, nFinished = 0;
        bool Count()
        {
            nFinished = 0;
            nResult = 0;
            for (int i = 0; i < aThreads.Length; i++)
            {
                if (aThreads[i].lFinished)
                {
                    nFinished++;
                    if (!lAll)
                    {
                        nResult = i + 1;
                        break;
                    }
                }
            }
            return nFinished >= (lAll ? aThreads.Length : 1);
        }
        lock (s_threadSync)
            WaitUntil(s_threadSync, Count, TimeoutMs(nTimeout));
        return lAll ? nFinished : nResult;
    }

    // hb_threadQuitRequest( <pThread> ): ask a running thread to end. .T.
    // when it was running; it ends where it next waits in HbRuntime.
    public static bool hb_threadQuitRequest(object? pThread)
    {
        if (pThread is not HbThread h || h.lFinished)
            return false;
        h.lQuitRequested = true;
        return true;
    }

    // ---- Mutexes ----

    public static HbMutex hb_mutexCreate() => new HbMutex();

    // hb_mutexLock( <pMtx>, [<nTimeout>] ): take the lock, which the thread
    // holding it takes again; with a timeout in seconds, .F. when it passed
    public static bool hb_mutexLock(object? pMtx, object? nTimeout = null)
    {
        if (pMtx is not HbMutex m)
            return false;
        Thread me = Thread.CurrentThread;
        lock (m.sync)
        {
            if (!WaitUntil(m.sync, () => m.owner == null || m.owner == me, TimeoutMs(nTimeout)))
                return false;
            m.owner = me;
            m.nCount++;
            return true;
        }
    }

    // hb_mutexUnlock( <pMtx> ): give up one hold of the lock; .F. when the
    // calling thread does not hold it
    public static bool hb_mutexUnlock(object? pMtx)
    {
        if (pMtx is not HbMutex m)
            return false;
        lock (m.sync)
        {
            if (m.owner != Thread.CurrentThread)
                return false;
            if (--m.nCount == 0)
            {
                m.owner = null;
                Monitor.PulseAll(m.sync);
            }
            return true;
        }
    }

    // hb_mutexNotify( <pMtx>, [<xValue>] ): queue a notification — whether
    // or not a thread is waiting for one — and wake a waiting subscriber
    public static object? hb_mutexNotify(object? pMtx, object? xValue = null)
    {
        if (pMtx is HbMutex m)
        {
            lock (m.sync)
            {
                m.events.Enqueue(xValue);
                Monitor.PulseAll(m.sync);
            }
        }
        return null;
    }

    // hb_mutexSubscribe( <pMtx>, [<nTimeout>], [@<xValue>] ): wait for a
    // notification, the oldest first, and put its value in xValue. .T. when
    // one came, .F. on the timeout (seconds). A thread holding the lock
    // gives it up while it waits and takes it back after, as a condition
    // variable does (thread.c hb_threadMutexSubscribe).
    public static bool hb_mutexSubscribe<T>(object? pMtx, object? nTimeout, ref T xValue)
    {
        if (pMtx is not HbMutex m)
            return false;
        Thread me = Thread.CurrentThread;
        lock (m.sync)
        {
            int nHeld = 0;
            if (m.owner == me)
            {
                nHeld = m.nCount;
                m.owner = null;
                m.nCount = 0;
                Monitor.PulseAll(m.sync);
            }
            try
            {
                if (!WaitUntil(m.sync, () => m.events.Count > 0, TimeoutMs(nTimeout)))
                    return false;
                object? xEvent = m.events.Dequeue();
                xValue = xEvent is null ? default! : (T) xEvent;
                return true;
            }
            finally
            {
                if (nHeld > 0)
                {
                    while (m.owner != null)
                        Monitor.Wait(m.sync);
                    m.owner = me;
                    m.nCount = nHeld;
                }
            }
        }
    }

    public static bool hb_mutexSubscribe(object? pMtx, object? nTimeout = null)
    {
        object? xIgnored = null;
        return hb_mutexSubscribe(pMtx, nTimeout, ref xIgnored);
    }
}
