// HbWin — the hbwin contrib library (contrib/hbwin/hbwin.hbx) for
// transpiled Harbour code. The emitter writes `HbWin.<name>(...)` for
// every name that .hbx lists. Real implementations go here; HbWin.Stubs.cs
// holds NotImplementedException stubs for the rest the application calls
// (scripts/gen_library_stubs.py in easipos-transpiled).
//
// Seeded 2026-09-16 with the four wapi_* functions that used to sit in
// HbRuntime.cs, moved unchanged. wapi_Sleep is real; the message box
// pair and wapi_OutputDebugString are console stand-ins that still
// need a real implementation.

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
}
