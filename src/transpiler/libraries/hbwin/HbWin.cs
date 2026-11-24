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
// wapi_FormatMessage, Windows' text for an error code, since 2026-09-24,
// as is the performance counter pair EasiPOS's MonotonicSeconds() reads;
// wapi_GetCurrentProcessId and win_UuidCreateString since 2026-10-07.

public static partial class HbWin
{
    public static decimal wapi_Sleep(decimal nMs)
    {
        System.Threading.Thread.Sleep((int)nMs);
        return 0;
    }

    // wapi_QueryPerformanceFrequency( @nFrequency ) --> lOk and
    // wapi_QueryPerformanceCounter( @nCounter ) --> lOk: the Windows
    // high-resolution counter and its ticks per second, which is what
    // Stopwatch reads on Windows - the same values Harbour gets. Neither
    // call fails on any Windows .NET runs on.
    public static bool wapi_QueryPerformanceFrequency(ref decimal nFrequency)
    {
        nFrequency = System.Diagnostics.Stopwatch.Frequency;
        return true;
    }

    public static bool wapi_QueryPerformanceCounter(ref decimal nCounter)
    {
        nCounter = System.Diagnostics.Stopwatch.GetTimestamp();
        return true;
    }

    // wapi_GetCurrentProcessId() (wapi_winbase_2.c): this process's id, which
    // EasiPOS keeps as POSStatus's cPID
    public static long wapi_GetCurrentProcessId() => System.Environment.ProcessId;

    // win_UuidCreateString( [@<nStatus>] ) (win_rpc.c): a new UUID as
    // UuidToString() writes it, lower-case hex in 8-4-4-4-12 groups without
    // braces, which is Guid's "D" form; .NET's NewGuid() is the same
    // UuidCreate() underneath on Windows. The status out-parameter is not
    // ported: EasiPOS's GetGUID() passes none.
    public static string win_UuidCreateString() => System.Guid.NewGuid().ToString("D");

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

    // The OLE error of the last object created, per thread, as hbwin keeps
    // it (hb_oleSetError): S_OK once one is created. The C# program creates
    // its connectors itself, by name (ConnectorCreate()), and sets it as
    // win_oleCreateObject() would: CO_E_CLASSSTRING for a name it has no
    // connector for, E_FAIL for one that throws while it is made.
    [System.ThreadStatic] static int t_oleError;

    public static int OleError { get => t_oleError; set => t_oleError = value; }

    public const int S_OK = 0;
    public const int CO_E_CLASSSTRING = unchecked((int)0x800401F3);
    public const int E_FAIL = unchecked((int)0x80004005);

    static readonly Dictionary<int, string> s_oleErrorNames = new()
    {
        [unchecked((int)0x800401F3)] = "CO_E_CLASSSTRING",
        [unchecked((int)0x8004000E)] = "OLE_E_WRONGCOMPOBJ",
        [unchecked((int)0x80040154)] = "REGDB_E_CLASSNOTREG",
        [unchecked((int)0x80040151)] = "REGDB_E_WRITEREGDB",
        [unchecked((int)0x8000000A)] = "E_PENDING",
        [unchecked((int)0x80004001)] = "E_NOTIMPL",
        [unchecked((int)0x80004002)] = "E_NOINTERFACE",
        [unchecked((int)0x80004003)] = "E_POINTER",
        [unchecked((int)0x80004004)] = "E_ABORT",
        [unchecked((int)0x80004005)] = "E_FAIL",
        [unchecked((int)0x8000FFFF)] = "E_UNEXPECTED",
        [unchecked((int)0x80070005)] = "E_ACCESSDENIED",
        [unchecked((int)0x80070006)] = "E_HANDLE",
        [unchecked((int)0x8007000E)] = "E_OUTOFMEMORY",
        [unchecked((int)0x80070057)] = "E_INVALIDARG",
        [unchecked((int)0x80020001)] = "DISP_E_UNKNOWNINTERFACE",
        [unchecked((int)0x80020003)] = "DISP_E_MEMBERNOTFOUND",
        [unchecked((int)0x80020004)] = "DISP_E_PARAMNOTFOUND",
        [unchecked((int)0x80020005)] = "DISP_E_TYPEMISMATCH",
        [unchecked((int)0x80020006)] = "DISP_E_UNKNOWNNAME",
        [unchecked((int)0x80020007)] = "DISP_E_NONAMEDARGS",
        [unchecked((int)0x80020008)] = "DISP_E_BADVARTYPE",
        [unchecked((int)0x80020009)] = "DISP_E_EXCEPTION",
        [unchecked((int)0x8002000A)] = "DISP_E_OVERFLOW",
        [unchecked((int)0x8002000B)] = "DISP_E_BADINDEX",
        [unchecked((int)0x8002000C)] = "DISP_E_UNKNOWNLCID",
        [unchecked((int)0x8002000D)] = "DISP_E_ARRAYISLOCKED",
        [unchecked((int)0x8002000E)] = "DISP_E_BADPARAMCOUNT",
        [unchecked((int)0x8002000F)] = "DISP_E_PARAMNOTOPTIONAL",
        [unchecked((int)0x80020010)] = "DISP_E_BADCALLEE",
        [unchecked((int)0x80020011)] = "DISP_E_NOTACOLLECTION",
        [unchecked((int)0x80020012)] = "DISP_E_DIVBYZERO",
        [unchecked((int)0x80020013)] = "DISP_E_BUFFERTOOSMALL",
    };

    // win_oleErrorText( [<nError>] ) (olecore.c): the name of nError, or of
    // the last OLE error; "" for S_OK, and an error it has no name for as
    // 0x and eight hex digits.
    public static string win_oleErrorText(decimal? nError = null)
    {
        int iError = nError.HasValue ? unchecked((int)(long)nError.Value) : t_oleError;
        if (iError == S_OK)
            return "";
        return s_oleErrorNames.TryGetValue(iError, out string? cName) ? cName : "0x" + unchecked((uint)iError).ToString("x8");
    }

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
