using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

// Threads and mutexes.
// One part of HbRuntime; HbRuntime.cs says what the whole is.

public static partial class HbRuntime
{
    // ---- Threading primitives ----
    // Harbour's hb_mutex* maps to a System.Threading object. We use a
    // Dictionary<object, object> of mutexes so the transpiled code can
    // hold them as dynamic.

    public static dynamic hb_mutexCreate() => new object();

    public static bool hb_mutexLock(dynamic mtx, dynamic nTimeout = null)
    {
        if (mtx is null) return false;
        if (nTimeout is null)
        {
            System.Threading.Monitor.Enter(mtx);
            return true;
        }
        // Harbour's timed lock takes a timeout in seconds; a non-positive
        // value polls once. Returns .F. if the lock wasn't acquired.
        double secs = Convert.ToDouble(nTimeout, INV);
        int ms = secs <= 0 ? 0 : (int)Math.Min(secs * 1000, int.MaxValue);
        return System.Threading.Monitor.TryEnter(mtx, ms);
    }

    public static bool hb_mutexUnlock(dynamic mtx)
    {
        if (mtx is null) return false;
        try { System.Threading.Monitor.Exit(mtx); return true; }
        catch { return false; }
    }
}
