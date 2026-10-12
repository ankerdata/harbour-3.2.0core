// A .NET startup hook: with DOTNET_STARTUP_HOOKS naming this assembly, the
// runtime calls StartupHook.Initialize() before the program's Main, and every
// exception the program throws is logged as it is thrown, caught or not.
//
// Why: a transpiled program's own traceback (the Harbour error handler's
// ProcName() walk) loses the frames below a reflection call, which is how
// HbRuntime starts every thread (hb_threadStart() through FuncPtr()), so an
// error in a thread names only HbRuntime. And exceptions HbRuntime throws and
// catches itself (a conversion the DLR refuses, then retried another way) are
// invisible, costing time on every call.
//
// Each distinct exception (its type, message and first frame outside .NET)
// is written once, with the whole stack of the thread at the moment it was
// thrown; repeats are counted, and a summary sorted by count is written when
// the process exits. The log is EXCEPTIONLOG, default exceptions.log in the
// current directory.
//
//     set DOTNET_STARTUP_HOOKS=<path>\ExceptionLog.dll
//     set EXCEPTIONLOG=<path>\exceptions.log
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;

internal static class StartupHook
{
    private static readonly ConcurrentDictionary<string, Entry> Seen = new();
    private static readonly object Lock = new();
    private static string _log = "exceptions.log";

    [ThreadStatic] private static bool _inHook;

    private sealed class Entry
    {
        public int Count;
        public string Kind = "";
        public string Origin = "";
    }

    public static void Initialize()
    {
        _log = Environment.GetEnvironmentVariable("EXCEPTIONLOG") ?? "exceptions.log";
        Write($"== {DateTime.Now:yyyy-MM-dd HH:mm:ss} {Environment.ProcessPath} (pid {Environment.ProcessId})" +
              Environment.NewLine);
        AppDomain.CurrentDomain.FirstChanceException += (_, e) => Record(e.Exception);
        AppDomain.CurrentDomain.ProcessExit += (_, _) => Summary();
    }

    private static void Record(Exception exception)
    {
        if (_inHook)
        {
            return;     // an exception in here would come back here
        }
        _inHook = true;
        try
        {
            var stack = new StackTrace(2, true);
            var kind = exception.GetType().FullName + ": " + exception.Message.Replace(Environment.NewLine, " ");
            var origin = Origin(stack);
            var entry = Seen.GetOrAdd(kind + "\n" + origin, _ => new Entry { Kind = kind, Origin = origin });
            if (Interlocked.Increment(ref entry.Count) == 1)
            {
                Write($"-- {DateTime.Now:HH:mm:ss.fff} thread {Environment.CurrentManagedThreadId}: {kind}" +
                      Environment.NewLine + stack + Environment.NewLine);
            }
        }
        catch
        {
            // Logging must never change what the program does
        }
        finally
        {
            _inHook = false;
        }
    }

    // The first frame in the program or its runtime, not in .NET itself
    private static string Origin(StackTrace stack)
    {
        foreach (var frame in stack.GetFrames())
        {
            var method = frame.GetMethod();
            var assembly = method?.DeclaringType?.Assembly.GetName().Name ?? "";
            if (method == null || assembly.StartsWith("System") || assembly.StartsWith("Microsoft") ||
                assembly == "ExceptionLog" || assembly == "Anonymously Hosted DynamicMethods Assembly")
            {
                continue;
            }
            var file = frame.GetFileName();
            return $"{method.DeclaringType?.Name}.{method.Name}" +
                   (file == null ? "" : $" ({Path.GetFileName(file)}:{frame.GetFileLineNumber()})");
        }
        return "(no frame outside .NET)";
    }

    private static void Summary()
    {
        var text = new StringBuilder();
        text.AppendLine($"== summary at exit, {DateTime.Now:yyyy-MM-dd HH:mm:ss}: {Seen.Count} distinct");
        foreach (var entry in Seen.Values.OrderByDescending(e => e.Count))
        {
            text.AppendLine($"{entry.Count,8}  {entry.Kind}");
            text.AppendLine($"          at {entry.Origin}");
        }
        Write(text.ToString());
    }

    private static void Write(string text)
    {
        lock (Lock)
        {
            File.AppendAllText(_log, text);
        }
    }
}
