using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

/// <summary>
/// Harbour runtime function implementations for transpiled C# code.
/// All Harbour builtin functions are available as static methods.
///
/// Method names are UPPERCASE to match the Harbour convention and the
/// canonical form in src/transpiler/hbfuncs.tab. The C# transpiler
/// uppercases every HbRuntime-remapped call at emit time (see
/// hb_csFuncMap in gencsharp.c), so source-level casing variations like
/// `INT()`, `int()`, `Int()` all resolve to the same method here.
/// </summary>
public static class HbRuntime
{
    static readonly CultureInfo INV = CultureInfo.InvariantCulture;

    // ---- Codeblock dispatch ----
    // Harbour codeblocks emit as Func<dynamic[], dynamic>, so EVAL just
    // packs the trailing args into the array and invokes. Delegate
    // fallback covers blocks that survived as a plain delegate (e.g.
    // typed fixed-arity lambdas) via DynamicInvoke.
    public static dynamic EVAL(dynamic block, params dynamic[] args)
    {
        if (block is Func<dynamic[], dynamic> fa) return fa(args);
        if (block is Delegate d) return d.DynamicInvoke(args);
        return null;
    }

    // ---- Console output (? and ??) ----

    // Harbour's `?` / `??` commands separate comma-delimited args
    // with a single space in the output. "hi", n prints as "hi 3"
    // (space between label and value), not "hi3".
    public static void QOUT(params dynamic[] args)
    {
        Console.WriteLine();
        for (int i = 0; i < args.Length; i++)
        {
            if (i > 0) Console.Write(' ');
            Console.Write(Fmt(args[i]));
        }
    }

    public static void QQOUT(params dynamic[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            if (i > 0) Console.Write(' ');
            Console.Write(Fmt(args[i]));
        }
    }

    static string Fmt(dynamic a) =>
        a is decimal d ? STR(d) : Convert.ToString(a, INV);

    // ---- Numeric functions ----

    public static string STR(decimal? nOrNull, decimal nWidth = 10, decimal nDec = -1)
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

    public static decimal VAL(string s)
    {
        decimal.TryParse(s, NumberStyles.Any, INV, out decimal result);
        return result;
    }

    public static decimal INT(decimal n) => Math.Truncate(n);
    public static decimal ROUND(decimal n, decimal nDec = 0) => Math.Round(n, (int)nDec);
    public static decimal ABS(decimal n) => Math.Abs(n);
    public static decimal MAX(decimal a, decimal b) => Math.Max(a, b);
    public static decimal MIN(decimal a, decimal b) => Math.Min(a, b);
    public static decimal MOD(decimal a, decimal b) => a % b;
    public static decimal SQRT(decimal n) => (decimal)Math.Sqrt((double)n);
    public static decimal LOG(decimal n) => (decimal)Math.Log((double)n);
    public static decimal EXP(decimal n) => (decimal)Math.Exp((double)n);

    // ---- String functions ----

    public static decimal LEN(dynamic x)
    {
        if (x is string s) return s.Length;
        if (x is Array a) return a.Length;
        return 0;
    }

    public static string UPPER(string s) => s.ToUpper();
    public static string LOWER(string s) => s.ToLower();
    public static string TRIM(string s) => s.TrimEnd();
    public static string RTRIM(string s) => s.TrimEnd();
    public static string LTRIM(string s) => s.TrimStart();
    public static string ALLTRIM(string s) => s.Trim();
    public static string SPACE(decimal n) => new string(' ', (int)n);
    public static string REPLICATE(string s, decimal n) => string.Concat(System.Linq.Enumerable.Repeat(s, (int)n));
    public static string CHR(decimal n) => ((char)(int)n).ToString();
    public static decimal ASC(string s) => s.Length > 0 ? (decimal)s[0] : 0;

    public static string SUBSTR(string s, decimal nStart, decimal nLen = -1)
    {
        // Harbour is 1-based
        int iStart = Math.Max((int)nStart - 1, 0);
        int iLen = (int)nLen;
        if (iStart >= s.Length) return "";
        if (iLen < 0) return s.Substring(iStart);
        iLen = Math.Min(iLen, s.Length - iStart);
        return s.Substring(iStart, iLen);
    }

    public static string LEFT(string s, decimal n) => SUBSTR(s, 1, n);
    public static string RIGHT(string s, decimal n) { int i = (int)n; return i >= s.Length ? s : s.Substring(s.Length - i); }

    public static decimal AT(string cSearch, string cString) =>
        (decimal)(cString.IndexOf(cSearch, StringComparison.Ordinal) + 1);

    public static decimal RAT(string cSearch, string cString) =>
        (decimal)(cString.LastIndexOf(cSearch, StringComparison.Ordinal) + 1);

    public static string PADL(string s, decimal nLen, string cFill = " ") =>
        s.Length >= (int)nLen ? s : s.PadLeft((int)nLen, cFill[0]);

    public static string PADR(string s, decimal nLen, string cFill = " ") =>
        s.Length >= (int)nLen ? s : s.PadRight((int)nLen, cFill[0]);

    public static string PADC(string s, decimal nLen, string cFill = " ")
    {
        int iLen = (int)nLen;
        if (s.Length >= iLen) return s;
        int nLeft = (iLen - s.Length) / 2;
        return s.PadLeft(s.Length + nLeft, cFill[0]).PadRight(iLen, cFill[0]);
    }

    public static string STRTRAN(string cString, string cSearch, string cReplace = "") =>
        cString.Replace(cSearch, cReplace);

    public static string STUFF(string cString, decimal nStart, decimal nDel, string cInsert)
    {
        int iStart = Math.Max((int)nStart - 1, 0);
        int iDel = (int)nDel;
        if (iStart >= cString.Length) return cString + cInsert;
        return cString.Substring(0, iStart) + cInsert + cString.Substring(Math.Min(iStart + iDel, cString.Length));
    }

    public static string TRANSFORM(dynamic val, string cMask) => Convert.ToString(val, INV);
    public static string STRZERO(decimal n, decimal nLen = 10, decimal nDec = 0) =>
        n.ToString("F" + (int)nDec, INV).PadLeft((int)nLen, '0');

    // ---- Logical functions ----

    public static bool EMPTY(dynamic val)
    {
        if (val == null) return true;
        if (val is decimal d) return d == 0;
        if (val is string s) return s.Trim().Length == 0;
        if (val is bool b) return !b;
        if (val is Array a) return a.Length == 0;
        return false;
    }

    public static bool ISDIGIT(string s) => s.Length > 0 && char.IsDigit(s[0]);
    public static bool ISALPHA(string s) => s.Length > 0 && char.IsLetter(s[0]);
    public static bool ISUPPER(string s) => s.Length > 0 && char.IsUpper(s[0]);
    public static bool ISLOWER(string s) => s.Length > 0 && char.IsLower(s[0]);

    // ---- Date functions ----
    // Harbour DATE → C# DateOnly (no time component). Harbour TIMESTAMP
    // maps to C# DateTime via the transpiler's type map.

    public static DateOnly DATE() => DateOnly.FromDateTime(DateTime.Today);
    public static DateOnly CTOD(string s) { DateTime.TryParse(s, out DateTime d); return DateOnly.FromDateTime(d); }
    public static DateOnly STOD(string s) { DateTime.TryParseExact(s, "yyyyMMdd", INV, DateTimeStyles.None, out DateTime d); return DateOnly.FromDateTime(d); }
    public static string DTOC(DateOnly d) => d.ToString("MM/dd/yyyy", INV);
    public static string DTOS(DateOnly d) => d.ToString("yyyyMMdd", INV);
    public static string TIME() => DateTime.Now.ToString("HH:mm:ss", INV);

    // ---- Terminal ----

    public static void SETCOLOR(string cColor) { }

    // ---- Array functions ----
    // Harbour arrays map to C# dynamic[] or List<dynamic>. Because dynamic[]
    // can't be resized, the transpiler-generated code mostly uses List-like
    // behavior — but the source still writes `aadd()`, `asize()` etc. against
    // whichever shape the variable happens to be. These helpers accept
    // dynamic and dispatch on the runtime shape: List<dynamic> resizes in
    // place; arrays are rebuilt into a new array and returned.

    public static dynamic AADD(dynamic arr, dynamic val)
    {
        if (arr is List<dynamic> list) { list.Add(val); return val; }
        return val;
    }

    public static dynamic ASIZE(dynamic arr, decimal nLen)
    {
        int n = (int)nLen;
        if (arr is List<dynamic> list)
        {
            while (list.Count > n) list.RemoveAt(list.Count - 1);
            while (list.Count < n) list.Add(null);
        }
        return arr;
    }

    public static dynamic ACLONE(dynamic arr)
    {
        if (arr is List<dynamic> list) return new List<dynamic>(list);
        if (arr is Array a) { var copy = (Array)a.Clone(); return copy; }
        return arr;
    }

    public static decimal ASCAN(dynamic arr, dynamic val)
    {
        if (arr is List<dynamic> list)
        {
            for (int i = 0; i < list.Count; i++)
                if (Equals(list[i], val)) return i + 1;
        }
        return 0;
    }

    public static dynamic AEVAL(dynamic arr, dynamic block)
    {
        if (arr is List<dynamic> list)
        {
            for (int i = 0; i < list.Count; i++)
                EVAL(block, list[i], (decimal)(i + 1));
        }
        return arr;
    }

    public static dynamic ADEL(dynamic arr, decimal nPos)
    {
        if (arr is List<dynamic> list)
        {
            int i = (int)nPos - 1;
            if (i >= 0 && i < list.Count) { list.RemoveAt(i); list.Add(null); }
        }
        return arr;
    }

    public static dynamic AINS(dynamic arr, decimal nPos)
    {
        if (arr is List<dynamic> list)
        {
            int i = (int)nPos - 1;
            if (i >= 0 && i < list.Count) { list.Insert(i, null); list.RemoveAt(list.Count - 1); }
        }
        return arr;
    }

    // ---- Type inspection ----

    public static string VALTYPE(dynamic x)
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

    public static decimal PCOUNT() => 0;  // varargs path uses hbva.Length directly

    // ---- Defaults helper (hb_default) ----
    // Harbour's hb_default( @var, val ) sets var to val if NIL. Without
    // by-ref pass-through we return the non-null one of the two.

    public static dynamic HB_DEFAULT(dynamic val, dynamic defVal) => val ?? defVal;

    // ---- Time ----

    public static decimal SECONDS() =>
        (decimal)(DateTime.Now - DateTime.Today).TotalSeconds;

    // ---- File I/O ----
    // Very simple handle table mapping decimal -> FileStream. Real Harbour
    // returns -1 on failure; FERROR() returns the last OS errno. We only
    // track "did the last op fail" as 0 / 1.

    static readonly Dictionary<int, FileStream> s_files = new();
    static int s_nextHandle = 1;
    static int s_lastError = 0;

    public static decimal FOPEN(string cFile, decimal nMode = 0)
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

    public static decimal FCREATE(string cFile, decimal nAttr = 0)
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

    public static bool FCLOSE(decimal nHandle)
    {
        if (s_files.TryGetValue((int)nHandle, out var fs))
        {
            fs.Dispose();
            s_files.Remove((int)nHandle);
            return true;
        }
        return false;
    }

    public static decimal FREAD(decimal nHandle, ref string cBuf, decimal nBytes)
    {
        if (!s_files.TryGetValue((int)nHandle, out var fs)) { s_lastError = 1; return 0; }
        byte[] b = new byte[(int)nBytes];
        int read = fs.Read(b, 0, b.Length);
        cBuf = System.Text.Encoding.UTF8.GetString(b, 0, read);
        return read;
    }

    public static decimal FWRITE(decimal nHandle, string cBuf, decimal nBytes = -1)
    {
        if (!s_files.TryGetValue((int)nHandle, out var fs)) { s_lastError = 1; return 0; }
        byte[] b = System.Text.Encoding.UTF8.GetBytes(cBuf);
        int n = (int)nBytes < 0 ? b.Length : Math.Min((int)nBytes, b.Length);
        fs.Write(b, 0, n);
        return n;
    }

    public static decimal FSEEK(decimal nHandle, decimal nOffset, decimal nOrigin = 0)
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

    public static decimal FERROR() => s_lastError;

    public static bool FILE(string cFile) => System.IO.File.Exists(cFile);

    public static string MEMOREAD(string cFile)
    {
        try { return System.IO.File.ReadAllText(cFile); }
        catch { return ""; }
    }

    public static bool MEMOWRIT(string cFile, string cData)
    {
        try { System.IO.File.WriteAllText(cFile, cData); return true; }
        catch { return false; }
    }

    // ---- Date decomposition ----

    static DateTime AsDT(dynamic d) => d is DateOnly dd ? dd.ToDateTime(TimeOnly.MinValue)
                                     : d is DateTime dt ? dt
                                     : default;
    public static decimal YEAR(dynamic d) => AsDT(d).Year;
    public static decimal MONTH(dynamic d) => AsDT(d).Month;
    public static decimal DAY(dynamic d) => AsDT(d).Day;
    public static decimal DOW(dynamic d) => (decimal)((int)AsDT(d).DayOfWeek + 1);
    public static string CDOW(dynamic d) => AsDT(d).ToString("dddd", INV);
    public static string CMONTH(dynamic d) => AsDT(d).ToString("MMMM", INV);

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
    public static dynamic HB_HGETDEF(dynamic h, dynamic key, dynamic def = null)
    {
        var d = AsDict(h);
        return (d != null && d.Contains(key)) ? d[key] : def;
    }
    public static bool HB_HHASKEY(dynamic h, dynamic key)
    {
        var d = AsDict(h);
        return d != null && d.Contains(key);
    }
    public static decimal HB_HDEL(dynamic h, dynamic key)
    {
        var d = AsDict(h);
        if (d != null && d.Contains(key)) d.Remove(key);
        return 0;
    }
    public static dynamic HB_HKEYS(dynamic h)
    {
        var r = new List<dynamic>();
        var d = AsDict(h);
        if (d != null) foreach (var k in d.Keys) r.Add(k);
        return r;
    }
    public static dynamic HB_HVALUES(dynamic h)
    {
        var r = new List<dynamic>();
        var d = AsDict(h);
        if (d != null) foreach (var v in d.Values) r.Add(v);
        return r;
    }

    // ---- Array extras ----

    public static dynamic AFILL(dynamic arr, dynamic val)
    {
        if (arr is List<dynamic> list) for (int i = 0; i < list.Count; i++) list[i] = val;
        return arr;
    }

    public static dynamic ASORT(dynamic arr, dynamic nStart = null, dynamic nCount = null, dynamic block = null)
    {
        if (arr is List<dynamic> list)
        {
            if (block is Delegate)
                list.Sort((a, b) => ((bool)EVAL(block, a, b)) ? -1 : 1);
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

    // ---- SET() ----
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

    public static dynamic SET(decimal nSet, dynamic newVal = null)
    {
        int key = (int)nSet;
        s_sets.TryGetValue(key, out dynamic prev);
        if (newVal is not null)
        {
            /* Normalize "ON"/"OFF" → bool when the existing slot is
               bool-typed. Harbour's `SET EXACT ON` lexer route STRSMARTs
               the keyword into a string literal that Set() receives. */
            if (prev is bool && newVal is string)
                s_sets[key] = AsOnOff(newVal);
            else
                s_sets[key] = newVal;
        }
        return prev;
    }

    /* Harbour's Set() / __SetCentury() accept either a bool or the
       strings "ON"/"OFF" (because the #command rules for SET ... ON/OFF
       STRSMART the keyword into a string literal). Normalize to bool. */
    static bool AsOnOff(dynamic val, bool fallback = false)
    {
        if (val is null) return fallback;
        if (val is bool b) return b;
        if (val is string s) return string.Equals(s, "ON", StringComparison.OrdinalIgnoreCase);
        return fallback;
    }

    public static bool __SETCENTURY(dynamic val = null)
    {
        bool prev = s_sets[(int)_SET_CENTURY] is bool pb && pb;
        if (val is not null) s_sets[(int)_SET_CENTURY] = AsOnOff(val);
        return prev;
    }

    // ---- Threading primitives ----
    // Harbour's hb_mutex* maps to a System.Threading object. We use a
    // Dictionary<object, object> of mutexes so the transpiled code can
    // hold them as dynamic.

    public static dynamic HB_MUTEXCREATE() => new object();

    public static bool HB_MUTEXLOCK(dynamic mtx, dynamic nTimeout = null)
    {
        if (mtx is null) return false;
        System.Threading.Monitor.Enter(mtx);
        return true;
    }

    public static bool HB_MUTEXUNLOCK(dynamic mtx)
    {
        if (mtx is null) return false;
        try { System.Threading.Monitor.Exit(mtx); return true; }
        catch { return false; }
    }

    // ---- wapi_* stubs ----
    // Windows-specific. On non-Windows they degrade to Console output /
    // Thread.Sleep. Enough for transpiled code to compile and run
    // headless tests.

    public static decimal WAPI_SLEEP(decimal nMs)
    {
        System.Threading.Thread.Sleep((int)nMs);
        return 0;
    }

    public static decimal WAPI_MESSAGEBOX(dynamic hWnd, string cText, string cCaption = "", decimal nType = 0)
    {
        Console.WriteLine($"[MessageBox] {cCaption}: {cText}");
        return 1;  // IDOK
    }

    public static decimal WAPI_MESSAGEBOXTIMEOUT(dynamic hWnd, string cText, string cCaption = "", decimal nType = 0, decimal wLang = 0, decimal nMs = 0)
    {
        Console.WriteLine($"[MessageBox] {cCaption}: {cText}");
        return 1;
    }

    public static void WAPI_OUTPUTDEBUGSTRING(string cText) => Console.Error.WriteLine(cText);

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

    public static decimal BIN2L(string s)
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

    public static decimal BIN2I(string s)
    {
        if (s is null || s.Length < 2) return 0;
        short v = (short)((byte)s[0] | ((byte)s[1] << 8));
        return v;
    }

    public static string I2BIN(decimal n)
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

    public static dynamic ERRORNEW() => new HbError();
    /* Object `.classname()` fallback for arbitrary objects — Harbour allows
       it on anything. Real port would use `GetType().Name`. */
    public static string CLASSNAME(dynamic o) =>
        o is HbError he ? he.classname() : (o?.GetType().Name ?? "NIL");

    // ---- Misc ----

    public static decimal RECNO() => 0;
    public static dynamic DIRECTORY(string cSpec = "*.*", string cAttr = "") => new List<dynamic>();
}
