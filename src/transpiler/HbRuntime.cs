using System;
using System.Globalization;

/// <summary>
/// Harbour runtime function implementations for transpiled C# code.
/// All Harbour builtin functions are available as static methods.
/// </summary>
public static class HbRuntime
{
    static readonly CultureInfo INV = CultureInfo.InvariantCulture;

    // ---- Console output (? and ??) ----

    public static void QOut(params dynamic[] args)
    {
        Console.WriteLine();
        foreach (var a in args)
            Console.Write(Fmt(a));
    }

    public static void QQOut(params dynamic[] args)
    {
        foreach (var a in args)
            Console.Write(Fmt(a));
    }

    static string Fmt(dynamic a) =>
        a is decimal d ? Str(d) : Convert.ToString(a, INV);

    // ---- Numeric functions ----

    public static string Str(decimal? nOrNull, int nWidth = 10, int nDec = -1)
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

    public static decimal Val(string s)
    {
        decimal.TryParse(s, NumberStyles.Any, INV, out decimal result);
        return result;
    }

    public static decimal Int(decimal n) => Math.Truncate(n);
    public static decimal Round(decimal n, int nDec = 0) => Math.Round(n, nDec);
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
    public static string Space(int n) => new string(' ', n);
    public static string Replicate(string s, int n) => string.Concat(System.Linq.Enumerable.Repeat(s, n));
    public static string Chr(int n) => ((char)n).ToString();
    public static decimal Asc(string s) => s.Length > 0 ? (decimal)s[0] : 0;

    public static string SubStr(string s, int nStart, int nLen = -1)
    {
        // Harbour is 1-based
        nStart = Math.Max(nStart - 1, 0);
        if (nStart >= s.Length) return "";
        if (nLen < 0) return s.Substring(nStart);
        nLen = Math.Min(nLen, s.Length - nStart);
        return s.Substring(nStart, nLen);
    }

    public static string Left(string s, int n) => SubStr(s, 1, n);
    public static string Right(string s, int n) => n >= s.Length ? s : s.Substring(s.Length - n);

    public static decimal At(string cSearch, string cString) =>
        (decimal)(cString.IndexOf(cSearch, StringComparison.Ordinal) + 1);

    public static decimal RAt(string cSearch, string cString) =>
        (decimal)(cString.LastIndexOf(cSearch, StringComparison.Ordinal) + 1);

    public static string PadL(string s, int nLen, string cFill = " ") =>
        s.Length >= nLen ? s : s.PadLeft(nLen, cFill[0]);

    public static string PadR(string s, int nLen, string cFill = " ") =>
        s.Length >= nLen ? s : s.PadRight(nLen, cFill[0]);

    public static string PadC(string s, int nLen, string cFill = " ")
    {
        if (s.Length >= nLen) return s;
        int nLeft = (nLen - s.Length) / 2;
        return s.PadLeft(s.Length + nLeft, cFill[0]).PadRight(nLen, cFill[0]);
    }

    public static string StrTran(string cString, string cSearch, string cReplace = "") =>
        cString.Replace(cSearch, cReplace);

    public static string Stuff(string cString, int nStart, int nDel, string cInsert)
    {
        nStart = Math.Max(nStart - 1, 0);
        if (nStart >= cString.Length) return cString + cInsert;
        return cString.Substring(0, nStart) + cInsert + cString.Substring(Math.Min(nStart + nDel, cString.Length));
    }

    public static string Transform(dynamic val, string cMask) => Convert.ToString(val, INV);
    public static string StrZero(decimal n, int nLen = 10, int nDec = 0) =>
        n.ToString("F" + nDec, INV).PadLeft(nLen, '0');

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

    public static DateTime Date() => DateTime.Today;
    public static DateTime CToD(string s) { DateTime.TryParse(s, out DateTime d); return d; }
    public static DateTime SToD(string s) { DateTime.TryParseExact(s, "yyyyMMdd", INV, DateTimeStyles.None, out DateTime d); return d; }
    public static string DToC(DateTime d) => d.ToString("MM/dd/yyyy", INV);
    public static string DToS(DateTime d) => d.ToString("yyyyMMdd", INV);
    public static string Time() => DateTime.Now.ToString("HH:mm:ss", INV);

    // ---- Terminal ----

    public static void SetColor(string cColor) { }
}
