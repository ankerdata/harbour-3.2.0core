using System;
using System.Globalization;

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

    // ---- Console output (? and ??) ----

    public static void QOUT(params dynamic[] args)
    {
        Console.WriteLine();
        foreach (var a in args)
            Console.Write(Fmt(a));
    }

    public static void QQOUT(params dynamic[] args)
    {
        foreach (var a in args)
            Console.Write(Fmt(a));
    }

    static string Fmt(dynamic a) =>
        a is decimal d ? STR(d) : Convert.ToString(a, INV);

    // ---- Numeric functions ----

    public static string STR(decimal? nOrNull, int nWidth = 10, int nDec = -1)
    {
        // Accepts nullable decimals so transpiled code can pass parameters
        // marked nilable in the by-ref table without an explicit cast.
        // NIL in the source becomes null here, which we render as "".
        if (nOrNull is null)
            return "".PadLeft(nWidth);
        decimal n = nOrNull.Value;
        string s;
        if (nDec >= 0)
            s = n.ToString("F" + nDec, INV);
        else if (n == Math.Truncate(n))
            s = n.ToString("F0", INV);
        else
            s = n.ToString("G", INV);
        return s.PadLeft(nWidth);
    }

    public static decimal VAL(string s)
    {
        decimal.TryParse(s, NumberStyles.Any, INV, out decimal result);
        return result;
    }

    public static decimal INT(decimal n) => Math.Truncate(n);
    public static decimal ROUND(decimal n, int nDec = 0) => Math.Round(n, nDec);
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
    public static string SPACE(int n) => new string(' ', n);
    public static string REPLICATE(string s, int n) => string.Concat(System.Linq.Enumerable.Repeat(s, n));
    public static string CHR(int n) => ((char)n).ToString();
    public static decimal ASC(string s) => s.Length > 0 ? (decimal)s[0] : 0;

    public static string SUBSTR(string s, int nStart, int nLen = -1)
    {
        // Harbour is 1-based
        nStart = Math.Max(nStart - 1, 0);
        if (nStart >= s.Length) return "";
        if (nLen < 0) return s.Substring(nStart);
        nLen = Math.Min(nLen, s.Length - nStart);
        return s.Substring(nStart, nLen);
    }

    public static string LEFT(string s, int n) => SUBSTR(s, 1, n);
    public static string RIGHT(string s, int n) => n >= s.Length ? s : s.Substring(s.Length - n);

    public static decimal AT(string cSearch, string cString) =>
        (decimal)(cString.IndexOf(cSearch, StringComparison.Ordinal) + 1);

    public static decimal RAT(string cSearch, string cString) =>
        (decimal)(cString.LastIndexOf(cSearch, StringComparison.Ordinal) + 1);

    public static string PADL(string s, int nLen, string cFill = " ") =>
        s.Length >= nLen ? s : s.PadLeft(nLen, cFill[0]);

    public static string PADR(string s, int nLen, string cFill = " ") =>
        s.Length >= nLen ? s : s.PadRight(nLen, cFill[0]);

    public static string PADC(string s, int nLen, string cFill = " ")
    {
        if (s.Length >= nLen) return s;
        int nLeft = (nLen - s.Length) / 2;
        return s.PadLeft(s.Length + nLeft, cFill[0]).PadRight(nLen, cFill[0]);
    }

    public static string STRTRAN(string cString, string cSearch, string cReplace = "") =>
        cString.Replace(cSearch, cReplace);

    public static string STUFF(string cString, int nStart, int nDel, string cInsert)
    {
        nStart = Math.Max(nStart - 1, 0);
        if (nStart >= cString.Length) return cString + cInsert;
        return cString.Substring(0, nStart) + cInsert + cString.Substring(Math.Min(nStart + nDel, cString.Length));
    }

    public static string TRANSFORM(dynamic val, string cMask) => Convert.ToString(val, INV);
    public static string STRZERO(decimal n, int nLen = 10, int nDec = 0) =>
        n.ToString("F" + nDec, INV).PadLeft(nLen, '0');

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
}
