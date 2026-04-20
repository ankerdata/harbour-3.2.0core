using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

/// <summary>
/// Harbour runtime function implementations for transpiled C# code.
/// All Harbour builtin functions are available as static methods.
///
/// Method names are UPPERCASE to match the Harbour convention and the
/// canonical form in src/transpiler/hbfuncs.tab. The C# transpiler
/// uppercases every HbRuntime-remapped call at emit time (see
/// hb_csFuncMap in gencsharp.c), so source-level casing variations like
/// `Int()`, `int()`, `Int()` all resolve to the same method here.
/// </summary>
public static partial class HbRuntime
{
    static readonly CultureInfo INV = CultureInfo.InvariantCulture;

    // ---- Codeblock dispatch ----
    // Harbour codeblocks emit as Func<dynamic[], dynamic>, so Eval just
    // packs the trailing args into the array and invokes. Delegate
    // fallback covers blocks that survived as a plain delegate (e.g.
    // typed fixed-arity lambdas) via DynamicInvoke.
    public static dynamic Eval(dynamic block, params dynamic[] args)
    {
        if (block is Func<dynamic[], dynamic> fa) return fa(args);
        if (block is Delegate d) return d.DynamicInvoke(args);
        return null;
    }

    // ---- Console output (? and ??) ----

    // Harbour's `?` / `??` commands separate comma-delimited args
    // with a single space in the output. "hi", n prints as "hi 3"
    // (space between label and value), not "hi3".
    public static void QOut(params dynamic[] args)
    {
        Console.WriteLine();
        for (int i = 0; i < args.Length; i++)
        {
            if (i > 0) Console.Write(' ');
            Console.Write(Fmt(args[i]));
        }
    }

    public static void QQOut(params dynamic[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            if (i > 0) Console.Write(' ');
            Console.Write(Fmt(args[i]));
        }
    }

    static string Fmt(dynamic a) =>
        a is decimal d ? Str(d) : Convert.ToString(a, INV);

    // ---- Numeric functions ----

    public static string Str(decimal? nOrNull, decimal nWidth = 10, decimal nDec = -1)
    {
        // Accepts nullable decimals so transpiled code can pass parameters
        // marked nilable in the by-ref table without an explicit cast.
        // NIL in the source becomes null here, which we render as "".
        int iWidth = (int)nWidth;
        int iDec = (int)nDec;
        if (nOrNull is null)
            return "".PadLeft(iWidth);
        decimal n = nOrNull.Value;
        string s;
        if (iDec >= 0)
            s = n.ToString("F" + iDec, INV);
        else if (n == Math.Truncate(n))
            s = n.ToString("F0", INV);
        else
            s = n.ToString("G", INV);
        return s.PadLeft(iWidth);
    }

    public static decimal Val(string s)
    {
        decimal.TryParse(s, NumberStyles.Any, INV, out decimal result);
        return result;
    }

    public static decimal Int(decimal n) => Math.Truncate(n);
    public static decimal Round(decimal n, decimal nDec = 0) => Math.Round(n, (int)nDec);
    public static decimal Abs(decimal n) => Math.Abs(n);
    public static decimal Max(decimal a, decimal b) => Math.Max(a, b);
    public static decimal Min(decimal a, decimal b) => Math.Min(a, b);
    public static decimal Mod(decimal a, decimal b) => a % b;
    public static decimal Sqrt(decimal n) => (decimal)Math.Sqrt((double)n);
    public static decimal Log(decimal n) => (decimal)Math.Log((double)n);
    public static decimal Exp(decimal n) => (decimal)Math.Exp((double)n);

    // ---- String functions ----

    public static decimal Len(dynamic x)
    {
        if (x is string s) return s.Length;
        if (x is Array a) return a.Length;
        return 0;
    }

    public static string Upper(string s) => s.ToUpper();
    public static string Lower(string s) => s.ToLower();
    public static string Trim(string s) => s.TrimEnd();
    public static string RTrim(string s) => s.TrimEnd();
    public static string LTrim(string s) => s.TrimStart();
    public static string AllTrim(string s) => s.Trim();
    public static string Space(decimal n) => new string(' ', (int)n);
    public static string Replicate(string s, decimal n) => string.Concat(System.Linq.Enumerable.Repeat(s, (int)n));
    public static string Chr(decimal n) => ((char)(int)n).ToString();
    public static decimal Asc(string s) => s.Length > 0 ? (decimal)s[0] : 0;

    public static string SubStr(string s, decimal nStart, decimal nLen = -1)
    {
        // Harbour is 1-based
        int iStart = Math.Max((int)nStart - 1, 0);
        int iLen = (int)nLen;
        if (iStart >= s.Length) return "";
        if (iLen < 0) return s.Substring(iStart);
        iLen = Math.Min(iLen, s.Length - iStart);
        return s.Substring(iStart, iLen);
    }

    public static string Left(string s, decimal n) => SubStr(s, 1, n);
    public static string Right(string s, decimal n) { int i = (int)n; return i >= s.Length ? s : s.Substring(s.Length - i); }

    public static decimal At(string cSearch, string cString) =>
        (decimal)(cString.IndexOf(cSearch, StringComparison.Ordinal) + 1);

    public static decimal RAt(string cSearch, string cString) =>
        (decimal)(cString.LastIndexOf(cSearch, StringComparison.Ordinal) + 1);

    public static string PadL(string s, decimal nLen, string cFill = " ") =>
        s.Length >= (int)nLen ? s : s.PadLeft((int)nLen, cFill[0]);

    public static string PadR(string s, decimal nLen, string cFill = " ") =>
        s.Length >= (int)nLen ? s : s.PadRight((int)nLen, cFill[0]);

    public static string PadC(string s, decimal nLen, string cFill = " ")
    {
        int iLen = (int)nLen;
        if (s.Length >= iLen) return s;
        int nLeft = (iLen - s.Length) / 2;
        return s.PadLeft(s.Length + nLeft, cFill[0]).PadRight(iLen, cFill[0]);
    }

    public static string StrTran(string cString, string cSearch, string cReplace = "") =>
        cString.Replace(cSearch, cReplace);

    public static string Stuff(string cString, decimal nStart, decimal nDel, string cInsert)
    {
        int iStart = Math.Max((int)nStart - 1, 0);
        int iDel = (int)nDel;
        if (iStart >= cString.Length) return cString + cInsert;
        return cString.Substring(0, iStart) + cInsert + cString.Substring(Math.Min(iStart + iDel, cString.Length));
    }

    public static string Transform(dynamic val, string cMask) => Convert.ToString(val, INV);
    public static string StrZero(decimal n, decimal nLen = 10, decimal nDec = 0) =>
        n.ToString("F" + (int)nDec, INV).PadLeft((int)nLen, '0');

    // ---- Logical functions ----

    public static bool Empty(dynamic val)
    {
        if (val == null) return true;
        if (val is decimal d) return d == 0;
        if (val is string s) return s.Trim().Length == 0;
        if (val is bool b) return !b;
        if (val is Array a) return a.Length == 0;
        return false;
    }

    public static bool IsDigit(string s) => s.Length > 0 && char.IsDigit(s[0]);
    public static bool IsAlpha(string s) => s.Length > 0 && char.IsLetter(s[0]);
    public static bool IsUpper(string s) => s.Length > 0 && char.IsUpper(s[0]);
    public static bool IsLower(string s) => s.Length > 0 && char.IsLower(s[0]);

    // ---- Date functions ----
    // Harbour Date → C# DateOnly (no time component). Harbour TIMESTAMP
    // maps to C# DateTime via the transpiler's type map.

    public static DateOnly Date() => DateOnly.FromDateTime(DateTime.Today);
    public static DateOnly CToD(string s) { DateTime.TryParse(s, out DateTime d); return DateOnly.FromDateTime(d); }
    public static DateOnly SToD(string s) { DateTime.TryParseExact(s, "yyyyMMdd", INV, DateTimeStyles.None, out DateTime d); return DateOnly.FromDateTime(d); }
    public static string DToC(DateOnly d) => d.ToString("MM/dd/yyyy", INV);
    public static string DToS(DateOnly d) => d.ToString("yyyyMMdd", INV);
    public static string Time() => DateTime.Now.ToString("HH:mm:ss", INV);

    // ---- Terminal ----

    public static void SetColor(string cColor) { }

    // ---- Array functions ----
    // Harbour arrays map to C# dynamic[] or List<dynamic>. Because dynamic[]
    // can't be resized, the transpiler-generated code mostly uses List-like
    // behavior — but the source still writes `aadd()`, `asize()` etc. against
    // whichever shape the variable happens to be. These helpers accept
    // dynamic and dispatch on the runtime shape: List<dynamic> resizes in
    // place; arrays are rebuilt into a new array and returned.

    public static dynamic AAdd(dynamic arr, dynamic val)
    {
        if (arr is List<dynamic> list) { list.Add(val); return val; }
        return val;
    }

    public static dynamic ASize(dynamic arr, decimal nLen)
    {
        int n = (int)nLen;
        if (arr is List<dynamic> list)
        {
            while (list.Count > n) list.RemoveAt(list.Count - 1);
            while (list.Count < n) list.Add(null);
        }
        return arr;
    }

    public static dynamic AClone(dynamic arr)
    {
        if (arr is List<dynamic> list) return new List<dynamic>(list);
        if (arr is Array a) { var copy = (Array)a.Clone(); return copy; }
        return arr;
    }

    public static decimal AScan(dynamic arr, dynamic val)
    {
        if (arr is List<dynamic> list)
        {
            for (int i = 0; i < list.Count; i++)
                if (Equals(list[i], val)) return i + 1;
        }
        return 0;
    }

    public static dynamic AEval(dynamic arr, dynamic block)
    {
        if (arr is List<dynamic> list)
        {
            for (int i = 0; i < list.Count; i++)
                Eval(block, list[i], (decimal)(i + 1));
        }
        return arr;
    }

    public static dynamic ADel(dynamic arr, decimal nPos)
    {
        if (arr is List<dynamic> list)
        {
            int i = (int)nPos - 1;
            if (i >= 0 && i < list.Count) { list.RemoveAt(i); list.Add(null); }
        }
        return arr;
    }

    public static dynamic AIns(dynamic arr, decimal nPos)
    {
        if (arr is List<dynamic> list)
        {
            int i = (int)nPos - 1;
            if (i >= 0 && i < list.Count) { list.Insert(i, null); list.RemoveAt(list.Count - 1); }
        }
        return arr;
    }

    // ---- Type inspection ----

    public static string ValType(dynamic x)
    {
        if (x is null) return "U";
        if (x is string) return "C";
        if (x is decimal || x is int || x is long || x is double || x is float) return "N";
        if (x is bool) return "L";
        if (x is DateOnly || x is DateTime) return "D";
        if (x is List<dynamic> || x is Array) return "A";
        if (x is Delegate) return "B";
        if (x is System.Collections.IDictionary) return "H";
        return "O";
    }

    public static decimal PCount() => 0;  // varargs path uses hbva.Length directly

    // ---- Defaults helper (hb_default) ----
    // Harbour's hb_default( @var, val ) sets var to val if NIL. Without
    // by-ref pass-through we return the non-null one of the two.

    /* Harbour's hb_default( @var, val ) mutates `var` in place when it
       is NIL, leaving a non-NIL value untouched. The emitter emits
       `ref var` at every call site (because the PRG uses `@var`).

       Generic `ref T` is required because C# `ref` is invariant — a
       `ref decimal` won't bind to `ref dynamic`, and most emitted
       call sites have concrete types for the by-ref parameter. The
       null check only fires for reference / nullable types; for
       non-nullable value types it's trivially false and the call is
       a no-op. That's a behaviour regression against Harbour (where
       an omitted NIL parameter would pick up the default) but the
       emitter already initialises such parameters with `= default`,
       so the common case still compiles and runs. A fuller fix
       would inline the default at the call site based on the param's
       nilable flag — leaving that for when it actually bites. */
    public static void HB_DEFAULT<T>(ref T val, T defVal)
    {
        if (val is null) val = defVal;
    }

    // ---- Time ----

    public static decimal Seconds() =>
        (decimal)(DateTime.Now - DateTime.Today).TotalSeconds;

    // ---- File I/O ----
    // Very simple handle table mapping decimal -> FileStream. Real Harbour
    // returns -1 on failure; FError() returns the last OS errno. We only
    // track "did the last op fail" as 0 / 1.

    static readonly Dictionary<int, FileStream> s_files = new();
    static int s_nextHandle = 1;
    static int s_lastError = 0;

    public static decimal FOpen(string cFile, decimal nMode = 0)
    {
        try
        {
            FileAccess access = ((int)nMode & 2) != 0 ? FileAccess.ReadWrite
                              : ((int)nMode & 1) != 0 ? FileAccess.Write
                                                      : FileAccess.Read;
            var fs = new FileStream(cFile, FileMode.Open, access, FileShare.ReadWrite);
            int h = s_nextHandle++;
            s_files[h] = fs;
            s_lastError = 0;
            return h;
        }
        catch { s_lastError = 1; return -1; }
    }

    public static decimal FCreate(string cFile, decimal nAttr = 0)
    {
        try
        {
            var fs = new FileStream(cFile, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite);
            int h = s_nextHandle++;
            s_files[h] = fs;
            s_lastError = 0;
            return h;
        }
        catch { s_lastError = 1; return -1; }
    }

    public static bool FClose(decimal nHandle)
    {
        if (s_files.TryGetValue((int)nHandle, out var fs))
        {
            fs.Dispose();
            s_files.Remove((int)nHandle);
            return true;
        }
        return false;
    }

    public static decimal FRead(decimal nHandle, ref string cBuf, decimal nBytes)
    {
        if (!s_files.TryGetValue((int)nHandle, out var fs)) { s_lastError = 1; return 0; }
        byte[] b = new byte[(int)nBytes];
        int read = fs.Read(b, 0, b.Length);
        cBuf = System.Text.Encoding.UTF8.GetString(b, 0, read);
        return read;
    }

    public static decimal FWrite(decimal nHandle, string cBuf, decimal nBytes = -1)
    {
        if (!s_files.TryGetValue((int)nHandle, out var fs)) { s_lastError = 1; return 0; }
        byte[] b = System.Text.Encoding.UTF8.GetBytes(cBuf);
        int n = (int)nBytes < 0 ? b.Length : Math.Min((int)nBytes, b.Length);
        fs.Write(b, 0, n);
        return n;
    }

    public static decimal FSeek(decimal nHandle, decimal nOffset, decimal nOrigin = 0)
    {
        if (!s_files.TryGetValue((int)nHandle, out var fs)) { s_lastError = 1; return 0; }
        SeekOrigin o = (int)nOrigin switch
        {
            1 => SeekOrigin.Current,
            2 => SeekOrigin.End,
            _ => SeekOrigin.Begin,
        };
        return (decimal)fs.Seek((long)nOffset, o);
    }

    public static decimal FError() => s_lastError;

    public static bool File(string cFile) => System.IO.File.Exists(cFile);

    public static string MemoRead(string cFile)
    {
        try { return System.IO.File.ReadAllText(cFile); }
        catch { return ""; }
    }

    public static bool MemoWrit(string cFile, string cData)
    {
        try { System.IO.File.WriteAllText(cFile, cData); return true; }
        catch { return false; }
    }

    // ---- Date decomposition ----

    static DateTime AsDT(dynamic d) => d is DateOnly dd ? dd.ToDateTime(TimeOnly.MinValue)
                                     : d is DateTime dt ? dt
                                     : default;
    public static decimal Year(dynamic d) => AsDT(d).Year;
    public static decimal Month(dynamic d) => AsDT(d).Month;
    public static decimal Day(dynamic d) => AsDT(d).Day;
    public static decimal DoW(dynamic d) => (decimal)((int)AsDT(d).DayOfWeek + 1);
    public static string CDoW(dynamic d) => AsDT(d).ToString("dddd", INV);
    public static string CMonth(dynamic d) => AsDT(d).ToString("MMMM", INV);

    // ---- Type predicates ----

    public static bool ISNIL(dynamic x) => x is null;
    public static bool ISCHARACTER(dynamic x) => x is string;
    public static bool ISNUMBER(dynamic x) => x is decimal or int or long or double or float;
    public static bool ISLOGICAL(dynamic x) => x is bool;
    public static bool ISDATE(dynamic x) => x is DateOnly or DateTime;
    public static bool ISARRAY(dynamic x) => x is Array or List<dynamic>;
    public static bool ISHASH(dynamic x) => x is System.Collections.IDictionary;
    public static bool ISOBJECT(dynamic x) =>
        x is not null && !(x is string or bool or decimal or int or long or double or float
                          or DateOnly or DateTime or Array or List<dynamic> or Delegate
                          or System.Collections.IDictionary);
    public static bool ISBLOCK(dynamic x) => x is Delegate;

    // ---- Hash helpers ----

    static System.Collections.IDictionary AsDict(dynamic h) => h as System.Collections.IDictionary;
    public static dynamic hb_HGetDef(dynamic h, dynamic key, dynamic def = null)
    {
        var d = AsDict(h);
        return (d != null && d.Contains(key)) ? d[key] : def;
    }
    public static bool hb_HHasKey(dynamic h, dynamic key)
    {
        var d = AsDict(h);
        return d != null && d.Contains(key);
    }
    public static decimal hb_HDel(dynamic h, dynamic key)
    {
        var d = AsDict(h);
        if (d != null && d.Contains(key)) d.Remove(key);
        return 0;
    }
    public static dynamic hb_HKeys(dynamic h)
    {
        var r = new List<dynamic>();
        var d = AsDict(h);
        if (d != null) foreach (var k in d.Keys) r.Add(k);
        return r;
    }
    public static dynamic hb_HValues(dynamic h)
    {
        var r = new List<dynamic>();
        var d = AsDict(h);
        if (d != null) foreach (var v in d.Values) r.Add(v);
        return r;
    }

    // ---- Array extras ----

    public static dynamic AFill(dynamic arr, dynamic val)
    {
        if (arr is List<dynamic> list) for (int i = 0; i < list.Count; i++) list[i] = val;
        return arr;
    }

    public static dynamic ASort(dynamic arr, dynamic nStart = null, dynamic nCount = null, dynamic block = null)
    {
        if (arr is List<dynamic> list)
        {
            if (block is Delegate)
                list.Sort((a, b) => ((bool)Eval(block, a, b)) ? -1 : 1);
            else
                list.Sort((a, b) => Comparer<dynamic>.Default.Compare(a, b));
        }
        return arr;
    }

    // ---- Harbour header constants ----
    // fileio.ch
    public const decimal FC_NORMAL = 0;
    public const decimal FC_READONLY = 1;
    public const decimal FC_HIDDEN = 2;
    public const decimal FC_SYSTEM = 4;
    public const decimal FO_READ = 0;
    public const decimal FO_WRITE = 1;
    public const decimal FO_READWRITE = 2;
    public const decimal FO_COMPAT = 0;
    public const decimal FO_EXCLUSIVE = 16;
    public const decimal FO_DENYWRITE = 32;
    public const decimal FO_DENYREAD = 48;
    public const decimal FO_DENYNONE = 64;
    public const decimal FO_SHARED = 64;
    public const decimal FS_SET = 0;
    public const decimal FS_RELATIVE = 1;
    public const decimal FS_END = 2;
    // directry.ch
    public const decimal F_NAME = 1;
    public const decimal F_SIZE = 2;
    public const decimal F_DATE = 3;
    public const decimal F_TIME = 4;
    public const decimal F_ATTR = 5;
    public const decimal F_LEN = 5;
    public const decimal F_ERROR = -1;
    // set.ch — only the ones actually used so far
    public const decimal _SET_EXACT = 1;
    public const decimal _SET_FIXED = 2;
    public const decimal _SET_DECIMALS = 3;
    public const decimal _SET_DATEFORMAT = 4;
    public const decimal _SET_EPOCH = 5;
    public const decimal _SET_PATH = 6;
    public const decimal _SET_DEFAULT = 7;
    public const decimal _SET_CENTURY = 48;
    // common.ch
    public const string CRLF = "\r\n";

    // ---- Set() ----
    // Harbour's Set( _SET_XXX [, newVal] ) reads/writes a setting. Keyed on
    // the numeric _SET_* identifier. Returns the prior value.

    static readonly Dictionary<int, dynamic> s_sets = new()
    {
        [(int)_SET_EXACT]      = false,
        [(int)_SET_FIXED]      = false,
        [(int)_SET_DECIMALS]   = 2m,
        [(int)_SET_DATEFORMAT] = "mm/dd/yyyy",
        [(int)_SET_EPOCH]      = 1900m,
        [(int)_SET_PATH]       = "",
        [(int)_SET_DEFAULT]    = "",
        [(int)_SET_CENTURY]    = false,
    };

    public static dynamic Set(decimal nSet, dynamic newVal = null)
    {
        int key = (int)nSet;
        s_sets.TryGetValue(key, out dynamic prev);
        if (newVal is not null)
        {
            /* Normalize "ON"/"OFF" → bool when the existing slot is
               bool-typed. Harbour's `Set EXACT ON` lexer route STRSMARTs
               the keyword into a string literal that Set() receives. */
            if (prev is bool && newVal is string)
                s_sets[key] = AsOnOff(newVal);
            else
                s_sets[key] = newVal;
        }
        return prev;
    }

    /* Harbour's Set() / __SetCentury() accept either a bool or the
       strings "ON"/"OFF" (because the #command rules for Set ... ON/OFF
       STRSMART the keyword into a string literal). Normalize to bool. */
    static bool AsOnOff(dynamic val, bool fallback = false)
    {
        if (val is null) return fallback;
        if (val is bool b) return b;
        if (val is string s) return string.Equals(s, "ON", StringComparison.OrdinalIgnoreCase);
        return fallback;
    }

    public static bool __SetCentury(dynamic val = null)
    {
        bool prev = s_sets[(int)_SET_CENTURY] is bool pb && pb;
        if (val is not null) s_sets[(int)_SET_CENTURY] = AsOnOff(val);
        return prev;
    }

    // ---- Threading primitives ----
    // Harbour's hb_mutex* maps to a System.Threading object. We use a
    // Dictionary<object, object> of mutexes so the transpiled code can
    // hold them as dynamic.

    public static dynamic hb_mutexCreate() => new object();

    public static bool hb_mutexLock(dynamic mtx, dynamic nTimeout = null)
    {
        if (mtx is null) return false;
        System.Threading.Monitor.Enter(mtx);
        return true;
    }

    public static bool hb_mutexUnlock(dynamic mtx)
    {
        if (mtx is null) return false;
        try { System.Threading.Monitor.Exit(mtx); return true; }
        catch { return false; }
    }

    // ---- wapi_* stubs ----
    // Windows-specific. On non-Windows they degrade to Console output /
    // Thread.Sleep. Enough for transpiled code to compile and run
    // headless tests.

    public static decimal wapi_Sleep(decimal nMs)
    {
        System.Threading.Thread.Sleep((int)nMs);
        return 0;
    }

    public static decimal wapi_MessageBox(dynamic hWnd, string cText, string cCaption = "", decimal nType = 0)
    {
        Console.WriteLine($"[MessageBox] {cCaption}: {cText}");
        return 1;  // IDOK
    }

    public static decimal wapi_MessageBoxTimeout(dynamic hWnd, string cText, string cCaption = "", decimal nType = 0, decimal wLang = 0, decimal nMs = 0)
    {
        Console.WriteLine($"[MessageBox] {cCaption}: {cText}");
        return 1;
    }

    public static void wapi_OutputDebugString(string cText) => Console.Error.WriteLine(cText);

    // ---- Win32 RGB helpers (easiutil/getrgbvalue.c) ----

    public static decimal COLORRGB2N(decimal r, decimal g, decimal b) =>
        (int)r + (int)g * 256 + (int)b * 65536;
    public static decimal GETRVALUE(decimal rgb) => (int)rgb & 0xFF;
    public static decimal GETGVALUE(decimal rgb) => ((int)rgb >> 8) & 0xFF;
    public static decimal GETBVALUE(decimal rgb) => ((int)rgb >> 16) & 0xFF;

    // ---- Binary conversions (Harbour builtins) ----
    // bin2l: 4-byte little-endian signed → numeric.
    // ul2bin: numeric → 4-byte little-endian unsigned string.
    // bin2i: 2-byte little-endian signed → numeric.
    // i2bin: numeric → 2-byte little-endian signed string.

    public static decimal Bin2L(string s)
    {
        if (s is null || s.Length < 4) return 0;
        int v = (byte)s[0] | ((byte)s[1] << 8) | ((byte)s[2] << 16) | ((byte)s[3] << 24);
        return v;
    }

    public static string UL2BIN(decimal n)
    {
        uint v = (uint)(long)n;
        char[] b = { (char)(v & 0xFF), (char)((v >> 8) & 0xFF),
                     (char)((v >> 16) & 0xFF), (char)((v >> 24) & 0xFF) };
        return new string(b);
    }

    public static decimal Bin2I(string s)
    {
        if (s is null || s.Length < 2) return 0;
        short v = (short)((byte)s[0] | ((byte)s[1] << 8));
        return v;
    }

    public static string I2Bin(decimal n)
    {
        ushort v = (ushort)(short)(int)n;
        char[] b = { (char)(v & 0xFF), (char)((v >> 8) & 0xFF) };
        return new string(b);
    }

    // ---- Cross-lib stubs ----
    // These have real implementations in sibling easipos libs (sharedx,
    // each app's main .prg). Stubbed here so the EasiUtil classlib
    // builds standalone; once a multi-project layout is wired up the
    // real impls take over. GETAPPNAME defaults to the entry assembly
    // name; MYERASE deletes; GETFLAG returns null (callers tolerate).

    public static string GETAPPNAME() =>
        System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name ?? "EasiUtil";

    public static bool MYERASE(string cFile)
    {
        try { System.IO.File.Delete(cFile); return true; }
        catch { return false; }
    }

    public static bool MYRENAME(string cOld, string cNew)
    {
        try { System.IO.File.Move(cOld, cNew); return true; }
        catch { return false; }
    }

    public static dynamic GETFLAG(string cFlagName) => null;

    // ---- Harbour Error object (stopgap) ----
    //
    // This class exists purely to let transpiled code that builds and
    // inspects Harbour Error objects compile. It mimics the shape of the
    // canonical Error class (the fields that createerror.prg and callers
    // poke at) but has no real semantics — no error propagation, no RTE
    // integration, no Try/SEQUENCE wiring. A real C# port would throw
    // structured exceptions or use Result<T,E> instead; hanging properties
    // off a POCO is just the path of least resistance to get past 1061s
    // in the classlib build. Expect this whole class to be deleted once
    // the idiomatic story is figured out.

    public class HbError
    {
        public decimal severity    { get; set; }
        public decimal genCode     { get; set; }
        public string  subSystem   { get; set; } = "";
        public decimal subCode     { get; set; }
        public string  description { get; set; } = "";
        public bool    canRetry    { get; set; }
        public bool    canDefault  { get; set; }
        public string  fileName    { get; set; } = "";
        public decimal osCode      { get; set; }
        public dynamic[] args      { get; set; } = System.Array.Empty<dynamic>();
        public string  operation   { get; set; } = "";
        public dynamic tries       { get; set; }
        public string classname() => "ERROR";
    }

    public static dynamic ErrorNew() => new HbError();
    /* Object `.classname()` fallback for arbitrary objects — Harbour allows
       it on anything. Real port would use `GetType().Name`. */
    public static string CLASSNAME(dynamic o) =>
        o is HbError he ? he.classname() : (o?.GetType().Name ?? "NIL");

    // ---- Misc ----

    /// <summary>
    /// Placeholder the transpiler emits in place of unsupported Harbour
    /// constructs (macros `&name`, workarea ALIAS expressions, comma-
    /// operator). Typed `dynamic` so it works on both sides of an
    /// assignment — C#'s `default` can't be an LHS, which would break
    /// `&cMemvar := expr` → `default = expr`. Executing the path that
    /// uses it silently discards the value; the transpiler emits a
    /// `warning W0016` at generation time listing every occurrence.
    /// </summary>
    public static dynamic MacroStub;

    public static decimal RecNo() => 0;
    public static dynamic Directory(string cSpec = "*.*", string cAttr = "") => new List<dynamic>();

    // ---- Function-reference resolution (Harbour's @FunName() operator) ----

    /// <summary>
    /// Cache of name → callable delegate so dispatch-table patterns like
    /// <c>{ TABLEFILE =&gt; @TableDetailDef() }</c> don't reflect on every
    /// invocation. Keyed case-insensitively (Harbour convention).
    /// </summary>
    private static readonly Dictionary<string, Func<dynamic[], dynamic>> s_funcPtrCache =
        new Dictionary<string, Func<dynamic[], dynamic>>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Return a callable <c>Func&lt;dynamic[], dynamic&gt;</c> for the
    /// named static method on the merged <c>Program</c> partial class.
    /// The transpiler emits <c>@FuncName()</c> as a call to this helper
    /// so that method groups — which C# won't convert to <c>dynamic</c>
    /// directly — become first-class values suitable for hash entries,
    /// array elements, and <c>LOCAL pFunc := @Foo()</c> assignments.
    /// Missing methods return a sentinel that throws when invoked —
    /// matches Harbour's runtime-failure semantics for a bad @ref.
    /// </summary>
    public static Func<dynamic[], dynamic> FuncPtr(string methodName)
    {
        if (methodName == null) return _ => null;
        if (s_funcPtrCache.TryGetValue(methodName, out var cached)) return cached;

        // Look up on Program. We assume the transpiled code lives in a
        // single `public static partial class Program` merged across
        // every .prg file's partial contribution. If someone builds a
        // multi-program executable, the lookup here will miss and
        // callers get the throwing sentinel.
        var programType = Type.GetType("Program")
            ?? System.Reflection.Assembly.GetExecutingAssembly().GetType("Program")
            ?? System.AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType("Program"))
                .FirstOrDefault(t => t != null);

        System.Reflection.MethodInfo method = null;
        if (programType != null)
        {
            method = programType.GetMethod(methodName,
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Static |
                System.Reflection.BindingFlags.IgnoreCase);
        }

        Func<dynamic[], dynamic> result;
        if (method == null)
        {
            result = _ => throw new MissingMethodException(
                $"HbRuntime.FuncPtr: no static method '{methodName}' on Program");
        }
        else
        {
            var parms = method.GetParameters();
            result = args =>
            {
                args ??= System.Array.Empty<dynamic>();
                var invokeArgs = new object[parms.Length];
                for (int i = 0; i < parms.Length; i++)
                {
                    if (i < args.Length)
                    {
                        var val = args[i];
                        // Convert.ChangeType handles the common
                        // numeric/string/bool coercions; null passes
                        // through unchanged.
                        if (val != null && !parms[i].ParameterType.IsAssignableFrom(val.GetType()))
                        {
                            try { val = Convert.ChangeType(val, parms[i].ParameterType, INV); }
                            catch { /* leave as-is; Invoke may still accept via boxing */ }
                        }
                        invokeArgs[i] = val;
                    }
                    else if (parms[i].HasDefaultValue)
                        invokeArgs[i] = parms[i].DefaultValue;
                    else
                        invokeArgs[i] = parms[i].ParameterType.IsValueType
                            ? Activator.CreateInstance(parms[i].ParameterType)
                            : null;
                }
                return method.Invoke(null, invokeArgs);
            };
        }

        s_funcPtrCache[methodName] = result;
        return result;
    }

    // ---- Dynamic member access (Harbour obj:&(name) macro support) ----

    // ---- DynamicObject base for ORM-style classes ----
}

/// <summary>
/// Harbour's COM automation wrapper. Source calls look like
/// <c>TOleAuto():New("ADODB.Recordset")</c> — compile-time shape: a class
/// with a <c>New(string progId)</c> method returning a dynamic COM proxy.
/// On Windows this would delegate to <c>Type.GetTypeFromProgID</c> +
/// <c>Activator.CreateInstance</c>; on other platforms there is no COM,
/// so New throws at call time. Either way the surface is "satisfies the
/// C# compiler" — runtime behaviour of Harbour ADO code against a modern
/// .NET data stack is a separate port.
/// </summary>
public class TOleAuto
{
    private object? _com;
    public TOleAuto() { }
    public dynamic New(string progId)
    {
        if (OperatingSystem.IsWindows())
        {
            var t = Type.GetTypeFromProgID(progId);
            if (t == null)
                throw new PlatformNotSupportedException(
                    $"ProgID '{progId}' not registered");
            _com = Activator.CreateInstance(t);
            return _com!;
        }
        throw new PlatformNotSupportedException(
            "TOleAuto (COM automation) is only supported on Windows");
    }
}

/// <summary>
/// Base class for Harbour classes that use runtime member access
/// (::&amp;(name) patterns). Backed by a Dictionary so that column
/// names or dynamically-created properties resolve at runtime.
/// Statically-declared properties (from DATA/VAR) take precedence
/// via the reflection path in TryGetMember/TrySetMember.
/// </summary>
public class HbDynamicObject : System.Dynamic.DynamicObject
{
    private readonly Dictionary<string, dynamic> _bag =
        new Dictionary<string, dynamic>(StringComparer.OrdinalIgnoreCase);

    public override bool TryGetMember(System.Dynamic.GetMemberBinder binder, out object result)
    {
        var prop = GetType().GetProperty(binder.Name, HbRuntime.MemberFlags);
        if (prop != null)
        {
            result = prop.GetValue(this);
            return true;
        }
        return _bag.TryGetValue(binder.Name, out result);
    }

    public override bool TrySetMember(System.Dynamic.SetMemberBinder binder, object value)
    {
        var prop = GetType().GetProperty(binder.Name, HbRuntime.MemberFlags);
        if (prop != null)
        {
            object coerced = value;
            if (value != null && prop.PropertyType != typeof(object) &&
                prop.PropertyType != value.GetType())
                coerced = Convert.ChangeType(value, prop.PropertyType);
            prop.SetValue(this, coerced);
            return true;
        }
        _bag[binder.Name] = value;
        return true;
    }

    public override bool TryInvokeMember(System.Dynamic.InvokeMemberBinder binder, object[] args, out object result)
    {
        var method = GetType().GetMethod(binder.Name, HbRuntime.MemberFlags);
        if (method != null)
        {
            result = method.Invoke(this, args);
            return true;
        }
        result = null;
        return false;
    }
}

public static partial class HbRuntime
{

    public static readonly System.Reflection.BindingFlags MemberFlags =
        System.Reflection.BindingFlags.Public |
        System.Reflection.BindingFlags.Instance |
        System.Reflection.BindingFlags.IgnoreCase;

    public static dynamic GETMEMBER(object obj, string name)
    {
        if (obj == null || string.IsNullOrEmpty(name)) return null;
        var prop = obj.GetType().GetProperty(name, MemberFlags);
        return prop?.GetValue(obj);
    }

    public static void SETMEMBER(object obj, string name, dynamic value)
    {
        if (obj == null || string.IsNullOrEmpty(name)) return;
        var prop = obj.GetType().GetProperty(name, MemberFlags);
        if (prop == null) return;
        object coerced = value;
        if (value != null && prop.PropertyType != typeof(object) &&
            prop.PropertyType != value.GetType())
            coerced = Convert.ChangeType(value, prop.PropertyType);
        prop.SetValue(obj, coerced);
    }

    public static dynamic SENDMSG(object obj, string name, params dynamic[] args)
    {
        if (obj == null || string.IsNullOrEmpty(name)) return null;
        var method = obj.GetType().GetMethod(name, MemberFlags);
        if (method == null) return null;
        return method.Invoke(obj, args);
    }
}
