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
// EasiPOS's single-instance check, are real since 2026-09-22, and
// wapi_FormatMessage, Windows' text for an error code, since 2026-09-24.

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

    // wapi_FormatMessage( [<nFlags>], [<cSource>], [<nMessageId>], [<nLanguageId>],
    //                     @<cBuffer> ) -> nChars (wapi_winbase_2.c): the text Windows
    // has for a message id, in cBuffer; the number of characters, or 0 and ""
    // when it has none. As hbwin does it: FORMAT_MESSAGE_FROM_SYSTEM unless told
    // otherwise, the last wapi_*() error when no id is given, and room for as many
    // characters as cBuffer holds — EasiPOS's win_ErrorDesc() passes 2048 spaces.
    // A message source (cSource) is not supported: EasiPOS passes none.
    public static decimal wapi_FormatMessage(object? nFlags, object? cSource,
        object? nMessageId, object? nLanguageId, ref string cBuffer)
    {
        const uint FromSystem = 0x00001000, AllocateBuffer = 0x00000100;
        const uint NeutralDefaultLang = 0x0400;     // MAKELANGID(LANG_NEUTRAL, SUBLANG_DEFAULT)

        uint nFlagsIn = nFlags is null ? FromSystem : System.Convert.ToUInt32(nFlags);
        uint nId = nMessageId is null ? (uint) t_lastError : System.Convert.ToUInt32(nMessageId);
        uint nLang = nLanguageId is null ? NeutralDefaultLang : System.Convert.ToUInt32(nLanguageId);
        // hbwin asks Windows to allocate only when the buffer given is empty;
        // here there is always a buffer of our own, so that flag does nothing.
        int nSize = string.IsNullOrEmpty(cBuffer) ? 65536 : cBuffer.Length;
        var aBuffer = new char[nSize];

        uint nChars = FormatMessageW(nFlagsIn & ~AllocateBuffer, System.IntPtr.Zero, nId, nLang,
                                     aBuffer, (uint) nSize, System.IntPtr.Zero);
        t_lastError = nChars == 0 ? System.Runtime.InteropServices.Marshal.GetLastPInvokeError() : 0;
        cBuffer = nChars == 0 ? "" : new string(aBuffer, 0, (int) nChars);
        return nChars;
    }

    [System.Runtime.InteropServices.DllImport("kernel32.dll",
        CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
    static extern uint FormatMessageW(uint dwFlags, System.IntPtr lpSource, uint dwMessageId,
        uint dwLanguageId, char[] lpBuffer, uint nSize, System.IntPtr Arguments);
}
