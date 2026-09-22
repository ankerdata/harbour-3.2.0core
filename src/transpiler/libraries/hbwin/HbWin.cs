// HbWin — the hbwin contrib library (contrib/hbwin/hbwin.hbx) for
// transpiled Harbour code. The emitter writes `HbWin.<name>(...)` for
// every name that .hbx lists. Real implementations go here; HbWin.Stubs.cs
// holds NotImplementedException stubs for the rest the application calls
// (scripts/gen_library_stubs.py in easipos-transpiled).
//
// Seeded 2026-09-16 with the four wapi_* functions that used to sit in
// HbRuntime.cs, moved unchanged. wapi_Sleep is real; the message box
// pair and wapi_OutputDebugString are console stand-ins that still
// need a real implementation. wapi_CreateMutex and wapi_GetLastError,
// EasiPOS's single-instance check, are real since 2026-09-22.

public static partial class HbWin
{
    public static decimal wapi_Sleep(decimal nMs)
    {
        System.Threading.Thread.Sleep((int)nMs);
        return 0;
    }

    // Stand-in: prints instead of showing a message box.
    public static decimal wapi_MessageBox(dynamic hWnd, string cText, string cCaption = "", decimal nType = 0)
    {
        Console.WriteLine($"[MessageBox] {cCaption}: {cText}");
        return 1;  // IDOK
    }

    // Stand-in: prints instead of showing a message box, ignores the timeout.
    public static decimal wapi_MessageBoxTimeout(dynamic hWnd, string cText, string cCaption = "", decimal nType = 0, decimal wLang = 0, decimal nMs = 0)
    {
        Console.WriteLine($"[MessageBox] {cCaption}: {cText}");
        return 1;
    }

    // Stand-in: writes to stderr instead of the Windows debugger stream.
    public static void wapi_OutputDebugString(string cText) => Console.Error.WriteLine(cText);

    // The Windows error of the last wapi_*() call, per thread, as hbwin
    // keeps it (hbwapi_SetLastError) for wapi_GetLastError(): whatever
    // else runs in between does not disturb it.
    [System.ThreadStatic] static int t_lastError;

    // wapi_GetLastError(): that error
    public static decimal wapi_GetLastError() => t_lastError;

    // wapi_CreateMutex( [<pSecurity>], [<lInitialOwner>], [<cName>] ): a
    // Windows mutex, named when cName is given. .NET's named Mutex is that
    // kernel object, so another process — the installer, a second EasiPOS,
    // Harbour or C# — sees it. Error 183, ERROR_ALREADY_EXISTS, when the
    // name was taken, which is how EasiPOS refuses a second instance; NIL
    // and the error when it cannot be had (5, access denied; 6, the name
    // belongs to another kind of object).
    public static System.Threading.Mutex? wapi_CreateMutex(object? pSecurity = null,
        bool? lInitialOwner = null, string? cName = null)
    {
        try
        {
            var mutex = new System.Threading.Mutex(lInitialOwner ?? false,
                string.IsNullOrEmpty(cName) ? null : cName, out bool lCreated);
            t_lastError = lCreated ? 0 : 183;
            return mutex;
        }
        catch (System.UnauthorizedAccessException)
        {
            t_lastError = 5;
        }
        catch (System.Threading.WaitHandleCannotBeOpenedException)
        {
            t_lastError = 6;
        }
        return null;
    }
}
