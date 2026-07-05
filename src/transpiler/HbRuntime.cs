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
        if (block is not Delegate d) return null;
        args ??= System.Array.Empty<dynamic>();
        var ps = d.Method.GetParameters();
        // A varargs codeblock (`{|...|}`) is a single dynamic[] param —
        // hand it the whole args array as that one parameter.
        if (ps.Length == 1 && ps[0].ParameterType == typeof(object[]))
            return d.DynamicInvoke(new object[] { args });
        // Fixed-arity codeblock. A Harbour block tolerates being called
        // with more args than it declares (extras ignored) or fewer
        // (missing become NIL); DynamicInvoke throws on any mismatch,
        // so fit the count to the delegate's real parameter list.
        var fitted = new object[ps.Length];
        System.Array.Copy(args, fitted, Math.Min(args.Length, ps.Length));
        return d.DynamicInvoke(fitted);
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
        a is decimal d ? Str(d) :
        // Harbour renders logicals as .T. / .F., and pads all numerics
        // to Str()'s default width. Integral values reach here when a
        // literal int crossed a dynamic boundary (the emitter only
        // m-suffixes floating literals), so give them the same padding
        // a decimal would get.
        a is bool l ? (l ? ".T." : ".F.") :
        a is int or long or short or byte ? Str((decimal)a) :
        Convert.ToString(a, INV);

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

    // ToString() is a recurring easipos idiom for Harbour's Str() —
    // a C#-influenced habit among the contributors. The transpiler
    // routes bare `ToString(x)` call sites here (see hb_csFuncMap in
    // gencsharp.c). Same formatting contract as Str so the two are
    // interchangeable; kept under its own name to keep source and
    // emit visually aligned.
    public static string ToString(decimal? nOrNull, decimal nWidth = 10, decimal nDec = -1) =>
        Str(nOrNull, nWidth, nDec);

    public static decimal Val(string s)
    {
        // Harbour's Val() parses the leading numeric run and ignores
        // any trailing text — Val("12abc") is 12, not 0. decimal.TryParse
        // on the whole string rejects the garbage and yields 0, so first
        // carve out the leading [ws][sign][digits][.digits] prefix.
        if (string.IsNullOrEmpty(s)) return 0;
        int i = 0, n = s.Length;
        while (i < n && (s[i] == ' ' || s[i] == '\t')) i++;
        int start = i;
        if (i < n && (s[i] == '+' || s[i] == '-')) i++;
        bool seenDot = false;
        while (i < n)
        {
            char c = s[i];
            if (c >= '0' && c <= '9') i++;
            else if (c == '.' && !seenDot) { seenDot = true; i++; }
            else break;
        }
        decimal.TryParse(s.Substring(start, i - start),
                         NumberStyles.Any, INV, out decimal result);
        return result;
    }

    public static decimal Int(decimal n) => Math.Truncate(n);
    // Harbour Round() is half-away-from-zero; Math.Round defaults to
    // banker's rounding (ToEven), which would skew POS money math.
    public static decimal Round(decimal n, decimal nDec = 0) =>
        Math.Round(n, (int)nDec, MidpointRounding.AwayFromZero);
    public static decimal Abs(decimal n) => Math.Abs(n);
    public static decimal Max(decimal a, decimal b) => Math.Max(a, b);
    public static decimal Min(decimal a, decimal b) => Math.Min(a, b);
    public static decimal Mod(decimal a, decimal b) => a % b;
    public static decimal Pow(decimal a, decimal b) => (decimal)Math.Pow((double)a, (double)b);
    public static decimal Sqrt(decimal n) => (decimal)Math.Sqrt((double)n);
    public static decimal Log(decimal n) => (decimal)Math.Log((double)n);
    public static decimal Exp(decimal n) => (decimal)Math.Exp((double)n);

    // ---- String functions ----

    public static decimal Len(dynamic x)
    {
        if (x is string s) return s.Length;
        if (x is System.Array a) return a.Length;
        // Harbour Len() of a hash is its key count.
        if (x is System.Collections.IDictionary h) return h.Count;
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
    public static string Right(string s, decimal n)
    {
        int i = (int)n;
        if (i <= 0) return "";                 // Harbour: non-positive → ""
        return i >= s.Length ? s : s.Substring(s.Length - i);
    }

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
    public static string StrZero(decimal n, decimal nLen = 10, decimal nDec = 0)
    {
        // Zero-pad the magnitude, then prepend the sign — padding the
        // whole "-5" would give "00-5" instead of "-005".
        string sign = n < 0 ? "-" : "";
        string digits = Math.Abs(n).ToString("F" + (int)nDec, INV);
        return sign + digits.PadLeft((int)nLen - sign.Length, '0');
    }

    // ---- Logical functions ----

    public static bool Empty(dynamic val)
    {
        if (val == null) return true;
        if (val is decimal d) return d == 0;
        if (val is string s) return s.Trim().Length == 0;
        if (val is bool b) return !b;
        if (val is System.Array a) return a.Length == 0;
        if (val is System.Collections.IDictionary h) return h.Count == 0;
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
    public static string DToC(DateOnly d)
    {
        // Honour SET DATEFORMAT. Harbour's format tokens (d/m/y, case-
        // insensitive) need translating to .NET — month must be 'M';
        // separators and other characters pass through unchanged.
        string fmt = s_sets.TryGetValue((int)_SET_DATEFORMAT, out var f)
                     && f is string fs && fs.Length > 0 ? fs : "mm/dd/yyyy";
        var sb = new System.Text.StringBuilder(fmt.Length);
        foreach (char c in fmt)
            sb.Append(c is 'm' or 'M' ? 'M'
                    : c is 'd' or 'D' ? 'd'
                    : c is 'y' or 'Y' ? 'y' : c);
        return d.ToString(sb.ToString(), INV);
    }
    public static string DToS(DateOnly d) => d.ToString("yyyyMMdd", INV);
    public static string Time() => DateTime.Now.ToString("HH:mm:ss", INV);

    // ---- Terminal ----

    public static void SetColor(string cColor) { }

    // ---- Array functions ----
    // Harbour arrays map to C# dynamic[]. Size-changing mutators (ASize,
    // AAdd) take `ref dynamic[]` so the reallocated array propagates back;
    // the transpiler inserts `ref` at the call site. Size-stable mutators
    // (AIns, ADel, AFill, ASort) shift contents in place.

    public static dynamic AAdd(ref dynamic[] arr, dynamic val)
    {
        int n = arr?.Length ?? 0;
        System.Array.Resize(ref arr, n + 1);
        arr[n] = val;
        return val;
    }

    // `ref dynamic` overload — used when the lvalue's static type is
    // `dynamic` (a class DATA field without a Hungarian array prefix,
    // e.g. `protected dynamic array;`). C# `ref` is invariant so a
    // `ref dynamic` arg won't bind to a `ref dynamic[]` parameter even
    // though the runtime value is an array. Build a new array, assign
    // it back through the ref.
    public static dynamic AAdd(ref dynamic arr, dynamic val)
    {
        if (arr is object[] objArr)
        {
            int n = objArr.Length;
            var tmp = new dynamic[n + 1];
            System.Array.Copy(objArr, tmp, n);
            tmp[n] = val;
            arr = tmp;
        }
        else if (arr == null)
        {
            arr = new dynamic[] { val };
        }
        return val;
    }

    // Non-ref fallback for call sites the emitter can't pin to an lvalue
    // (GETMEMBER results, array indices, method-call results). Returns
    // val to preserve Harbour's AAdd return semantic; the underlying
    // array isn't actually grown — same silent-failure mode as the
    // pre-ref-overload runtime. Real fix requires the source to be
    // restructured to assign through.
    public static dynamic AAdd(dynamic arr, dynamic val) => val;

    public static dynamic[] ASize(ref dynamic[] arr, decimal nLen)
    {
        int n = (int)nLen;
        if (arr == null)
            arr = new dynamic[n];
        else
            System.Array.Resize(ref arr, n);
        return arr;
    }

    public static dynamic[] ASize(ref dynamic arr, decimal nLen)
    {
        int n = (int)nLen;
        var tmp = new dynamic[n];
        if (arr is object[] objArr)
            System.Array.Copy(objArr, tmp, Math.Min(objArr.Length, n));
        arr = tmp;
        return tmp;
    }

    // Non-ref fallback. Returns a new resized array but can't update
    // the caller's variable — caller code that does `aArr := ASize(...)`
    // still works; `ASize(GETMEMBER(...), n)` as a statement is a
    // silent no-op for the caller, matching the pre-ref behavior.
    public static dynamic[] ASize(dynamic arr, decimal nLen)
    {
        int n = (int)nLen;
        if (arr is object[] objArr)
        {
            var copy = new dynamic[n];
            System.Array.Copy(objArr, copy, Math.Min(objArr.Length, n));
            return copy;
        }
        return new dynamic[n];
    }

    public static dynamic AClone(dynamic arr)
    {
        // Harbour AClone duplicates nested arrays recursively; scalars,
        // objects and hashes are shared by reference.
        if (arr is object[] objArr)
        {
            var copy = new dynamic[objArr.Length];
            for (int i = 0; i < objArr.Length; i++)
                copy[i] = objArr[i] is object[] ? AClone(objArr[i]) : objArr[i];
            return copy;
        }
        return arr;
    }

    public static decimal AScan(dynamic arr, dynamic val)
    {
        if (arr is object[] objArr)
        {
            // AScan(arr, bBlock): evaluate the block per element
            // (Harbour passes the element and its 1-based index),
            // return the first index where it yields .T.
            if (val is Delegate)
            {
                for (int i = 0; i < objArr.Length; i++)
                    if ((bool)Eval(val, objArr[i], (decimal)(i + 1)))
                        return i + 1;
            }
            else
            {
                for (int i = 0; i < objArr.Length; i++)
                    if (Equals(objArr[i], val)) return i + 1;
            }
        }
        return 0;
    }

    public static dynamic AEval(dynamic arr, dynamic block)
    {
        if (arr is object[] objArr)
        {
            for (int i = 0; i < objArr.Length; i++)
                Eval(block, objArr[i], (decimal)(i + 1));
        }
        return arr;
    }

    public static dynamic ADel(dynamic arr, decimal nPos)
    {
        if (arr is object[] objArr)
        {
            int i = (int)nPos - 1;
            if (i >= 0 && i < objArr.Length)
            {
                for (int j = i; j < objArr.Length - 1; j++)
                    objArr[j] = objArr[j + 1];
                objArr[objArr.Length - 1] = null;
            }
        }
        return arr;
    }

    public static dynamic AIns(dynamic arr, decimal nPos)
    {
        if (arr is object[] objArr)
        {
            int i = (int)nPos - 1;
            if (i >= 0 && i < objArr.Length)
            {
                for (int j = objArr.Length - 1; j > i; j--)
                    objArr[j] = objArr[j - 1];
                objArr[i] = null;
            }
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
        if (x is System.Array) return "A";
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
    public static bool ISARRAY(dynamic x) => x is System.Array;
    public static bool ISHASH(dynamic x) => x is System.Collections.IDictionary;
    public static bool ISOBJECT(dynamic x) =>
        x is not null && !(x is string or bool or decimal or int or long or double or float
                          or DateOnly or DateTime or System.Array or Delegate
                          or System.Collections.IDictionary);
    public static bool ISBLOCK(dynamic x) => x is Delegate;

    // ---- Hash helpers ----

    static System.Collections.IDictionary AsDict(dynamic h) => h as System.Collections.IDictionary;
    // The (object) casts below are load-bearing. AsDict(h) with a
    // dynamic argument is itself dynamically bound, which makes `d`
    // dynamic too — and then d.Contains(...) resolves against the
    // hash's RUNTIME type (e.g. Dictionary<string, dynamic>, which has
    // no public Contains): RuntimeBinderException. AsDict((object)h)
    // binds statically, `d` is IDictionary, and the key casts keep the
    // member calls bound to the non-generic IDictionary surface.
    public static dynamic hb_HGetDef(dynamic h, dynamic key, dynamic def = null)
    {
        var d = AsDict((object)h);
        return (d != null && d.Contains((object)key)) ? d[(object)key] : def;
    }
    public static bool hb_HHasKey(dynamic h, dynamic key)
    {
        var d = AsDict((object)h);
        return d != null && d.Contains((object)key);
    }

    // Harbour `$` operator: `a $ b` is substring containment when b is
    // a string, key containment when b is a hash. Dictionary has no
    // .Contains() — only the non-generic IDictionary.Contains(key).
    public static bool HbIn(dynamic needle, dynamic haystack)
    {
        if (haystack is string s)
            return s.Contains((string)needle);
        if (haystack is System.Collections.IDictionary d)
            return d.Contains((object)needle);
        return false;
    }
    public static decimal hb_HDel(dynamic h, dynamic key)
    {
        var d = AsDict((object)h);
        if (d != null && d.Contains((object)key)) d.Remove((object)key);
        return 0;
    }
    public static dynamic hb_HKeys(dynamic h)
    {
        var d = AsDict((object)h);
        if (d == null) return System.Array.Empty<dynamic>();
        var r = new dynamic[d.Count];
        int i = 0;
        foreach (var k in d.Keys) r[i++] = k;
        return r;
    }
    public static dynamic hb_HValues(dynamic h)
    {
        var d = AsDict((object)h);
        if (d == null) return System.Array.Empty<dynamic>();
        var r = new dynamic[d.Count];
        int i = 0;
        foreach (var v in d.Values) r[i++] = v;
        return r;
    }

    // ---- Array extras ----

    public static dynamic AFill(dynamic arr, dynamic val)
    {
        if (arr is object[] objArr) for (int i = 0; i < objArr.Length; i++) objArr[i] = val;
        return arr;
    }

    public static dynamic ASort(dynamic arr, dynamic nStart = null, dynamic nCount = null, dynamic block = null)
    {
        if (arr is object[] objArr)
        {
            if (block is Delegate)
                // A Harbour sort block returns .T. when its first arg
                // should sort before the second. Map that to a proper
                // three-way comparison: a `? -1 : 1` shortcut returns 1
                // for both (a,b) and (b,a) on equal elements, which is
                // an inconsistent comparer — System.Array.Sort throws on it.
                System.Array.Sort(objArr, (a, b) =>
                {
                    if ((bool)Eval(block, a, b)) return -1;
                    if ((bool)Eval(block, b, a)) return 1;
                    return 0;
                });
            else
                System.Array.Sort(objArr, (a, b) => Comparer<dynamic>.Default.Compare(a, b));
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
    public static dynamic Directory(string cSpec = "*.*", string cAttr = "") => System.Array.Empty<dynamic>();

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
        var programType = System.Type.GetType("Program")
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

    // Exposed so HbRuntime.GETMEMBER/SETMEMBER — the obj:&(name) macro
    // path, which doesn't go through the DLR — can reach the bag too,
    // not only statically reflected members.
    public bool TryBagGet(string name, out dynamic value) =>
        _bag.TryGetValue(name, out value);
    public void BagSet(string name, dynamic value) => _bag[name] = value;

    public override bool TryGetMember(System.Dynamic.GetMemberBinder binder, out object result)
    {
        var prop = GetType().GetProperty(binder.Name, HbRuntime.MemberFlags);
        if (prop != null)
        {
            result = prop.GetValue(this);
            return true;
        }
        // Class DATA members emit as plain fields, not properties — a
        // declared member reached via ((dynamic)this) lands here.
        var field = GetType().GetField(binder.Name, HbRuntime.MemberFlags);
        if (field != null)
        {
            result = field.GetValue(this);
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
        var field = GetType().GetField(binder.Name, HbRuntime.MemberFlags);
        if (field != null)
        {
            object coerced = value;
            if (value != null && field.FieldType != typeof(object) &&
                field.FieldType != value.GetType())
                coerced = Convert.ChangeType(value, field.FieldType);
            field.SetValue(this, coerced);
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
        var t = obj.GetType();
        var prop = t.GetProperty(name, MemberFlags);
        if (prop != null) return prop.GetValue(obj);
        // Class DATA members now emit as plain fields (so they can be
        // passed by ref). Fall back to a field lookup.
        var field = t.GetField(name, MemberFlags);
        if (field != null) return field.GetValue(obj);
        // Dynamic class: a column held only in the dictionary bag.
        if (obj is HbDynamicObject hdo && hdo.TryBagGet(name, out dynamic bagVal))
            return bagVal;
        return null;
    }

    public static void SETMEMBER(object obj, string name, dynamic value)
    {
        if (obj == null || string.IsNullOrEmpty(name)) return;
        var t = obj.GetType();
        var prop = t.GetProperty(name, MemberFlags);
        if (prop != null)
        {
            object coerced = value;
            if (value != null && prop.PropertyType != typeof(object) &&
                prop.PropertyType != value.GetType())
                coerced = Convert.ChangeType(value, prop.PropertyType);
            prop.SetValue(obj, coerced);
            return;
        }
        var field = t.GetField(name, MemberFlags);
        if (field == null)
        {
            // Dynamic class: store the column in the dictionary bag.
            if (obj is HbDynamicObject hdo) hdo.BagSet(name, value);
            return;
        }
        object coercedf = value;
        if (value != null && field.FieldType != typeof(object) &&
            field.FieldType != value.GetType())
            coercedf = Convert.ChangeType(value, field.FieldType);
        field.SetValue(obj, coercedf);
    }

    public static dynamic SENDMSG(object obj, string name, params dynamic[] args)
    {
        if (obj == null || string.IsNullOrEmpty(name)) return null;
        var method = obj.GetType().GetMethod(name, MemberFlags);
        if (method == null) return null;
        return method.Invoke(obj, args);
    }

    // Legacy port I/O for DRAW_NCR25 cash drawer (Super I/O chip on ports
    // 0x2E/0x2F). No equivalent on non-Windows; no-op everywhere so the
    // rest of drawio.prg compiles. Callers check return, so we return 0.
    public static decimal INPIOInit() => 0;
    public static void    INPIOWrite(decimal port, decimal value) { }
    public static decimal INPIORead(decimal port) => 0;

    // Windows message-pump helper called during busy-wait loops. No-op
    // on other platforms; a proper implementation would drain the GUI
    // event queue, but the wait loops tolerate empty pumps.
    public static void BusyReadMsgPump() { }

    // Defined in sharedx/idxfile.prg (DBF reindex utility, blacklisted).
    // The original procedure is already a no-op — IndexFileLan() is
    // commented out and only TraceOut / status-display calls remain.
    public static void ReindexLan() { }

    // Windows API shims used by easipos. Callers build paths with
    // `left(GetModuleFileName(), rat("\", ...))` so we return the full
    // path of the running executable, not just its directory.
    public static string GetModuleFileName() =>
        System.Environment.ProcessPath ?? "";

    // `CreateProcess(cCmd)` in easipos takes one string that may include
    // arguments ("matrixsvr.exe /X"). Split on the first space.
    public static bool CreateProcess(string cCommand)
    {
        if (string.IsNullOrWhiteSpace(cCommand)) return false;
        try
        {
            var parts = cCommand.Split(new[] { ' ' }, 2);
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = parts[0],
                Arguments = parts.Length > 1 ? parts[1] : "",
                UseShellExecute = false,
            };
            return System.Diagnostics.Process.Start(psi) != null;
        }
        catch { return false; }
    }

    // Toggle a file's read-only bit. `cMode` is "W" (writable) or "R"
    // (read-only) — matches the easipos filearchive / filepath idiom.
    public static void Chmod(string cFile, string cMode)
    {
        if (string.IsNullOrEmpty(cFile) || !System.IO.File.Exists(cFile))
            return;
        var attrs = System.IO.File.GetAttributes(cFile);
        if (cMode != null && (cMode == "R" || cMode == "r"))
            attrs |= System.IO.FileAttributes.ReadOnly;
        else
            attrs &= ~System.IO.FileAttributes.ReadOnly;
        System.IO.File.SetAttributes(cFile, attrs);
    }

    // Binary helpers ported from EasiPosX/easiutil/bin.c. Harbour stores
    // byte strings as 1 char per byte with values 0..255 (Latin-1).
    // Little-endian — matches the original C which used raw pointer
    // casts on x86 hardware.
    private static byte[] _binFromStr(string s, int need)
    {
        byte[] b = new byte[Math.Max(need, s?.Length ?? 0)];
        if (s != null)
            for (int i = 0; i < s.Length && i < b.Length; i++)
                b[i] = (byte)(s[i] & 0xFF);
        return b;
    }
    private static string _binToStr(byte[] b)
    {
        char[] c = new char[b.Length];
        for (int i = 0; i < b.Length; i++) c[i] = (char)b[i];
        return new string(c);
    }
    public static decimal BIN2UI(string s) =>
        System.BitConverter.ToUInt16(_binFromStr(s, 2), 0);
    public static decimal BIN2UL(string s) =>
        System.BitConverter.ToUInt32(_binFromStr(s, 4), 0);
    public static decimal BIN2LL(string s) =>
        System.BitConverter.ToInt64(_binFromStr(s, 8), 0);
    // UL2BIN is already defined further up.
    public static string LL2BIN(decimal n) =>
        _binToStr(System.BitConverter.GetBytes((ulong)(long)n));
    public static decimal C4W_LOWORD(decimal n) => ((long)n) & 0xFFFF;
    public static decimal C4W_HIWORD(decimal n) => (((long)n) >> 16) & 0xFFFF;

    // CRC-16-CCITT (poly 0x1021). Byte-swapped result matches bin.c.
    private static readonly ushort[] _crctab = new ushort[] {
        0x0000, 0x1021, 0x2042, 0x3063, 0x4084, 0x50a5, 0x60c6, 0x70e7,
        0x8108, 0x9129, 0xa14a, 0xb16b, 0xc18c, 0xd1ad, 0xe1ce, 0xf1ef,
        0x1231, 0x0210, 0x3273, 0x2252, 0x52b5, 0x4294, 0x72f7, 0x62d6,
        0x9339, 0x8318, 0xb37b, 0xa35a, 0xd3bd, 0xc39c, 0xf3ff, 0xe3de,
        0x2462, 0x3443, 0x0420, 0x1401, 0x64e6, 0x74c7, 0x44a4, 0x5485,
        0xa56a, 0xb54b, 0x8528, 0x9509, 0xe5ee, 0xf5cf, 0xc5ac, 0xd58d,
        0x3653, 0x2672, 0x1611, 0x0630, 0x76d7, 0x66f6, 0x5695, 0x46b4,
        0xb75b, 0xa77a, 0x9719, 0x8738, 0xf7df, 0xe7fe, 0xd79d, 0xc7bc,
        0x48c4, 0x58e5, 0x6886, 0x78a7, 0x0840, 0x1861, 0x2802, 0x3823,
        0xc9cc, 0xd9ed, 0xe98e, 0xf9af, 0x8948, 0x9969, 0xa90a, 0xb92b,
        0x5af5, 0x4ad4, 0x7ab7, 0x6a96, 0x1a71, 0x0a50, 0x3a33, 0x2a12,
        0xdbfd, 0xcbdc, 0xfbbf, 0xeb9e, 0x9b79, 0x8b58, 0xbb3b, 0xab1a,
        0x6ca6, 0x7c87, 0x4ce4, 0x5cc5, 0x2c22, 0x3c03, 0x0c60, 0x1c41,
        0xedae, 0xfd8f, 0xcdec, 0xddcd, 0xad2a, 0xbd0b, 0x8d68, 0x9d49,
        0x7e97, 0x6eb6, 0x5ed5, 0x4ef4, 0x3e13, 0x2e32, 0x1e51, 0x0e70,
        0xff9f, 0xefbe, 0xdfdd, 0xcffc, 0xbf1b, 0xaf3a, 0x9f59, 0x8f78,
        0x9188, 0x81a9, 0xb1ca, 0xa1eb, 0xd10c, 0xc12d, 0xf14e, 0xe16f,
        0x1080, 0x00a1, 0x30c2, 0x20e3, 0x5004, 0x4025, 0x7046, 0x6067,
        0x83b9, 0x9398, 0xa3fb, 0xb3da, 0xc33d, 0xd31c, 0xe37f, 0xf35e,
        0x02b1, 0x1290, 0x22f3, 0x32d2, 0x4235, 0x5214, 0x6277, 0x7256,
        0xb5ea, 0xa5cb, 0x95a8, 0x8589, 0xf56e, 0xe54f, 0xd52c, 0xc50d,
        0x34e2, 0x24c3, 0x14a0, 0x0481, 0x7466, 0x6447, 0x5424, 0x4405,
        0xa7db, 0xb7fa, 0x8799, 0x97b8, 0xe75f, 0xf77e, 0xc71d, 0xd73c,
        0x26d3, 0x36f2, 0x0691, 0x16b0, 0x6657, 0x7676, 0x4615, 0x5634,
        0xd94c, 0xc96d, 0xf90e, 0xe92f, 0x99c8, 0x89e9, 0xb98a, 0xa9ab,
        0x5844, 0x4865, 0x7806, 0x6827, 0x18c0, 0x08e1, 0x3882, 0x28a3,
        0xcb7d, 0xdb5c, 0xeb3f, 0xfb1e, 0x8bf9, 0x9bd8, 0xabbb, 0xbb9a,
        0x4a75, 0x5a54, 0x6a37, 0x7a16, 0x0af1, 0x1ad0, 0x2ab3, 0x3a92,
        0xfd2e, 0xed0f, 0xdd6c, 0xcd4d, 0xbdaa, 0xad8b, 0x9de8, 0x8dc9,
        0x7c26, 0x6c07, 0x5c64, 0x4c45, 0x3ca2, 0x2c83, 0x1ce0, 0x0cc1,
        0xef1f, 0xff3e, 0xcf5d, 0xdf7c, 0xaf9b, 0xbfba, 0x8fd9, 0x9ff8,
        0x6e17, 0x7e36, 0x4e55, 0x5e74, 0x2e93, 0x3eb2, 0x0ed1, 0x1ef0
    };
    public static decimal CRC16(string s)
    {
        ushort crc = 0;
        if (s != null)
            foreach (char ch in s)
                crc = (ushort)((crc << 8) ^ _crctab[((crc >> 8) ^ (ch & 0xFF)) & 0xFF]);
        return (ushort)((crc >> 8) | ((crc << 8) & 0xFF00));
    }
}

// Wrapper returned by `obj:Super()` — keeps the :className() chain working.
public class HbSuperRef
{
    public Type? t;
    public string className() => t?.Name ?? "";
    public string ClassName() => t?.Name ?? "";
}

// Extension methods so every object supports :className / :Super.
public static class HbObjectExtensions
{
    public static string className(this object obj) => obj?.GetType().Name ?? "";
    public static string ClassName(this object obj) => obj?.GetType().Name ?? "";
    public static HbSuperRef Super(this object obj) =>
        new HbSuperRef { t = obj?.GetType().BaseType };
}
// Shared throwaway storage for omitted by-ref output arguments. A
// Harbour caller may omit a by-ref param (Foo(n) where Foo(n, @out));
// C# can't omit a ref param, so the transpiler passes
// `ref HbDiscard<T>.Value` for the omitted slot. The write-back lands
// here and is discarded -- matching Harbour's NIL-arg semantics.
public static class HbDiscard<T> { public static T Value = default!; }
