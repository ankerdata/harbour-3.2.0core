using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

/// <summary>
/// Harbour's core runtime library for transpiled C# code, plus the
/// helpers the emitter itself calls (Eval, StrCmp, HbIn, FuncPtr, ...).
/// Nothing application-specific belongs here: EasiPOS's own C functions
/// live in the application's EasiPosNative project, and a function the
/// program defines in .prg is always the program's (gencsharp.c,
/// hb_csFuncTabPrefix).
///
/// Method names are spelt as Harbour's include/*.hbx lists them
/// (`SubStr`, `hb_HGetDef`): the emitter writes every core call in that
/// canonical spelling whatever casing the source used, and C# is
/// case-sensitive. A name spelt any other way is never called.
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
        IsNumeric(a) ? Str(Convert.ToDecimal(a, INV)) :
        Convert.ToString(a, INV);

    // A Harbour number in any of the CLR's numeric types: a decimal as a
    // rule, but an int or long where an integer literal crossed a
    // dynamic boundary, a uint for a literal past int's range
    // (2147483648), a double from a library
    static bool IsNumeric(object x) =>
        x is decimal or int or long or double or float or short or byte
          or sbyte or ushort or uint or ulong;

    // ---- Numeric functions ----

    // Str( <n>[, <nWidth>[, <nDec>]] ), as vm/itemapi.c's hb_itemStr has
    // it. A width alone means no decimals; a width below 1 means 10;
    // decimals are taken out of the width (Str( n, 8, 2 ) is 8 wide), and
    // a width of 1 ignores them. Without a width, the width and decimals
    // the number carries (StrDefault). A null (NIL) number is rendered as
    // spaces, so transpiled code passing a nilable parameter keeps
    // running (Harbour raises an argument error).
    public static string Str(decimal? nOrNull, decimal? nWidth = null, decimal? nDec = null)
    {
        decimal n = nOrNull ?? 0;
        int iWidth, iDec;
        if (nWidth is null)
            (iWidth, iDec) = StrDefault(n);
        else
        {
            iWidth = (int)nWidth.Value;
            if (iWidth < 1)
                iWidth = 10;
            iDec = 0;
        }
        if (iWidth > 1 && nDec is not null)
        {
            iDec = Math.Max((int)nDec.Value, 0);
            if (iDec > 0 && nWidth is not null)
                iWidth -= iDec + 1;
        }
        int iSize = iDec > 0 ? iWidth + 1 + iDec : iWidth;
        if (iSize <= 0)
            return "";
        if (nOrNull is null)
            return "".PadLeft(iSize);
        return NumToStr(n, iSize, iDec);
    }

    // The width and decimals Str( n ) uses when it is given none. Harbour
    // takes them from the number, which carries both; a C# decimal
    // carries its decimals, as its scale, and they follow Harbour's rules
    // for literals, + - *, Val() and Round(). A value with more than 16
    // significant digits is a division or math result that did not come
    // out even, which Harbour shows with SET DECIMALS (100/13 is "7.69").
    // The width is Harbour's default, 10 integer places, 20 for a large
    // value (hbdefs.h, HB_INT_LENGTH and HB_DBL_LENGTH). Divergent by
    // design (Alex, 2026-09-19): a division that comes out even keeps its
    // own decimals (10/4 is "2.5", Harbour "2.50"), and a number from
    // Val() or Year() gets the default width rather than its own.
    static (int iWidth, int iDec) StrDefault(decimal n)
    {
        int iDec = n.Scale;
        if (SignificantDigits(n) > 16)
            iDec = SetDecimals();
        bool fBig = iDec == 0 ? n > 999999999 || n < -999999999
                              : n > 9999999999m || n < -999999999m;
        return (fBig ? 20 : 10, iDec);
    }

    // Significant digits of n, trailing zeros not counted: 7.69230769...
    // (a quotient) has 29, 20052.0240 has 8.
    static int SignificantDigits(decimal n) =>
        Math.Abs(n / 1.0000000000000000000000000000m).ToString(INV)
            .Replace(".", "").TrimStart('0').Length;

    // hb_itemStrBuf: n rounded half away from zero to iDec decimals,
    // right-aligned in iSize characters, or iSize asterisks when it does
    // not fit. Harbour's doubles carry 16 significant digits, so a digit
    // past the 16th shows as 0 (Str( Sqrt( 3 ), 21, 18 ) is
    // " 1.732050807568877000"); the minus sign is shown only when a
    // digit is not 0 (Str( -0.4, 5 ) is "    0").
    static string NumToStr(decimal n, int iSize, int iDec)
    {
        int iRound = iDec;
        if (n != 0 && !IsHbInteger(n))
            iRound = Math.Min(iDec, 15 - Exponent10(Math.Abs(n)));
        decimal r = RoundAway(n, iRound);
        string s = Math.Abs(r).ToString("F" + iDec, INV);
        if (r != 0 && n < 0)
            s = "-" + s;
        return s.Length > iSize ? new string('*', iSize) : s.PadLeft(iSize);
    }

    // A double result as a decimal with all of its round-trip digits (17
    // significant), which Str() cuts to the 16 Harbour shows; (decimal)d
    // would keep only 15, and Str( Sqrt( 3 ), 21, 18 ) would lose a digit.
    static decimal FromDouble(double d) =>
        decimal.Parse(d.ToString("R", INV), NumberStyles.Float, INV);

    // Would Harbour hold n as an integer rather than a double? Written
    // without decimals and within 64 bits. Harbour prints an integer
    // exactly and a double to 16 digits, and SET FIXED treats the two
    // apart; a decimal's scale says which a value is (0.0 is a double).
    static bool IsHbInteger(decimal n) =>
        n.Scale == 0 && n >= long.MinValue && n <= long.MaxValue;

    // floor( log10( a ) ) for a > 0, exactly
    static int Exponent10(decimal a)
    {
        int e = 0;
        if (a >= 1)
            for (; a >= 10; e++)
                a /= 10;
        else
            for (; a < 1; e--)
                a *= 10;
        return e;
    }

    // n rounded half away from zero to iDec decimals; a negative iDec
    // rounds to tens, hundreds, ... as Harbour's Round() does
    static decimal RoundAway(decimal n, int iDec)
    {
        if (iDec >= 0)
            return Math.Round(n, Math.Min(iDec, 28), MidpointRounding.AwayFromZero);
        if (iDec < -28)
            return 0;
        decimal p = 1;
        for (int i = 0; i < -iDec; i++)
            p *= 10;
        return Math.Round(n / p, MidpointRounding.AwayFromZero) * p;
    }

    // ToString() is a recurring easipos idiom for Harbour's Str() —
    // a C#-influenced habit among the contributors. The transpiler
    // routes bare `ToString(x)` call sites here (see hb_csFuncMap in
    // gencsharp.c). Same formatting contract as Str so the two are
    // interchangeable; kept under its own name to keep source and
    // emit visually aligned.
    public static string ToString(decimal? nOrNull, decimal? nWidth = null, decimal? nDec = null) =>
        Str(nOrNull, nWidth, nDec);

    // Val(): the number at the start of the string (common/hbstr.c,
    // hb_str2number) — blanks, tabs, LF and CR skipped, one sign, digits
    // and one decimal point, up to the first other character: Val( "12abc" )
    // is 12. Harbour sizes the result so that Str() gives back text as
    // long as the string read: its decimals are the digits after the
    // point PLUS every character after the first one that is not a digit,
    // so Val( "15.1A10" ) is 15.1 shown as "15.1000", Val( "1..." ) is 1
    // shown as "1.00". The decimals go into the result's scale (the width
    // cannot be carried; Alex, 2026-09-19). A number with a decimal point
    // is a double in Harbour, so one of more than 16 significant digits
    // is rounded to what the double holds: Val( "2.0000000000000001" ) is 2.
    public static decimal Val(string s)
    {
        if (string.IsNullOrEmpty(s))
            return 0;
        int n = s.Length, i = 0;
        while (i < n && s[i] is ' ' or '\t' or '\n' or '\r')
            i++;
        bool fNeg = false;
        if (i < n && s[i] == '-')
        {
            fNeg = true;
            i++;
        }
        else if (i < n && s[i] == '+')
            i++;
        var sbDigits = new System.Text.StringBuilder();
        bool fDec = false;
        int iDec = 0, iDecR = 0;
        for (; i < n; i++)
        {
            char c = s[i];
            if (c >= '0' && c <= '9')
            {
                sbDigits.Append(c);
                if (fDec)
                    iDec++;
            }
            else if (c == '.' && !fDec)
                fDec = true;
            else
            {
                // the value stops here; the rest still counts as decimals
                // (after a point found now or earlier)
                while (!fDec && i < n)
                    if (s[i++] == '.')
                        fDec = true;
                if (fDec)
                    iDecR = n - i;
                break;
            }
        }
        string cDigits = sbDigits.ToString();
        string cInt = cDigits.Substring(0, cDigits.Length - iDec);
        string cText = (cInt.Length > 0 ? cInt : "0") +
                       (iDec > 0 ? "." + cDigits.Substring(cDigits.Length - iDec) : "");
        if (!decimal.TryParse(cText, NumberStyles.AllowDecimalPoint, INV, out decimal nValue))
            nValue = FromDouble(double.Parse(cText, INV));
        if ((fDec || !IsHbInteger(nValue)) && nValue != 0)
        {
            int iRound = 15 - Exponent10(nValue);
            if (iRound < nValue.Scale)
                nValue = RoundAway(nValue, iRound);
        }
        int iScale = Math.Min(iDec + iDecR, 28);
        if (iScale > nValue.Scale)
            nValue += new decimal(0, 0, 0, false, (byte)iScale);
        return fNeg ? -nValue : nValue;
    }

    public static decimal Int(decimal n) => Math.Truncate(n);
    // Harbour Round() is half-away-from-zero; Math.Round defaults to
    // banker's rounding (ToEven), which would skew POS money math. A
    // negative nDec rounds to tens, hundreds, ...: Round( 150, -2 ) is 200.
    // The result carries nDec decimals, as Harbour's does, so Str() shows
    // them: Round( 1.5, 2 ) is "1.50" (adding a zero of that scale raises
    // the decimal's scale without changing its value).
    public static decimal Round(decimal n, decimal nDec = 0)
    {
        int iDec = (int)nDec;
        decimal r = RoundAway(n, iDec);
        return iDec > 0 ? r + new decimal(0, 0, 0, false, (byte)Math.Min(iDec, 28)) : r;
    }
    public static decimal Abs(decimal n) => Math.Abs(n);
    public static decimal Max(decimal a, decimal b) => Math.Max(a, b);
    public static decimal Min(decimal a, decimal b) => Math.Min(a, b);
    public static decimal Mod(decimal a, decimal b) => a % b;
    public static decimal Pow(decimal a, decimal b) => FromDouble(Math.Pow((double)a, (double)b));
    // Harbour's Sqrt() of 0 or less is 0 (rtl/math.c)
    public static decimal Sqrt(decimal n) => n <= 0 ? 0 : FromDouble(Math.Sqrt((double)n));
    public static decimal Log(decimal n) => (decimal)Math.Log((double)n);
    public static decimal Exp(decimal n) => (decimal)Math.Exp((double)n);

    // Harbour bit ops (hb_bit*). Variadic like the RTL functions; operands
    // are truncated to Int64. Result stays long so callers can compare it
    // to an int/long/decimal mask (`hb_bitAnd(nFlags, MASK) == MASK`).
    public static dynamic hb_bitAnd(params dynamic[] args)
    {
        long r = ~0L;
        foreach (var a in args) r &= Convert.ToInt64(a);
        return r;
    }
    public static dynamic hb_bitOr(params dynamic[] args)
    {
        long r = 0L;
        foreach (var a in args) r |= Convert.ToInt64(a);
        return r;
    }
    public static dynamic hb_bitXor(params dynamic[] args)
    {
        long r = 0L;
        foreach (var a in args) r ^= Convert.ToInt64(a);
        return r;
    }

    // ---- String functions ----

    /* FOR EACH whose body reads an enumerator message (`x:__enumKey()`):
       the emitter iterates these pairs and binds the loop variable to
       Value. A hash yields (key, value), an array (1-based index,
       element), a string (index, character). */
    public static IEnumerable<KeyValuePair<dynamic, dynamic>> HbEnumPairs(dynamic x, bool reverse = false)
    {
        var list = new List<KeyValuePair<dynamic, dynamic>>();
        if (x is System.Collections.IDictionary d)
        {
            foreach (System.Collections.DictionaryEntry e in d)
                list.Add(new KeyValuePair<dynamic, dynamic>(e.Key, e.Value));
        }
        else if (x is string s)
        {
            for (int i = 0; i < s.Length; i++)
                list.Add(new KeyValuePair<dynamic, dynamic>((decimal)(i + 1), s[i].ToString()));
        }
        else if (x is System.Collections.IEnumerable en)
        {
            decimal i = 0;
            foreach (var e in en)
                list.Add(new KeyValuePair<dynamic, dynamic>(++i, e));
        }
        if (reverse)
            list.Reverse();
        return list;
    }

    /* The enumerable of a FOR EACH: Harbour hands out a hash's VALUES
       and a string's characters as one-character strings, where C#
       would hand out pairs and chars. Arrays are iterated directly by
       the emitter; everything else comes through here. */
    public static IEnumerable<dynamic> HbEnumValues(dynamic x, bool reverse = false)
    {
        var list = new List<dynamic>();
        if (x is System.Collections.IDictionary d)
        {
            foreach (System.Collections.DictionaryEntry e in d)
                list.Add(e.Value);
        }
        else if (x is string s)
        {
            foreach (char c in s)
                list.Add(c.ToString());
        }
        else if (x is System.Collections.IEnumerable en)
        {
            foreach (var e in en)
                list.Add(e);
        }
        if (reverse)
            list.Reverse();
        return list;
    }

    public static decimal Len(dynamic x)
    {
        if (x is string s) return s.Length;
        if (x is System.Array a) return a.Length;
        // Harbour Len() of a hash is its key count.
        if (x is System.Collections.IDictionary h) return h.Count;
        return 0;
    }

    // Upper()/Lower() as a single-byte Windows codepage has them: ASCII,
    // and a letter above 127 only when its other case is a single-byte
    // character too (ä ↔ Ä; µ and ÿ have none there and stay). Invariant,
    // so no culture's rules (Turkish i) apply. EasiPOS selects ENWIN,
    // DEWIN, CSWIN or PLWIN; how its strings carry those codepages is the
    // deferred codepage question.
    static char UpperChar(char c)
    {
        if (c < 128)
            return c >= 'a' && c <= 'z' ? (char)(c - 32) : c;
        char u = char.ToUpperInvariant(c);
        return c > 'ÿ' || u <= 'ÿ' ? u : c;
    }

    static char LowerChar(char c)
    {
        if (c < 128)
            return c >= 'A' && c <= 'Z' ? (char)(c + 32) : c;
        char l = char.ToLowerInvariant(c);
        return c > 'ÿ' || l <= 'ÿ' ? l : c;
    }

    public static string Upper(string s) => string.Create(s.Length, s, (span, src) =>
    {
        for (int i = 0; i < src.Length; i++)
            span[i] = UpperChar(src[i]);
    });

    public static string Lower(string s) => string.Create(s.Length, s, (span, src) =>
    {
        for (int i = 0; i < src.Length; i++)
            span[i] = LowerChar(src[i]);
    });

    // The trims (rtl/trim.c): RTrim()/Trim() take off trailing blanks
    // only; LTrim() leading blanks, tabs, CRs and LFs (HB_ISSPACE);
    // AllTrim() both.
    public static string Trim(string s) => s.TrimEnd(' ');
    public static string RTrim(string s) => s.TrimEnd(' ');
    public static string LTrim(string s) => s.TrimStart(' ', '\t', '\n', '\r');
    public static string AllTrim(string s) => s.TrimEnd(' ').TrimStart(' ', '\t', '\n', '\r');

    public static string Space(decimal n) => n >= 1 ? new string(' ', (int)n) : "";

    public static string Replicate(string s, decimal n) =>
        n >= 1 && s.Length > 0 ? string.Concat(System.Linq.Enumerable.Repeat(s, (int)n)) : "";

    // Chr(): the character of the number's low byte (rtl/chrasc.c):
    // Chr( 256 ) is Chr( 0 ), Chr( -65 ) is Chr( 191 )
    public static string Chr(decimal n) =>
        ((char)((long)Math.Truncate(n % 65536) & 0xFF)).ToString();

    public static decimal Asc(string s) => s.Length > 0 ? (decimal)s[0] : 0;

    // SubStr( <c>, <nStart>[, <nCount>] ) (rtl/substr.c): a start of 0 is
    // 1; a negative start counts from the end (SubStr( "abcdef", -2 ) is
    // "ef", and one before the beginning is the beginning); a count of 0
    // or less is "".
    public static string SubStr(string s, decimal nStart, decimal? nCount = null)
    {
        long nSize = s.Length;
        long nFrom = (long)Math.Truncate(nStart);
        long nLen = nCount is null ? nSize : (long)Math.Truncate(nCount.Value);
        if (nFrom > 0 && --nFrom > nSize)
            nLen = 0;
        if (nLen <= 0)
            return "";
        if (nFrom < 0)
            nFrom += nSize;
        if (nFrom < 0)
            nFrom = 0;
        nLen = Math.Min(nLen, nSize - nFrom);
        return nLen > 0 ? s.Substring((int)nFrom, (int)nLen) : "";
    }

    public static string Left(string s, decimal n)
    {
        long i = (long)Math.Truncate(n);
        if (i <= 0)
            return "";
        return i >= s.Length ? s : s.Substring(0, (int)i);
    }

    public static string Right(string s, decimal n)
    {
        long i = (long)Math.Truncate(n);
        if (i <= 0)
            return "";
        return i >= s.Length ? s : s.Substring(s.Length - (int)i);
    }

    // At()/RAt(): 1-based position of the first/last occurrence, 0 when
    // there is none — and 0 for an empty search string, which C#'s
    // IndexOf() finds at 0
    public static decimal At(string cSearch, string cString) =>
        cSearch.Length == 0 ? 0 : cString.IndexOf(cSearch, StringComparison.Ordinal) + 1;

    public static decimal RAt(string cSearch, string cString) =>
        cSearch.Length == 0 ? 0 : cString.LastIndexOf(cSearch, StringComparison.Ordinal) + 1;

    // hb_At( <cSearch>, <cString>[, <nStart>[, <nEnd>]] ) (rtl/at.c): At()
    // within cString's nStart..nEnd, the position still counted from its
    // beginning
    public static decimal hb_At(string cSearch, string cString, decimal nStart = 0, decimal? nEnd = null)
    {
        long nFrom = (long)Math.Truncate(nStart);
        nFrom = nFrom <= 1 ? 0 : nFrom - 1;
        if (cSearch.Length == 0 || nFrom >= cString.Length)
            return 0;
        long nTo = cString.Length - nFrom;
        if (nEnd is not null)
        {
            long n = (long)Math.Truncate(nEnd.Value);
            nTo = n <= nFrom ? 0 : Math.Min(n - nFrom, nTo);
        }
        if (nTo <= 0)
            return 0;
        int iPos = cString.IndexOf(cSearch, (int)nFrom, (int)nTo, StringComparison.Ordinal);
        return iPos < 0 ? 0 : iPos + 1;
    }

    // PadL()/PadR()/PadC() (rtl/padx.c): a length of 0 or less is ""; a
    // longer string is cut to its first nLen characters, whichever the
    // side; the fill is cFill's first character (blank by default; an
    // empty cFill fills with Chr( 0 )). NIL pads to "".
    static string Pad(string s, decimal nLen, string cFill, int iMode)
    {
        long n = (long)Math.Truncate(nLen);
        if (n <= 0 || s is null)
            return "";
        if (n <= s.Length)
            return n == s.Length ? s : s.Substring(0, (int)n);
        char c = cFill is null ? ' ' : cFill.Length > 0 ? cFill[0] : '\0';
        int nPad = (int)n - s.Length;
        int nLeft = iMode == 0 ? nPad : iMode == 2 ? nPad >> 1 : 0;
        return new string(c, nLeft) + s + new string(c, nPad - nLeft);
    }

    public static string PadL(string s, decimal nLen, string cFill = null) => Pad(s, nLen, cFill, 0);
    public static string PadR(string s, decimal nLen, string cFill = null) => Pad(s, nLen, cFill, 1);
    public static string PadC(string s, decimal nLen, string cFill = null) => Pad(s, nLen, cFill, 2);

    // StrTran( <c>, <cSearch>[, <cReplace>[, <nStart>[, <nCount>]]] )
    // (rtl/strtran.c): replaces the occurrences from the nStart'th on,
    // nCount of them (all by default). A start or count of 0 gives "";
    // an empty search string changes nothing.
    public static string StrTran(string cString, string cSearch, string cReplace = null,
                                 decimal? nStart = null, decimal? nCount = null)
    {
        long iStart = nStart is null ? 1 : (long)Math.Truncate(nStart.Value);
        long iCount = nCount is null ? -1 : (long)Math.Truncate(nCount.Value);
        if (iStart == 0 || iCount == 0)
            return "";
        if (cSearch.Length == 0 || cSearch.Length > cString.Length || iStart < 0)
            return cString;
        cReplace ??= "";
        var sb = new System.Text.StringBuilder(cString.Length);
        int iPos = 0, iFound = 0;
        while (iCount != 0)
        {
            int iAt = cString.IndexOf(cSearch, iPos, StringComparison.Ordinal);
            if (iAt < 0)
                break;
            sb.Append(cString, iPos, iAt - iPos);
            if (++iFound >= iStart)
            {
                sb.Append(cReplace);
                if (iCount > 0)
                    iCount--;
            }
            else
                sb.Append(cSearch);
            iPos = iAt + cSearch.Length;
        }
        sb.Append(cString, iPos, cString.Length - iPos);
        return sb.ToString();
    }

    // Stuff( <c>, <nStart>, <nDelete>, <cInsert> ) (rtl/stuff.c). The C
    // holds the position and count unsigned, so a negative (or too large)
    // start means the end of the string, and a negative (or too large)
    // count means up to the end; a start of 0 inserts at the front.
    public static string Stuff(string cString, decimal nStart, decimal nDel, string cInsert)
    {
        if (cString is null || cInsert is null)
            return "";
        long nLen = cString.Length;
        long iPos = (long)Math.Truncate(nStart), iDel = (long)Math.Truncate(nDel);
        if (iPos != 0)
            iPos = iPos < 1 || iPos > nLen ? nLen : iPos - 1;
        if (iDel != 0 && (iDel < 1 || iDel > nLen - iPos))
            iDel = nLen - iPos;
        return cString.Substring(0, (int)iPos) + cInsert + cString.Substring((int)(iPos + iDel));
    }

    // hb_StrShrink( <c>[, <nShrinkBy> = 1] ): c without its last
    // nShrinkBy characters; nothing is taken off for 0 or less
    public static string hb_StrShrink(string s, decimal nShrinkBy = 1)
    {
        long n = (long)Math.Truncate(nShrinkBy);
        if (n <= 0)
            return s;
        return n < s.Length ? s.Substring(0, s.Length - (int)n) : "";
    }

    // hb_ntos( <n> ): Str( n ) without its leading blanks
    public static string hb_ntos(decimal n) => Str(n).TrimStart(' ');

    // hb_eol(): the platform's end of line
    public static string hb_eol() => "\r\n";

    // hb_BLen()/hb_BLeft()/hb_BSubStr(): the byte versions of Len(),
    // Left() and SubStr(). A Harbour string is bytes and a transpiled one
    // holds a byte per char, so they are the same functions.
    public static decimal hb_BLen(string s) => s.Length;
    public static string hb_BLeft(string s, decimal n) => Left(s, n);
    public static string hb_BSubStr(string s, decimal nStart, decimal? nCount = null) => SubStr(s, nStart, nCount);

    // hb_StrToHex( <c>[, <cSeparator>] ): each byte as two upper-case hex
    // digits, separated by cSeparator (rtl/hbhex.c)
    public static string hb_StrToHex(string s, string cSeparator = "")
    {
        var sb = new System.Text.StringBuilder(s.Length * (2 + cSeparator.Length));
        for (int i = 0; i < s.Length; i++)
        {
            if (i > 0)
                sb.Append(cSeparator);
            sb.Append(((int)s[i] & 0xFF).ToString("X2", INV));
        }
        return sb.ToString();
    }

    // hb_HexToStr( <cHex> ): the bytes the hex digits spell, two to a
    // byte; any other character is skipped, as is an odd last digit
    public static string hb_HexToStr(string cHex)
    {
        var sb = new System.Text.StringBuilder(cHex.Length / 2);
        int iHigh = -1;
        foreach (char c in cHex)
        {
            int d = c >= '0' && c <= '9' ? c - '0'
                  : c >= 'A' && c <= 'F' ? c - 'A' + 10
                  : c >= 'a' && c <= 'f' ? c - 'a' + 10 : -1;
            if (d < 0)
                continue;
            if (iHigh < 0)
                iHigh = d;
            else
            {
                sb.Append((char)(iHigh * 16 + d));
                iHigh = -1;
            }
        }
        return sb.ToString();
    }

    // hb_ATokens( <c>[, <cDelim> | <lEOL>[, <lSkipStrings>[, <lDoubleQuoteOnly>]]] )
    // (rtl/hbtoken.c): c split into an array of strings. With no delimiter
    // it splits on blanks, taking a run of them as one and ignoring those
    // at either end; an explicit delimiter separates at every occurrence
    // (",," gives an empty token); lEOL splits on line ends (CR, LF, CRLF,
    // LFCR). lSkipStrings keeps quoted text (" or ') whole, " only with
    // lDoubleQuoteOnly. An empty string is one empty token.
    public static dynamic[] hb_ATokens(string cString, string cDelim = null,
                                       bool lSkipStrings = false, bool lDoubleQuoteOnly = false) =>
        Tokens(cString, string.IsNullOrEmpty(cDelim) ? null : cDelim, false, lSkipStrings, lDoubleQuoteOnly);

    public static dynamic[] hb_ATokens(string cString, bool lEOL,
                                       bool lSkipStrings = false, bool lDoubleQuoteOnly = false) =>
        Tokens(cString, null, lEOL, lSkipStrings, lDoubleQuoteOnly);

    static dynamic[] Tokens(string s, string cDelim, bool fEOL, bool fQuotes, bool fDoubleOnly)
    {
        bool fIsDelim = cDelim != null;
        if (s.Length > 0 && !fIsDelim && !fEOL)
        {
            cDelim = " ";
            s = s.Trim(' ');
        }
        bool fDQuote = fQuotes, fSQuote = fQuotes && !fDoubleOnly;
        var aTokens = new System.Collections.Generic.List<dynamic>();
        int nLen = s.Length, nStart = 0, nPos = 0;
        char cQuote = '\0';
        for (; nPos < nLen; ++nPos)
        {
            char ch = s[nPos];
            if (cQuote != '\0')
            {
                if (ch == cQuote)
                    cQuote = '\0';
            }
            else if ((ch == '"' && fDQuote) || (ch == '\'' && fSQuote))
                cQuote = ch;
            else if (fEOL && (ch == '\n' || ch == '\r'))
            {
                aTokens.Add(s.Substring(nStart, nPos - nStart));
                if (nPos + 1 < nLen && s[nPos + 1] == (ch == '\n' ? '\r' : '\n'))
                    ++nPos;
                nStart = nPos + 1;
            }
            else if (cDelim != null && string.CompareOrdinal(s, nPos, cDelim, 0, cDelim.Length) == 0)
            {
                aTokens.Add(s.Substring(nStart, nPos - nStart));
                if (!fIsDelim)
                    while (nPos + 1 < nLen && s[nPos + 1] == cDelim[0])
                        ++nPos;
                nPos += cDelim.Length - 1;
                nStart = nPos + 1;
            }
        }
        aTokens.Add(nStart < nLen ? s.Substring(nStart) : "");
        return aTokens.ToArray();
    }

    // hb_StrReplace( <c>, <cSource>|<aSource>|<hReplace>[, <cDest>|<aDest>] )
    // (rtl/strrepl.c), one pass over c. With strings, each character of
    // cSource found is replaced by cDest's character at the same place, or
    // dropped when cDest is shorter; with arrays, the first aSource entry
    // matching at a position is replaced by aDest's (or cDest's character)
    // at its index, or dropped; a hash maps its keys to its values.
    public static string hb_StrReplace(string cString, string cSource, string cDest = null)
    {
        if (cString.Length == 0 || cSource.Length == 0)
            return cString;
        var sb = new System.Text.StringBuilder(cString.Length);
        foreach (char c in cString)
        {
            int i = cSource.IndexOf(c);
            if (i < 0)
                sb.Append(c);
            else if (cDest != null && i < cDest.Length)
                sb.Append(cDest[i]);
        }
        return sb.ToString();
    }

    public static string hb_StrReplace(string cString, dynamic[] aSource, dynamic[] aDest = null)
    {
        var keys = System.Array.ConvertAll(aSource, x => x as string ?? "");
        var values = aDest == null ? System.Array.Empty<string>()
                                   : System.Array.ConvertAll(aDest, x => x as string ?? "");
        return StrReplace(cString, keys, values);
    }

    public static string hb_StrReplace(string cString, dynamic[] aSource, string cDest)
    {
        var keys = System.Array.ConvertAll(aSource, x => x as string ?? "");
        var values = new string[cDest.Length];
        for (int i = 0; i < cDest.Length; i++)
            values[i] = cDest[i].ToString();
        return StrReplace(cString, keys, values);
    }

    public static string hb_StrReplace(string cString, System.Collections.IDictionary hReplace)
    {
        var keys = new System.Collections.Generic.List<string>();
        var values = new System.Collections.Generic.List<string>();
        foreach (System.Collections.DictionaryEntry e in hReplace)
        {
            keys.Add(e.Key as string ?? "");
            values.Add(e.Value as string ?? "");
        }
        return StrReplace(cString, keys.ToArray(), values.ToArray());
    }

    static string StrReplace(string s, string[] keys, string[] values)
    {
        if (s.Length == 0 || keys.Length == 0)
            return s;
        var sb = new System.Text.StringBuilder(s.Length);
        int nPos = 0;
        while (nPos < s.Length)
        {
            int nAt = -1;
            for (int k = 0; k < keys.Length; k++)
                if (keys[k].Length > 0 && string.CompareOrdinal(s, nPos, keys[k], 0, keys[k].Length) == 0
                    && keys[k].Length <= s.Length - nPos)
                {
                    nAt = k;
                    break;
                }
            if (nAt < 0)
            {
                sb.Append(s[nPos++]);
                continue;
            }
            if (nAt < values.Length)
                sb.Append(values[nAt]);
            nPos += keys[nAt].Length;
        }
        return sb.ToString();
    }

    // hb_StrFormat( <cFormat>, ... ) (rtl/hbstrfmt.c): printf-style.
    // %[n$][flags][width][.precision]type with flags - + blank 0 and types
    // s (a string argument; anything else is empty), d (rounded to an
    // integer), x/X (hex), f (the number's own decimals, or the
    // precision), c (a character code); %% is %. A missing argument is
    // empty or 0; an unknown %sequence is copied as it stands.
    public static string hb_StrFormat(string cFormat, params dynamic[] aArgs)
    {
        aArgs ??= System.Array.Empty<dynamic>();
        var sb = new System.Text.StringBuilder(cFormat.Length + 16);
        int iParam = 0, p = 0, n = cFormat.Length;
        char At(int i) => i < n ? cFormat[i] : '\0';
        while (p < n)
        {
            if (cFormat[p] != '%')
            {
                sb.Append(cFormat[p++]);
                continue;
            }
            int pSave = p++;
            if (At(p) == '%')
            {
                sb.Append('%');
                p++;
                continue;
            }
            int iWidth = -1, iDec = -1, iParamNo = 0;
            bool fLeft = false, fForce = false, fPadZero = false, fSpace = false;
            while (char.IsAsciiDigit(At(p)))
                iParamNo = iParamNo * 10 + At(p++) - '0';
            if (iParamNo > 0 && At(p) == '$')
                p++;
            else
            {
                iParamNo = -1;
                p = pSave + 1;
            }
            for (bool fFlag = true; fFlag && p < n; )
            {
                switch (At(p))
                {
                    case '-': fLeft = true; p++; break;
                    case '+': fForce = true; p++; break;
                    case ' ': fSpace = true; p++; break;
                    case '0': fPadZero = true; p++; break;
                    default: fFlag = false; break;
                }
            }
            if (char.IsAsciiDigit(At(p)))
            {
                iWidth = 0;
                while (char.IsAsciiDigit(At(p)))
                    iWidth = iWidth * 10 + At(p++) - '0';
            }
            if (At(p) == '.')
            {
                p++;
                iDec = 0;
                while (char.IsAsciiDigit(At(p)))
                    iDec = iDec * 10 + At(p++) - '0';
            }
            char cType = At(p);
            object xItem = null;
            if (cType is 'c' or 'd' or 'x' or 'X' or 'f' or 's')
            {
                if (iParamNo == -1)
                    iParamNo = ++iParam;
                xItem = iParamNo <= aArgs.Length ? (object)aArgs[iParamNo - 1] : null;
            }
            switch (cType)
            {
                case 'c':
                {
                    char c = (char)(IsNumeric(xItem) ? (int)Math.Truncate(Convert.ToDecimal(xItem, INV)) & 0xFF : 0);
                    if (fLeft)
                        sb.Append(c);
                    if (iWidth > 1)
                        sb.Append(' ', iWidth - 1);
                    if (!fLeft)
                        sb.Append(c);
                    break;
                }
                case 'd':
                case 'x':
                case 'X':
                {
                    string cDigits;
                    bool fSign = false;
                    if (IsNumeric(xItem))
                    {
                        decimal v = Convert.ToDecimal(xItem, INV);
                        cDigits = cType == 'd' ? NumToStr(v, 25, 0).TrimStart(' ')
                                               : hb_NumToHex(v);
                        if (cType == 'x')
                            cDigits = cDigits.ToLowerInvariant();
                        if (cDigits.StartsWith('-'))
                        {
                            fSign = true;
                            cDigits = cDigits.Substring(1);
                        }
                    }
                    else
                        cDigits = xItem is bool l && l ? "1" : "0";
                    int iSize = cDigits.Length, iExtra = fForce || fSpace || fSign ? 1 : 0;
                    string cSign = fSign ? "-" : fForce ? "+" : fSpace ? " " : "";
                    if (iDec >= 0)
                        fPadZero = false;
                    if (fLeft)
                    {
                        sb.Append(cSign).Append('0', Math.Max(iDec - iSize, 0)).Append(cDigits);
                        sb.Append(' ', Math.Max(iWidth - Math.Max(iSize, iDec) - iExtra, 0));
                    }
                    else if (fPadZero)
                        sb.Append(cSign).Append('0', Math.Max(iWidth - iSize - iExtra, 0)).Append(cDigits);
                    else
                        sb.Append(' ', Math.Max(iWidth - Math.Max(iSize, iDec) - iExtra, 0))
                          .Append(cSign).Append('0', Math.Max(iDec - iSize, 0)).Append(cDigits);
                    break;
                }
                case 'f':
                {
                    string cNum = "0";
                    if (IsNumeric(xItem))
                    {
                        decimal v = Convert.ToDecimal(xItem, INV);
                        int iD = iDec >= 0 ? Math.Min(iDec, 253) : StrDefault(v).iDec;
                        cNum = NumToStr(v, 255, iD).TrimStart(' ');
                    }
                    bool fNeg = cNum.StartsWith('-');
                    int iExtra = (fForce || fSpace) && !fNeg ? 1 : 0;
                    string cSign = fNeg ? "" : fForce ? "+" : fSpace ? " " : "";
                    int nPad = Math.Max(iWidth - cNum.Length - iExtra, 0);
                    if (fLeft)
                        sb.Append(cSign).Append(cNum).Append(' ', nPad);
                    else if (fPadZero)
                    {
                        if (fNeg)
                            sb.Append('-').Append('0', nPad).Append(cNum, 1, cNum.Length - 1);
                        else
                            sb.Append(cSign).Append('0', nPad).Append(cNum);
                    }
                    else
                        sb.Append(' ', nPad).Append(cSign).Append(cNum);
                    break;
                }
                case 's':
                {
                    string cStr = xItem as string ?? "";
                    if (iDec >= 0 && iDec < cStr.Length)
                        cStr = cStr.Substring(0, iDec);
                    if (fLeft)
                        sb.Append(cStr);
                    if (iWidth > 1)
                        sb.Append(' ', Math.Max(iWidth - cStr.Length, 0));
                    if (!fLeft)
                        sb.Append(cStr);
                    break;
                }
                default:
                    sb.Append(cFormat, pSave, p - pSave);
                    continue;
            }
            p++;
        }
        return sb.ToString();
    }

    // MemoLine( <c>[, <nLineLength> = 79[, <nLine> = 1[, <nTabSize> = 4[,
    // <lWrap> = .T.[, <cEOL>[, <lPad> = .T.]]]]]] ) and MLCount( <c>[,
    // <nLineLength>[, <nTabSize>[, <lWrap>[, <cEOL>]]]] ) (rtl/mlcfunc.c):
    // c cut into lines of at most nLineLength columns, at the last blank
    // when wrapping; a tab moves to the next tab stop; a soft CR
    // (Chr( 141 ) + Chr( 10 )) is dropped; lines end at cEOL (SET EOL,
    // CRLF, by default — a lone LF is an ordinary character). MemoLine()
    // gives the line padded with blanks to nLineLength.
    sealed class MemoLines
    {
        readonly string s;
        readonly int nLineLength, nTabSize;
        readonly bool fWrap;
        readonly string cEOL;
        public int nOffset, nCol;

        public MemoLines(string s, int nLineLength, int nTabSize, bool fWrap, string cEOL)
        {
            this.s = s;
            this.nLineLength = nLineLength;
            this.fWrap = fWrap;
            this.cEOL = string.IsNullOrEmpty(cEOL) ? "\r\n" : cEOL;
            if (nTabSize >= nLineLength)
                nTabSize = nLineLength - 1;
            this.nTabSize = nTabSize < 1 ? 1 : nTabSize;
        }

        public int Length => s.Length;
        public char this[int i] => s[i];
        public int TabSize => nTabSize;
        public int LineLength => nLineLength;

        public bool IsSoftCR(int i) => i + 1 < s.Length && s[i] == (char)141 && s[i + 1] == '\n';

        // hb_mlGetLine: advance past the next line, leaving its width in nCol
        public bool GetLine()
        {
            int nBlankCol = 0, nBlankPos = 0;
            nCol = 0;
            if (nOffset >= s.Length)
                return false;
            while (nOffset < s.Length)
            {
                if (IsSoftCR(nOffset))
                {
                    nOffset += 2;
                    if (!fWrap)
                        break;
                    if (nBlankPos + 2 == nOffset)
                        nBlankPos += 2;
                    continue;
                }
                if (string.CompareOrdinal(s, nOffset, cEOL, 0, cEOL.Length) == 0
                    && s.Length - nOffset >= cEOL.Length)
                {
                    nOffset += cEOL.Length;
                    break;
                }
                if (!fWrap && nCol >= nLineLength)
                    break;
                int nLastPos = nOffset;
                char ch = s[nOffset++];
                nCol += ch == '\t' ? nTabSize - nCol % nTabSize : 1;
                if (nCol > nLineLength)
                {
                    if (fWrap && ch != ' ' && ch != '\t')
                    {
                        if (nBlankCol != 0)
                        {
                            nCol = nBlankCol;
                            nOffset = nBlankPos;
                        }
                        else
                            nOffset = nLastPos;
                    }
                    else if (!fWrap)
                        nOffset = nLastPos;
                    break;
                }
                if (nCol > 1 && (ch == ' ' || ch == '\t'))
                {
                    nBlankCol = nCol;
                    nBlankPos = nOffset;
                }
            }
            if (nCol > nLineLength)
                nCol = nLineLength;
            return true;
        }
    }

    public static decimal MLCount(string cString, decimal nLineLength = 79, decimal nTabSize = 4,
                                  bool lWrap = true, string cEOL = null)
    {
        int nSize = (int)Math.Truncate(nLineLength);
        if (cString is null || nSize <= 0)
            return 0;
        var ml = new MemoLines(cString, nSize, (int)nTabSize, lWrap, cEOL);
        int nLines = 0;
        while (ml.GetLine())
            nLines++;
        return nLines;
    }

    public static string MemoLine(string cString, decimal nLineLength = 79, decimal nLineNumber = 1,
                                  decimal nTabSize = 4, bool lWrap = true, string cEOL = null,
                                  bool lPad = true)
    {
        long nLine = (long)Math.Truncate(nLineNumber);
        int nSize = (int)Math.Truncate(nLineLength);
        if (nLine < 1 || cString is null || nSize <= 0)
            return "";
        var ml = new MemoLines(cString, nSize, (int)nTabSize, lWrap, cEOL);
        while (--nLine > 0)
            if (!ml.GetLine())
                return "";
        int nIndex = ml.nOffset;
        ml.GetLine();
        var sb = new System.Text.StringBuilder(nSize);
        int nCol = 0;
        while (nIndex < ml.Length && nCol < ml.nCol)
        {
            if (ml.IsSoftCR(nIndex))
            {
                nIndex += 2;
                continue;
            }
            char ch = ml[nIndex++];
            if (ch == '\t')
            {
                int n = ml.TabSize - sb.Length % ml.TabSize;
                do
                    sb.Append(' ');
                while (++nCol < ml.nCol && --n > 0);
            }
            else
            {
                sb.Append(ch);
                ++nCol;
            }
        }
        if (nCol < nSize)
        {
            int nPad = Math.Min(nSize - nCol, nSize - sb.Length);
            if (!lPad && nPad > 0)
                nPad = nIndex < ml.Length && (ml[nIndex] == ' ' || ml[nIndex] == '\t') ? 1 : 0;
            if (nPad > 0)
                sb.Append(' ', nPad);
        }
        return sb.ToString();
    }

    // hb_CStr( <x> ): any value as text (rtl/valtoexp.prg)
    public static string hb_CStr(dynamic xValue)
    {
        object x = xValue;
        return x switch
        {
            null => "NIL",
            string s => s,
            DateOnly d => d == default ? "0d00000000" : "0d" + DToS(d),
            DateTime t => "t\"" + t.ToString("yyyy-MM-dd HH:mm:ss.fff", INV) + "\"",
            bool l => l ? ".T." : ".F.",
            Delegate => "{|| ... }",
            System.Array a => "{ Array of " + a.Length + " Items }",
            System.Collections.IDictionary h => "{ Hash of " + h.Count + " Items }",
            _ when IsNumeric(x) => Str(Convert.ToDecimal(x, INV)),
            _ => "{ " + CLASSNAME(x) + " Object }",
        };
    }

    // hb_ValToExp( <x> ): x as a Harbour expression that reads back as
    // it (rtl/valtoexp.prg): strings quoted as hb_StrToExp() quotes them,
    // arrays and hashes written out in full. An object needs the object
    // runtime's __objGetIVars() and a self-referencing array its
    // __itemSetRef(), both part of the deferred object-reflection work,
    // so either throws.
    public static string hb_ValToExp(dynamic xValue, bool lRaw = false) =>
        ValToExp(xValue, new System.Collections.Generic.HashSet<object>(ReferenceEqualityComparer.Instance));

    static string ValToExp(object x, System.Collections.Generic.HashSet<object> seen)
    {
        switch (x)
        {
            case null: return "NIL";
            case string s: return StrToExp(s);
            case DateOnly d: return d == default ? "0d00000000" : "0d" + DToS(d);
            case DateTime t: return "t\"" + t.ToString("yyyy-MM-dd HH:mm:ss.fff", INV) + "\"";
            case bool l: return l ? ".T." : ".F.";
            case Delegate: return "{|| ... }";
        }
        if (IsNumeric(x))
            return hb_ntos(Convert.ToDecimal(x, INV));
        if (x is not (System.Array or System.Collections.IDictionary))
            throw new NotSupportedException("hb_ValToExp() of an object needs __objGetIVars(), part of the deferred object reflection");
        if (!seen.Add(x))
            throw new NotSupportedException("hb_ValToExp() of a self-referencing array needs __itemSetRef()");
        var sb = new System.Text.StringBuilder("{");
        if (x is System.Collections.IDictionary h)
        {
            if (h.Count == 0)
                sb.Append("=>");
            bool fFirst = true;
            foreach (System.Collections.DictionaryEntry e in h)
            {
                sb.Append(fFirst ? "" : ", ").Append(ValToExp(e.Key, seen)).Append("=>").Append(ValToExp(e.Value, seen));
                fFirst = false;
            }
        }
        else
        {
            var a = (System.Array)x;
            for (int i = 0; i < a.Length; i++)
                sb.Append(i == 0 ? "" : ", ").Append(ValToExp(a.GetValue(i), seen));
        }
        seen.Remove(x);
        return sb.Append('}').ToString();
    }

    // hb_StrToExp (rtl/strtoexp.c): the string as a literal — in "..."
    // unless it holds a ", then '...', then [...]; e"..." with \ escapes
    // when it holds all three, or a CR, LF or NUL
    static string StrToExp(string s)
    {
        int iType = 0;
        foreach (char c in s)
            iType |= c switch { '"' => 1, '\'' => 2, ']' => 4, '\r' or '\n' or '\0' => 7, _ => 0 };
        if (iType == 7)
        {
            var sb = new System.Text.StringBuilder("e\"");
            foreach (char c in s)
                sb.Append(c switch
                {
                    '\r' => "\\r",
                    '\n' => "\\n",
                    '\0' => "\\000",
                    '\\' => "\\\\",
                    '"' => "\\\"",
                    _ => c.ToString(),
                });
            return sb.Append('"').ToString();
        }
        return (iType & 1) == 0 ? "\"" + s + "\""
             : (iType & 2) == 0 ? "'" + s + "'"
             : "[" + s + "]";
    }

    // hb_NumToHex( <n>[, <nLen>] ): the integer in upper-case hex, as few
    // digits as it needs, or exactly nLen (1-32) — zero-padded or cut to
    // its low digits. A negative number is its two's complement.
    public static string hb_NumToHex(decimal n, decimal? nLen = null)
    {
        ulong u = unchecked((ulong)(long)Math.Truncate(n));
        int iLen = nLen is null ? 0 : Math.Clamp((int)nLen.Value, 1, 32);
        var sb = new System.Text.StringBuilder();
        do
        {
            int d = (int)(u & 0x0F);
            sb.Insert(0, (char)(d + (d < 10 ? '0' : 'A' - 10)));
            u >>= 4;
        }
        while (iLen == 0 ? u != 0 : sb.Length < iLen);
        return sb.ToString();
    }

    // ---- Transform() — rtl/transfrm.c ----
    // A picture is optional functions ("@" and letters up to a blank)
    // then a template. Strings, numbers, dates, timestamps and logicals
    // each read the template their own way; the functions then blank
    // (@Z), left-justify (@B) or cut (@S<n>) the result. Upper-casing is
    // Upper()'s.

    [Flags]
    enum PicFlag
    {
        Left = 0x0001,      // @B
        Credit = 0x0002,    // @C
        Debit = 0x0004,     // @X
        PadL = 0x0008,      // @L, @0 (FoxPro/Xbase++)
        ParNeg = 0x0010,    // @(
        Remain = 0x0020,    // @R
        Upper = 0x0040,     // @!
        Date = 0x0080,      // @D
        British = 0x0100,   // @E — for numbers also: exchange . and ,
        Empty = 0x0200,     // @Z
        Width = 0x0400,     // @S<n>
        ParNegWos = 0x0800, // @) — @( without the leading blanks
        Time = 0x1000,      // @T
    }

    public static string Transform(dynamic xValue, string cPicture = null)
    {
        object value = xValue;
        if (value is null)
            throw new ArgumentException("Argument error (TRANSFORM)");
        if (string.IsNullOrEmpty(cPicture))
            return TransformPlain(value);

        // --- the functions ---
        PicFlag flags = 0;
        int nParamS = 0;
        string cPic = cPicture;
        if (cPic[0] == '@')
        {
            int i = 1;
            for (; i < cPic.Length; i++)
            {
                char c = cPic[i];
                if (c == ' ' || c == '\t')
                {
                    i++;
                    break;
                }
                switch (char.ToUpperInvariant(c))
                {
                    case '!': flags |= PicFlag.Upper; break;
                    case '(': flags |= PicFlag.ParNeg; break;
                    case ')': flags |= PicFlag.ParNegWos; break;
                    case 'L':
                    case '0': flags |= PicFlag.PadL; break;
                    case 'B': flags |= PicFlag.Left; break;
                    case 'C': flags |= PicFlag.Credit; break;
                    case 'D': flags |= PicFlag.Date; break;
                    case 'E': flags |= PicFlag.British; break;
                    case 'R': flags |= PicFlag.Remain; break;
                    case 'S':
                        flags |= PicFlag.Width;
                        nParamS = 0;
                        while (i + 1 < cPic.Length && cPic[i + 1] >= '0' && cPic[i + 1] <= '9')
                            nParamS = nParamS * 10 + (cPic[++i] - '0');
                        break;
                    case 'T': flags |= PicFlag.Time; break;
                    case 'X': flags |= PicFlag.Debit; break;
                    case 'Z': flags |= PicFlag.Empty; break;
                }
            }
            cPic = i < cPic.Length ? cPic.Substring(i) : "";
        }

        char[] result;
        int nOffset = 0;
        switch (value)
        {
            case string s:
                result = TransformString(s, cPic, flags);
                break;
            case bool l:
                result = TransformLogical(l, cPic, flags);
                break;
            case DateOnly d:
                result = TransformDate(d, flags);
                break;
            case DateTime t:
                result = TransformTimeStamp(t, flags);
                break;
            case decimal or int or long or double or float or short or byte or uint or ulong:
                result = TransformNumber(Convert.ToDecimal(value, INV), cPic, ref flags, out nOffset);
                break;
            default:
                throw new ArgumentException("Argument error (TRANSFORM)");
        }

        // --- @Z, @B, @S ---
        int nLen = result.Length;
        if ((flags & PicFlag.Empty) != 0)
            System.Array.Fill(result, ' ');
        else if ((flags & PicFlag.Left) != 0)
        {
            int iFirst = nOffset;
            while (iFirst < nLen && result[iFirst] == ' ')
                iFirst++;
            if (iFirst > nOffset && iFirst < nLen)
            {
                System.Array.Copy(result, iFirst, result, nOffset, nLen - iFirst);
                for (int i = nOffset + nLen - iFirst; i < nLen; i++)
                    result[i] = ' ';
            }
        }
        if (nParamS > 0 && nLen > nParamS)
            nLen = nParamS;
        return new string(result, 0, nLen);
    }

    // Transform( x, "" ) or Transform( x ): the value as Harbour writes
    // it with no picture
    static string TransformPlain(object value)
    {
        switch (value)
        {
            case string s:
                return s;
            case DateOnly d:
                return DToC(d);
            case DateTime t:
                return TimeStampText(t, SetDateFormat());
            case bool l:
                return l ? "T" : "F";
            case decimal or int or long or double or float or short or byte or uint or ulong:
                decimal n = Convert.ToDecimal(value, INV);
                if (!SetFixed())
                    return Str(n);
                if (IsHbInteger(n))
                {
                    (int iWidth, _) = StrDefault(n);
                    iWidth += 2 + (SetDecimals() << 1);
                    return NumToStr(n, iWidth, 0);
                }
                // hb_itemString: with SET FIXED a double shows SET DECIMALS
                return Str(n, null, SetDecimals());
            default:
                throw new ArgumentException("Argument error (TRANSFORM)");
        }
    }

    static bool SetFixed() => s_sets.TryGetValue((int)_SET_FIXED, out var f) && f is bool l && l;
    // SET DECIMALS; `SET DECIMALS TO 3` arrives as an int
    static int SetDecimals() =>
        s_sets.TryGetValue((int)_SET_DECIMALS, out var d) && d is decimal or int or long
            ? Convert.ToInt32(d, INV) : 2;
    static bool SetCentury() => s_sets.TryGetValue((int)_SET_CENTURY, out var c) && c is bool l && l;

    static char[] TransformString(string cExp, string cPic, PicFlag flags)
    {
        bool fUpper = (flags & PicFlag.Upper) != 0;
        bool fRemain = (flags & PicFlag.Remain) != 0;
        if ((flags & (PicFlag.Date | PicFlag.British)) != 0)
            cPic = DateFormat("XXXXXXXX", SetDateFormat());
        var sb = new System.Text.StringBuilder();
        if (cPic.Length > 0)
        {
            int iExp = 0, iPic = 0;
            for (; iPic < cPic.Length; iPic++)
            {
                char cP = cPic[iPic];
                int iPrev = iExp;
                if (iExp < cExp.Length)
                {
                    char cE = cExp[iExp++];
                    switch (cP)
                    {
                        case '!':
                            sb.Append(UpperChar(cE));
                            break;
                        case '#': case '9': case 'a': case 'A': case 'l': case 'L':
                        case 'n': case 'N': case 'x': case 'X':
                            sb.Append(fUpper ? UpperChar(cE) : cE);
                            break;
                        case 'y': case 'Y':
                            sb.Append(cE is 't' or 'T' or 'y' or 'Y' ? 'Y' : 'N');
                            break;
                        default:
                            sb.Append(cP);
                            if (fRemain)
                                iExp = iPrev;
                            break;
                    }
                }
                else if (!fRemain)
                    break;
                else
                    sb.Append("!#9aAlLnNxXyY".IndexOf(cP) >= 0 ? ' ' : cP);
            }
            if (fRemain && iExp == 0 && iExp < cExp.Length)
                sb.Append(fUpper ? Upper(cExp) : cExp);
        }
        else
        {
            sb.Append(fUpper ? Upper(cExp) : cExp);
            if ((flags & PicFlag.British) != 0)
            {
                bool fFound = false;
                for (int i = 0; i < sb.Length; i++)
                {
                    if (sb[i] == ',')
                        sb[i] = '.';
                    else if (!fFound && sb[i] == '.')
                    {
                        sb[i] = ',';
                        fFound = true;
                    }
                }
            }
        }
        char[] result = sb.ToString().ToCharArray();
        if ((flags & PicFlag.British) != 0 && result.Length >= 5)
        {
            (result[0], result[1], result[3], result[4]) = (result[3], result[4], result[0], result[1]);
        }
        return result;
    }

    static char[] TransformNumber(decimal n, string cPic, ref PicFlag flags, out int nOffset)
    {
        nOffset = 0;
        bool fExchange = (flags & PicFlag.British) != 0;
        if ((flags & PicFlag.Date) != 0)
            cPic = DateFormat("99999999", SetDateFormat());

        // the template's digit places and decimals
        int iWidth = 0, iDec = 0;
        for (int i = 0; i < cPic.Length; i++)
        {
            if (cPic[i] == '.')
            {
                while (++i < cPic.Length)
                    if (cPic[i] is '9' or '#' or '$' or '*')
                    {
                        iWidth++;
                        iDec++;
                    }
                if (iDec > 0)
                    iWidth++;
                break;
            }
            if (cPic[i] is '9' or '#' or '$' or '*')
                iWidth++;
        }
        int iCount = 0;
        if (iWidth == 0)
        {
            (iWidth, iDec) = StrDefault(n);
            if (SetFixed())
            {
                if (IsHbInteger(n))
                    iWidth += 2 + (SetDecimals() << 1);
                else
                    iDec = SetDecimals();
            }
            if (iDec > 0)
                iWidth += iDec + 1;
        }
        else if (iDec > 0 && iWidth - iDec == 1)
        {
            // ".99": no integer place — format one and drop it
            iCount = 1;
            iWidth++;
        }

        decimal nValue = n;
        if ((flags & (PicFlag.Debit | PicFlag.ParNeg | PicFlag.ParNegWos)) != 0 && n < 0)
            nValue = -n;
        if (n != 0)
            flags &= ~PicFlag.Empty;

        string cStr = NumToStr(nValue, iWidth, iDec);
        if (iCount > 0)
        {
            iWidth--;
            cStr = cStr[0] != '0' ? "." + new string('*', iWidth - 1) : cStr.Substring(1, iWidth);
        }

        char[] a = cStr.ToCharArray();
        if ((flags & PicFlag.PadL) != 0)
        {
            int i = 0;
            for (; i < a.Length && a[i] == ' '; i++)
                a[i] = '0';
            if (i > 0 && i < a.Length && a[i] == '-')
            {
                a[0] = '-';
                a[i] = '0';
            }
        }

        var r = new System.Collections.Generic.List<char>(Math.Max(cPic.Length, a.Length) + 4);
        if (cPic.Length == 0)
        {
            if (fExchange)
                for (int i = 0; i < iWidth && i < a.Length; i++)
                    if (a[i] == '.')
                    {
                        a[i] = ',';
                        break;
                    }
            r.AddRange(a);
        }
        else
        {
            iCount = 0;
            for (int i = 0; i < cPic.Length; i++)
            {
                char cP = cPic[i];
                if (cP is '9' or '#')
                    r.Add(iCount < iWidth ? a[iCount++] : ' ');
                else if (cP is '$' or '*')
                {
                    if (iCount < iWidth)
                    {
                        r.Add(a[iCount] == ' ' ? cP : a[iCount]);
                        iCount++;
                    }
                    else
                        r.Add(' ');
                }
                else if (cP == '.' && iCount < iWidth)
                {
                    r.Add(fExchange ? ',' : '.');
                    iCount++;
                }
                else if (cP == ',' && i > 0 && iCount < iWidth)
                {
                    if (r[i - 1] >= '0' && r[i - 1] <= '9')
                        r.Add(fExchange ? '.' : ',');
                    else
                    {
                        r.Add(r[i - 1]);
                        if (r[i - 1] == '-')
                            r[i - 1] = i > 1 && r[i - 2] != '$' ? r[i - 2] : ' ';
                    }
                }
                else
                    r.Add(cP);
            }
        }

        int nLen = r.Count;
        bool fPic = cPic.Length > 0;
        bool Replaceable(int k) =>
            r[k] >= '0' && r[k] <= '9' && (!fPic || cPic[k] == '9' || cPic[k] != r[k]);
        if (n < 0)
        {
            if ((flags & PicFlag.ParNegWos) != 0)
            {
                iCount = 0;
                if (fPic && nLen > 1 && cPic[0] == r[0] && cPic[0] is '*' or '$' && r[1] == ' ')
                    ++iCount;
                while (iCount + 1 < nLen && r[iCount + 1] == ' ')
                    ++iCount;
                if (r[iCount] >= '1' && r[iCount] <= '9' &&
                    (!fPic || cPic[iCount] == '9' || cPic[iCount] != r[iCount]))
                {
                    r[iCount] = '(';
                    for (++iCount; iCount < nLen; iCount++)
                        if (Replaceable(iCount))
                            r[iCount] = '*';
                }
                else
                    r[iCount] = '(';
                r.Add(')');
            }
            else if ((flags & PicFlag.ParNeg) != 0)
            {
                if (r[0] >= '1' && r[0] <= '9' && (!fPic || cPic[0] == '9' || cPic[0] != r[0]))
                    for (int k = 1; k < nLen; k++)
                        if (Replaceable(k))
                            r[k] = '*';
                r[0] = '(';
                r.Add(')');
                nOffset = 1;
            }
            if ((flags & PicFlag.Debit) != 0)
                r.AddRange(" DB");
        }
        else if ((flags & PicFlag.Credit) != 0 && n > 0)
            r.AddRange(" CR");
        return r.ToArray();
    }

    // The date format @E substitutes: SET DATEFORMAT's separators with the
    // day first (DDMMYYYY, or DDMMYY without century)
    static string BritishFormat(string cFormat)
    {
        string cBritish = SetCentury() ? "DDMMYYYY" : "DDMMYY";
        var sb = new System.Text.StringBuilder();
        int iB = 0, iF = 0;
        char cLast = 'x';
        for (int n = 0; n < 10; n++)
        {
            if (iB < cBritish.Length && cBritish[iB] == cLast)
            {
                sb.Append(cLast);
                iB++;
            }
            else if (iF >= cFormat.Length)
                break;
            else if (iB < cBritish.Length && "YyDdMm".IndexOf(cFormat[iF]) >= 0)
            {
                cLast = cBritish[iB++];
                sb.Append(cLast);
                do
                    iF++;
                while (iF < cFormat.Length && cFormat[iF - 1] == cFormat[iF]);
            }
            else
                sb.Append(cFormat[iF++]);
        }
        return sb.ToString();
    }

    static char[] TransformDate(DateOnly d, PicFlag flags)
    {
        string cFormat = SetDateFormat();
        if ((flags & PicFlag.British) != 0)
            cFormat = BritishFormat(cFormat);
        string cResult = DateFormat(DateString(d), cFormat);
        if ((flags & PicFlag.Remain) != 0)
        {
            // @R puts the format's separators in again (a 12-byte buffer)
            char[] buf = new char[13];
            cResult.CopyTo(0, buf, 0, cResult.Length);
            int nLen = cResult.Length;
            string cPicDate = DateFormat("99999999", cFormat);
            for (int i = 0; i < cPicDate.Length; i++)
                if (cPicDate[i] != '9')
                {
                    System.Array.Copy(buf, i, buf, i + 1, 12 - i);
                    buf[i] = cPicDate[i];
                    nLen++;
                }
            buf[12] = '\0';
            return buf.AsSpan(0, Math.Min(nLen, 13)).ToArray();
        }
        return cResult.ToCharArray();
    }

    static char[] TransformTimeStamp(DateTime t, PicFlag flags)
    {
        string cDateFormat = (flags & (PicFlag.Date | PicFlag.Time)) != PicFlag.Time ? SetDateFormat() : null;
        bool fTime = (flags & (PicFlag.Date | PicFlag.Time)) != PicFlag.Date;
        if (cDateFormat != null && (flags & PicFlag.British) != 0)
            cDateFormat = BritishFormat(cDateFormat);
        string cResult = fTime ? TimeStampText(t, cDateFormat)
                               : DateFormat(DateString(DateOnly.FromDateTime(t)), cDateFormat);
        return cResult.ToCharArray();
    }

    // A timestamp as SET DATEFORMAT and Harbour's default time format,
    // "hh:mm:ss.fff" (no date part when cDateFormat is null). SET
    // TIMEFORMAT itself is not kept yet.
    static string TimeStampText(DateTime t, string cDateFormat)
    {
        string cTime = t.ToString("HH:mm:ss.fff", INV);
        return cDateFormat is null ? cTime
            : DateFormat(DateString(DateOnly.FromDateTime(t)), cDateFormat) + " " + cTime;
    }

    static char[] TransformLogical(bool l, string cPic, PicFlag flags)
    {
        if ((flags & (PicFlag.Date | PicFlag.British)) != 0)
            cPic = DateFormat("99999999", SetDateFormat());
        var sb = new System.Text.StringBuilder();
        bool fDone = false, fExit = false;
        for (int i = 0; (i < cPic.Length || !fDone) && !fExit; i++)
        {
            char cP;
            if (i < cPic.Length)
                cP = cPic[i];
            else
            {
                cP = 'L';
                fExit = true;
            }
            switch (cP)
            {
                case 'y': case 'Y':
                    sb.Append(fDone ? ' ' : l ? 'Y' : 'N');
                    fDone = true;
                    break;
                case '#': case 'l': case 'L':
                    sb.Append(fDone ? ' ' : l ? 'T' : 'F');
                    fDone = true;
                    break;
                default:
                    sb.Append(cP);
                    break;
            }
            if ((flags & PicFlag.Remain) == 0)
                fExit = true;
        }
        return sb.ToString().ToCharArray();
    }
    // StrZero( <n>[, <nWidth>[, <nDec>]] ) is Str() with the leading
    // blanks turned to zeros and a minus sign moved to the front
    // (rtl/strzero.c): StrZero( -5, 4 ) is "-005". A number too wide stays
    // asterisks.
    public static string StrZero(decimal n, decimal? nWidth = null, decimal? nDec = null)
    {
        char[] a = Str(n, nWidth, nDec).ToCharArray();
        int iMinus = System.Array.IndexOf(a, '-');
        if (iMinus >= 0)
            a[iMinus] = ' ';
        for (int i = 0; i < a.Length && a[i] == ' '; i++)
            a[i] = '0';
        if (iMinus >= 0)
            a[0] = '-';
        return new string(a);
    }

    // ---- Logical functions ----

    public static bool Empty(dynamic val)
    {
        if (val == null) return true;
        if (IsNumeric(val)) return Convert.ToDecimal(val, INV) == 0;
        if (val is string s) return s.Trim().Length == 0;
        // Harbour's empty date (CToD("")) is DateOnly's default here.
        if (val is DateOnly dt) return dt == default;
        if (val is DateTime ts) return ts == default;
        if (val is bool b) return !b;
        if (val is System.Array a) return a.Length == 0;
        if (val is System.Collections.IDictionary h) return h.Count == 0;
        return false;
    }

    public static bool IsDigit(string s) => s.Length > 0 && char.IsDigit(s[0]);
    public static bool IsAlpha(string s) => s.Length > 0 && char.IsLetter(s[0]);
    public static bool IsUpper(string s) => s.Length > 0 && char.IsUpper(s[0]);
    public static bool IsLower(string s) => s.Length > 0 && char.IsLower(s[0]);

    // String ordering: the emitter turns `<`, `<=`, `>`, `>=` on strings
    // into `StrCmp(a, b) <op> 0` (`==` stays C# `==`; the transpiler
    // refuses `=`, E0100). Exact and ordinal, by decision (Alex,
    // 2026-09-19): Harbour's SET EXACT semantics — an equal prefix
    // compares equal when EXACT is OFF, trailing blanks are ignored when
    // it is ON — are not reproduced; EasiPOS writes `==` and runs with
    // SET EXACT ON. "Lisbon" > "Lis".
    public static int StrCmp(string a, string b) =>
        Math.Sign(string.CompareOrdinal(a ?? "", b ?? ""));

    // FOR ... STEP <n> whose step is not a constant: the emitter's loop
    // condition, which counts down when the step is below zero and up
    // otherwise — vm/hvm.c hb_vmForTest, evaluated on every pass as the
    // step can change. The end comes before the step, Harbour's order.
    public static bool ForTest(decimal nCounter, decimal nEnd, decimal nStep) =>
        nStep < 0 ? nCounter >= nEnd : nCounter <= nEnd;

    // ---- Date functions ----
    // Harbour Date → C# DateOnly (no time component). Harbour TIMESTAMP
    // maps to C# DateTime via the transpiler's type map.

    public static DateOnly Date() => DateOnly.FromDateTime(DateTime.Today);
    public static DateOnly CToD(string s) { DateTime.TryParse(s, out DateTime d); return DateOnly.FromDateTime(d); }

    // hb_SToD / SToD: one function under two names (rtl/dateshb.c,
    // datesx.c). A string of at least 7 characters is read as YYYYMMDD
    // digit by digit WITHOUT checking that they are digits
    // (hb_dateStrGet: each is char - '0'), and the result stands only if
    // it is a real date (hb_dateEncode: month 1-12, day within the month,
    // leap years). Anything else — shorter, not a date, NIL, no argument
    // — is the empty date. A 7-character string reads C's terminating NUL
    // as its eighth digit, so it is never a date. Harbour allows year 0;
    // DateOnly starts at year 1, so "0000mmdd" is the empty date here.
    public static DateOnly hb_SToD(string cDate = null)
    {
        if (cDate is null || cDate.Length < 7)
            return default;
        int Digit(int i) => (i < cDate.Length ? cDate[i] : '\0') - '0';
        int nYear = ((Digit(0) * 10 + Digit(1)) * 10 + Digit(2)) * 10 + Digit(3);
        int nMonth = Digit(4) * 10 + Digit(5);
        int nDay = Digit(6) * 10 + Digit(7);
        if (nYear < 1 || nYear > 9999 || nMonth < 1 || nMonth > 12 ||
            nDay < 1 || nDay > DateTime.DaysInMonth(nYear, nMonth))
            return default;
        return new DateOnly(nYear, nMonth, nDay);
    }

    public static DateOnly SToD(string cDate = null) => hb_SToD(cDate);

    // DToC(): the date laid out by SET DATEFORMAT (rtl/dates.c,
    // hb_dateFormat); the empty date is the format with its letters
    // blanked, "  /  /  ".
    public static string DToC(DateOnly d) => DateFormat(DateString(d), SetDateFormat());

    // DToS(): YYYYMMDD, or eight blanks for the empty date
    public static string DToS(DateOnly d) => DateString(d);

    // hb_itemGetDS: the date as YYYYMMDD, eight blanks when empty
    static string DateString(DateOnly d) =>
        d == default ? "        " : d.ToString("yyyyMMdd", INV);

    static string SetDateFormat() =>
        s_sets.TryGetValue((int)_SET_DATEFORMAT, out var f) && f is string cFormat ? cFormat : "mm/dd/yy";

    // hb_dateFormat: cDate (8 characters, YYYYMMDD — or a template such as
    // "99999999" or "XXXXXXXX", which Transform() lays out the same way)
    // written into cFormat's d, m and y runs, 10 characters at most. A
    // run longer than the date's digits is filled with its own letter;
    // other characters are copied (upper-cased). Anything but 8
    // characters gives the format with its letters blanked.
    static string DateFormat(string cDate, string cFormat)
    {
        int nSize = Math.Min(cFormat.Length, 10);
        var sb = new System.Text.StringBuilder(nSize);
        if (cDate is null || cDate.Length != 8)
        {
            foreach (char c in cFormat.Substring(0, nSize))
                sb.Append("DdMmYy".IndexOf(c) >= 0 ? ' ' : c);
            return sb.ToString();
        }
        bool fUsedD = false, fUsedM = false, fUsedY = false;
        int iPos = 0;
        while (sb.Length < nSize)
        {
            char cRun = char.ToUpperInvariant(cFormat[iPos++]);
            int nRun = 1;
            while (iPos < cFormat.Length && char.ToUpperInvariant(cFormat[iPos]) == cRun && sb.Length < nSize)
            {
                iPos++;
                if (sb.Length + nRun < nSize)
                    nRun++;
            }
            // the date's digits for this run, most significant first
            string cDigits = cRun == 'D' ? (fUsedD ? "" : cDate.Substring(6, 2))
                           : cRun == 'M' ? (fUsedM ? "" : cDate.Substring(4, 2))
                           : cRun == 'Y' ? (fUsedY ? "" : cDate.Substring(0, 4))
                           : "";
            // hb_dateFormat's switch falls through: a run of 1-4 letters
            // takes that many digits (yy the last two; dd/mm padded with
            // their tens digit, ddd = d6 d6 d7); a longer run takes only
            // the last digit and fills the rest with its letter
            if (cDigits.Length > 0 && nRun > 4)
                cDigits = cDigits.Substring(cDigits.Length - 1);
            else if (cRun == 'Y' && cDigits.Length == 4 && nRun < 4)
                cDigits = cDigits.Substring(4 - nRun);
            else if (cRun is 'D' or 'M' && cDigits.Length == 2 && nRun == 1)
                cDigits = cDigits.Substring(1);
            else if (cRun is 'D' or 'M' && cDigits.Length == 2 && nRun > 2)
                cDigits = new string(cDigits[0], nRun - 2) + cDigits;
            for (int i = 0; i < nRun && sb.Length < nSize; i++)
                sb.Append(i < cDigits.Length ? cDigits[i] : cRun);
            if (cRun == 'D') fUsedD = true;
            else if (cRun == 'M') fUsedM = true;
            else if (cRun == 'Y') fUsedY = true;
        }
        return sb.ToString();
    }
    public static string Time() => DateTime.Now.ToString("HH:mm:ss", INV);

    // ---- Terminal ----

    public static void SetColor(string cColor) { }

    // ---- Array functions ----
    // Harbour arrays map to C# dynamic[]. Size-changing mutators (ASize,
    // AAdd) take `ref dynamic[]` so the reallocated array propagates back;
    // the transpiler inserts `ref` at the call site. Size-stable mutators
    // (AIns, ADel, AFill, ASort) shift contents in place.

    // Array( <nElements> [, <nElements>...] ): an array of NIL with one
    // more dimension for each argument — Array( 2, 3 ) is two arrays of
    // three (vm/arrayshb.c, hb_arrayNewRagged). No argument gives NIL; a
    // negative dimension is Harbour's bound error. (Harbour gives NIL for
    // a non-numeric argument too; the signature makes that a compile error.)
    public static dynamic[] Array(params decimal[] anElements)
    {
        if (anElements.Length == 0)
            return null;
        foreach (decimal n in anElements)
            if (Math.Truncate(n) < 0)
                throw new ArgumentOutOfRangeException(nameof(anElements), n,
                    "Bound error: array dimension (ARRAY)");
        return ArrayRagged(anElements, 0);
    }

    static dynamic[] ArrayRagged(decimal[] anElements, int iDimension)
    {
        var a = new dynamic[(int)anElements[iDimension]];
        if (iDimension + 1 < anElements.Length)
            for (int i = 0; i < a.Length; i++)
                a[i] = ArrayRagged(anElements, iDimension + 1);
        return a;
    }

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

    // ASize() (vm/arrayshb.c): a size below 0 is 0; not an array, NIL
    public static dynamic[] ASize(ref dynamic[] arr, decimal nLen)
    {
        int n = SizeArg(nLen);
        if (arr == null)
            arr = new dynamic[n];
        else
            System.Array.Resize(ref arr, n);
        return arr;
    }

    public static dynamic[] ASize(ref dynamic arr, decimal nLen)
    {
        if (arr is not object[] objArr)
            return null;
        var tmp = new dynamic[SizeArg(nLen)];
        System.Array.Copy(objArr, tmp, Math.Min(objArr.Length, tmp.Length));
        arr = tmp;
        return tmp;
    }

    // Non-ref fallback. Returns a new resized array but can't update
    // the caller's variable — caller code that does `aArr := ASize(...)`
    // still works; `ASize(GETMEMBER(...), n)` as a statement is a
    // silent no-op for the caller, matching the pre-ref behavior.
    public static dynamic[] ASize(dynamic arr, decimal nLen)
    {
        if (arr is not object[] objArr)
            return null;
        var copy = new dynamic[SizeArg(nLen)];
        System.Array.Copy(objArr, copy, Math.Min(objArr.Length, copy.Length));
        return copy;
    }

    static int SizeArg(decimal n) => n <= 0 ? 0 : (int)Math.Min(Math.Truncate(n), int.MaxValue);

    // A start/count argument as hb_arrayScan()/Fill()/Eval() read it: the
    // C holds it unsigned, so a negative one is huge
    static ulong SizeParam(decimal n) => unchecked((ulong)(long)Math.Truncate(n));

    // (first index, count) for a start/count pair over nLen elements, as
    // hb_arrayScan/Fill/Eval work them out: a start of 0 is 1, a count
    // past the end stops at the end
    static (long, long) StartCount(long nLen, decimal? nStart, decimal? nCount)
    {
        ulong uStart = nStart is null ? 0 : SizeParam(nStart.Value);
        ulong uFirst = uStart != 0 ? uStart - 1 : 0;
        if (uFirst >= (ulong)nLen)
            return (0, 0);
        ulong uCount = (ulong)nLen - uFirst;
        if (nCount is not null && SizeParam(nCount.Value) < uCount)
            uCount = SizeParam(nCount.Value);
        return ((long)uFirst, (long)uCount);
    }

    // AClone(): a copy with nested arrays copied too; hashes and objects
    // are shared. Not an array, NIL.
    public static dynamic AClone(dynamic arr)
    {
        if (arr is not object[] objArr)
            return null;
        var copy = new dynamic[objArr.Length];
        for (int i = 0; i < objArr.Length; i++)
            copy[i] = objArr[i] is object[] ? AClone(objArr[i]) : objArr[i];
        return copy;
    }

    // ATail(): the last element, NIL for an empty array or none
    public static dynamic ATail(dynamic arr) =>
        arr is object[] a && a.Length > 0 ? a[a.Length - 1] : null;

    // AScan( <a>, <xValue>|<bBlock>[, <nStart>[, <nCount>]] ) and
    // hb_AScan( ..., <lExact> ) (vm/arrays.c, hb_arrayScan). A block is
    // evaluated with each element and its index, and matches when it
    // returns .T.; a value matches an element of its own type — strings
    // exactly (by decision: no SET EXACT prefix rules), numbers and
    // logicals by value, dates by day (and time too with lExact), NIL
    // NIL; an array, hash or object only itself, and only with lExact.
    public static decimal AScan(dynamic arr, dynamic xValue, decimal? nStart = null, decimal? nCount = null) =>
        ArrayScan(arr, xValue, nStart, nCount, false);

    public static decimal hb_AScan(dynamic arr, dynamic xValue, decimal? nStart = null,
                                   decimal? nCount = null, bool lExact = false) =>
        ArrayScan(arr, xValue, nStart, nCount, lExact);

    static decimal ArrayScan(object arr, object xValue, decimal? nStart, decimal? nCount, bool fExact)
    {
        if (arr is not object[] a)
            return 0;
        (long i, long n) = StartCount(a.Length, nStart, nCount);
        if (xValue is Delegate)
        {
            for (; n > 0 && i < a.Length; n--)
            {
                i++;
                if (Eval(xValue, a[i - 1], (decimal)i) is bool l && l)
                    return i;
            }
            return 0;
        }
        for (; n > 0; n--, i++)
            if (ScanMatch(a[i], xValue, fExact))
                return i + 1;
        return 0;
    }

    static bool ScanMatch(object x, object v, bool fExact)
    {
        switch (v)
        {
            case null: return x is null;
            case string s: return x is string xs && xs == s;
            case bool l: return x is bool xl && xl == l;
            case DateOnly d: return fExact ? x is DateOnly xd && xd == d : DayOf(x) is DateOnly xday && xday == d;
            case DateTime t: return fExact ? x is DateTime xt && xt == t : DayOf(x) is DateOnly tday && tday == DateOnly.FromDateTime(t);
        }
        if (IsNumeric(v))
            return IsNumeric(x) && Convert.ToDecimal(x, INV) == Convert.ToDecimal(v, INV);
        return fExact && ReferenceEquals(x, v);
    }

    static DateOnly? DayOf(object x) => x switch
    {
        DateOnly d => d,
        DateTime t => DateOnly.FromDateTime(t),
        _ => null,
    };

    // AEval( <a>, <bBlock>[, <nStart>[, <nCount>]] ): the block with each
    // element and its index; stops early if the block shrinks the array
    public static dynamic AEval(dynamic arr, dynamic block, decimal? nStart = null, decimal? nCount = null)
    {
        if (arr is object[] a && block is Delegate)
        {
            (long i, long n) = StartCount(a.Length, nStart, nCount);
            for (; n > 0 && i < a.Length; n--, i++)
                Eval(block, a[i], (decimal)(i + 1));
        }
        return arr;
    }

    // ADel()/AIns(): shift the elements after (from) nPos, leaving a NIL
    // at the end (at nPos); the array keeps its length. A position of 0
    // is 1; out of range, nothing happens.
    public static dynamic ADel(dynamic arr, decimal nPos)
    {
        if (arr is object[] a)
            ArrayDel(a, nPos);
        return arr;
    }

    public static dynamic AIns(dynamic arr, decimal nPos)
    {
        if (arr is object[] a)
            ArrayIns(a, nPos);
        return arr;
    }

    static bool ArrayDel(object[] a, decimal nPos)
    {
        long i = (long)Math.Truncate(nPos);
        if (i == 0)
            i = 1;
        if (i < 1 || i > a.Length)
            return false;
        System.Array.Copy(a, (int)i, a, (int)i - 1, a.Length - (int)i);
        a[^1] = null;
        return true;
    }

    static bool ArrayIns(object[] a, decimal nPos)
    {
        long i = (long)Math.Truncate(nPos);
        if (i == 0)
            i = 1;
        if (i < 1 || i > a.Length)
            return false;
        System.Array.Copy(a, (int)i - 1, a, (int)i, a.Length - (int)i);
        a[i - 1] = null;
        return true;
    }

    // hb_ADel( <a>, <nPos>[, <lAutoSize>] ) and hb_AIns( <a>, <nPos>[,
    // <xValue>[, <lAutoSize>]] ) (vm/arrayshb.c): ADel()/AIns() that can
    // also shrink or grow the array (lAutoSize), and set the inserted
    // element; they return the array, NIL for anything else. A C# array
    // cannot change length in place, so the emitter passes the array by
    // ref, as for AAdd() and ASize(): `ref dynamic[]`, or `ref dynamic` for
    // an lvalue typed dynamic (C# ref is invariant). An argument that is
    // not an lvalue (a literal, an element, a method's result) gets the
    // plain overload: the array returned is resized, but the caller's
    // array cannot follow — in Harbour it would, being the same array.
    public static dynamic[] hb_ADel(ref dynamic[] arr, decimal nPos, bool lAutoSize = false)
    {
        if (arr is not null && ArrayDel(arr, nPos) && lAutoSize)
            System.Array.Resize(ref arr, arr.Length - 1);
        return arr;
    }

    public static dynamic hb_ADel(ref dynamic arr, decimal nPos, bool lAutoSize = false)
    {
        if (arr is not object[] a)
            return null;
        dynamic[] d = a;
        return arr = hb_ADel(ref d, nPos, lAutoSize);
    }

    public static dynamic hb_ADel(dynamic arr, decimal nPos, bool lAutoSize = false)
    {
        if (arr is not object[] a)
            return null;
        dynamic[] d = a;
        return hb_ADel(ref d, nPos, lAutoSize);
    }

    public static dynamic[] hb_AIns(ref dynamic[] arr, decimal nPos, dynamic xValue = null, bool lAutoSize = false)
    {
        if (arr is null)
            return null;
        long i = (long)Math.Truncate(nPos);
        if (i == 0)
            i = 1;
        if (lAutoSize && i >= 1 && i <= arr.Length + 1)
            System.Array.Resize(ref arr, arr.Length + 1);
        if (ArrayIns(arr, i) && xValue is not null)
            arr[i - 1] = xValue;
        return arr;
    }

    public static dynamic hb_AIns(ref dynamic arr, decimal nPos, dynamic xValue = null, bool lAutoSize = false)
    {
        if (arr is not object[] a)
            return null;
        dynamic[] d = a;
        return arr = hb_AIns(ref d, nPos, xValue, lAutoSize);
    }

    public static dynamic hb_AIns(dynamic arr, decimal nPos, dynamic xValue = null, bool lAutoSize = false)
    {
        if (arr is not object[] a)
            return null;
        dynamic[] d = a;
        return hb_AIns(ref d, nPos, xValue, lAutoSize);
    }

    // ACopy( <aSource>, <aTarget>[, <nStart>[, <nCount>[, <nTargetPos>]]] )
    // (vm/arrays.c, hb_arrayCopy, as Harbour is built: with
    // HB_COMPAT_C53): elements copied into aTarget, which keeps its
    // length; a start past the end copies nothing. Returns aTarget; NIL
    // unless both are arrays.
    public static dynamic ACopy(dynamic aSource, dynamic aTarget, decimal? nStart = null,
                                decimal? nCount = null, decimal? nTargetPos = null)
    {
        if (aSource is not object[] src || aTarget is not object[] dst)
            return null;
        ulong uSrcLen = (ulong)src.Length, uDstLen = (ulong)dst.Length;
        ulong uStart = nStart is not null && SizeParam(nStart.Value) >= 1 ? SizeParam(nStart.Value) : 1;
        ulong uTarget = nTargetPos is not null && SizeParam(nTargetPos.Value) >= 1 ? SizeParam(nTargetPos.Value) : 1;
        if (uStart > uSrcLen)
            return aTarget;
        ulong uCount = nCount is not null && SizeParam(nCount.Value) <= uSrcLen - uStart
            ? SizeParam(nCount.Value) : uSrcLen - uStart + 1;
        if (uDstLen == 0)
            return aTarget;
        if (uTarget > uDstLen)
            uTarget = uDstLen;
        if (uCount > uDstLen - uTarget)
            uCount = uDstLen - uTarget + 1;
        for (ulong k = 0; k < uCount; k++)
            dst[uTarget - 1 + k] = src[uStart - 1 + k];
        return aTarget;
    }

    // ---- Type inspection ----

    public static string ValType(dynamic x)
    {
        if (x is null) return "U";
        if (x is string) return "C";
        if (IsNumeric(x)) return "N";
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
    public static void hb_default<T>(ref T val, T defVal)
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

    // FErase( <cFile> ): 0, or -1 (F_ERROR) with FError() set to the OS
    // error hb_fsDelete leaves (rtl/philes.c): 2 for a file that is not
    // there, 3 for a directory that is not, 5 for a directory or a
    // read-only file. File.Delete itself succeeds on a missing file, so
    // those cases are looked for first. No name, or an empty one, is 3.
    public static decimal FErase(string cFile)
    {
        if (string.IsNullOrEmpty(cFile))
        {
            s_lastError = 3;
            return -1;
        }
        try
        {
            // Directory and File name HbRuntime's own methods here
            if (System.IO.Directory.Exists(cFile))
                s_lastError = 5;
            else if (!System.IO.File.Exists(cFile))
                s_lastError = System.IO.Directory.Exists(Path.GetDirectoryName(Path.GetFullPath(cFile))) ? 2 : 3;
            else
            {
                System.IO.File.Delete(cFile);
                s_lastError = 0;
                return 0;
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            s_lastError = OsError(e);
        }
        return -1;
    }

    // The DOS/Windows error code FError() reports for a System.IO
    // exception. A Win32 failure carries its code in the HRESULT
    // (0x8007xxxx); the rest are mapped by kind.
    static int OsError(Exception e) =>
        (e.HResult & unchecked((int)0xFFFF0000)) == unchecked((int)0x80070000) ? e.HResult & 0xFFFF
        : e is FileNotFoundException ? 2
        : e is DirectoryNotFoundException ? 3
        : e is UnauthorizedAccessException ? 5
        : 31;       // ERROR_GEN_FAILURE

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
    public static bool ISNUMBER(dynamic x) => IsNumeric(x);
    public static bool ISLOGICAL(dynamic x) => x is bool;
    public static bool ISDATE(dynamic x) => x is DateOnly or DateTime;
    public static bool ISARRAY(dynamic x) => x is System.Array;
    public static bool ISHASH(dynamic x) => x is System.Collections.IDictionary;
    public static bool ISOBJECT(dynamic x) =>
        x is not null && !(x is string or bool or DateOnly or DateTime or System.Array or Delegate
                          or System.Collections.IDictionary || IsNumeric(x));
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

    // AFill( <a>, <xValue>[, <nStart>[, <nCount>]] ) (vm/arrayshb.c): a
    // count of 0 fills nothing; a negative start fills nothing, a start of
    // 0 is 1; a negative count means "to the end" from start 1 and
    // nothing from anywhere else
    public static dynamic AFill(dynamic arr, dynamic xValue, decimal? nStart = null, decimal? nCount = null)
    {
        if (arr is not object[] a)
            return arr;
        long lStart = nStart is null ? 0 : (long)Math.Truncate(nStart.Value);
        long lCount = nCount is null ? 0 : (long)Math.Truncate(nCount.Value);
        if ((nCount is not null && lCount == 0) || lStart < 0)
            return arr;
        if (lStart == 0)
            lStart = 1;
        decimal? dCount = nCount;
        if (lCount < 0)
        {
            if (lStart != 1)
                return arr;
            dCount = 0;
        }
        (long i, long n) = StartCount(a.Length, nStart is null ? null : lStart, dCount is null || dCount == 0 ? null : dCount);
        for (; n > 0; n--)
            a[i++] = xValue;
        return arr;
    }

    // ASort( <a>[, <nStart>[, <nCount>[, <bOrder>]]] ) (vm/asort.c): a
    // stable merge sort, in place, of nCount elements from nStart. The
    // block gets two elements and says whether the first goes first; a
    // result that is not a logical or a number counts as .T. Without a
    // block like types compare natively (strings exactly, by decision)
    // and unlike ones by Clipper's weights. Not an array, NIL.
    public static dynamic ASort(dynamic arr, decimal? nStart = null, decimal? nCount = null, dynamic bOrder = null)
    {
        if (arr is not object[] a)
            return null;
        long nLen = a.Length;
        ulong uStart = nStart is not null && SizeParam(nStart.Value) >= 1 ? SizeParam(nStart.Value) : 1;
        if (uStart > (ulong)nLen)
            return arr;
        ulong uCount = nCount is not null && SizeParam(nCount.Value) >= 1 && SizeParam(nCount.Value) <= (ulong)nLen - uStart
            ? SizeParam(nCount.Value) : (ulong)nLen - uStart + 1;
        if (uStart + uCount > (ulong)nLen)
            uCount = (ulong)nLen - uStart + 1;
        if (uCount <= 1)
            return arr;
        int iStart = (int)uStart - 1, n = (int)uCount;
        Func<int, int, bool> isLess = bOrder is Delegate
            ? (x, y) => Eval(bOrder, a[x], a[y]) switch
            {
                bool l => l,
                object v when IsNumeric(v) => Convert.ToDecimal(v, INV) != 0,
                _ => true,
            }
            : (x, y) => SortLess(a[x], a[y]);
        int[] mem = new int[2 * n];
        for (int k = 0; k < n; k++)
            mem[k] = iStart + k;
        int iDest = SortMerge(isLess, mem, 0, n, n) ? 0 : n;
        var sorted = new object[n];
        for (int k = 0; k < n; k++)
            sorted[k] = a[mem[iDest + k]];
        System.Array.Copy(sorted, 0, a, iStart, n);
        return arr;
    }

    // hb_arraySortDO: merge sort of the indices mem[src..src+n), using
    // mem[buf..buf+n) as the other buffer; true when the result is in src
    static bool SortMerge(Func<int, int, bool> isLess, int[] mem, int src, int buf, int nCount)
    {
        if (nCount <= 1)
            return true;
        int nCnt1 = nCount >> 1, nCnt2 = nCount - nCnt1;
        int p1 = src, p2 = src + nCnt1;
        bool fBuf1 = SortMerge(isLess, mem, p1, buf, nCnt1);
        bool fBuf2 = SortMerge(isLess, mem, p2, buf + nCnt1, nCnt2);
        int pDst;
        if (fBuf1)
            pDst = buf;
        else
        {
            pDst = src;
            p1 = buf;
        }
        if (!fBuf2)
            p2 = buf + nCnt1;
        while (nCnt1 > 0 && nCnt2 > 0)
        {
            if (isLess(mem[p2], mem[p1]))
            {
                mem[pDst++] = mem[p2++];
                nCnt2--;
            }
            else
            {
                mem[pDst++] = mem[p1++];
                nCnt1--;
            }
        }
        if (nCnt1 > 0)
            while (nCnt1-- > 0)
                mem[pDst++] = mem[p1++];
        else if (nCnt2 > 0 && fBuf1 == fBuf2)
            while (nCnt2-- > 0)
                mem[pDst++] = mem[p2++];
        return !fBuf1;
    }

    // hb_itemIsLess without a block
    static bool SortLess(object x, object y)
    {
        if (x is string sx && y is string sy)
            return string.CompareOrdinal(sx, sy) < 0;
        if (IsNumeric(x) && IsNumeric(y))
            return Convert.ToDecimal(x, INV) < Convert.ToDecimal(y, INV);
        if (x is DateTime tx && y is DateTime ty)
            return tx < ty;
        if (DayOf(x) is DateOnly dx && DayOf(y) is DateOnly dy)
            return dx < dy;
        if (x is bool lx && y is bool ly)
            return !lx && ly;
        return SortWeight(x) < SortWeight(y);
    }

    // Clipper's order across types: array/object, block, string, logical,
    // date, number, NIL (and a hash)
    static int SortWeight(object x) => x switch
    {
        null => 7,
        System.Collections.IDictionary => 7,
        Delegate => 2,
        string => 3,
        bool => 4,
        DateOnly or DateTime => 5,
        _ when IsNumeric(x) => 6,
        _ => 1,
    };

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
        [(int)_SET_DATEFORMAT] = "mm/dd/yy",       // Harbour's default, century off
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
            // A date format turns century on when its (first) year has
            // four digits, off otherwise (vm/set.c)
            if (key == (int)_SET_DATEFORMAT && newVal is string cFormat)
            {
                var m = System.Text.RegularExpressions.Regex.Match(cFormat, "[Yy]+");
                s_sets[(int)_SET_CENTURY] = m.Success && m.Length >= 4;
            }
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

    // SET CENTURY: turning it on or off rewrites the date format's year
    // as YYYY or YY (vm/set.c, hb_setSetCentury — which also upper-cases
    // the format).
    public static bool __SetCentury(dynamic val = null)
    {
        bool prev = s_sets[(int)_SET_CENTURY] is bool pb && pb;
        if (val is null)
            return prev;
        bool fCentury = AsOnOff(val);
        s_sets[(int)_SET_CENTURY] = fCentury;
        if (fCentury != prev)
        {
            string cFormat = SetDateFormat().ToUpperInvariant();
            int iStart = cFormat.IndexOf('Y');
            int iStop = iStart < 0 ? 0 : iStart;
            while (iStart >= 0 && iStop < cFormat.Length && cFormat[iStop] == 'Y')
                iStop++;
            if (iStart < 0)
                iStart = 0;
            s_sets[(int)_SET_DATEFORMAT] = cFormat.Substring(0, iStart) +
                (fCentury ? "YYYY" : "YY") + cFormat.Substring(iStop);
        }
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

    // ---- Binary conversions (Harbour builtins) ----
    // bin2l: 4-byte little-endian signed → numeric.
    // bin2i: 2-byte little-endian signed → numeric.
    // i2bin: numeric → 2-byte little-endian signed string.

    public static decimal Bin2L(string s)
    {
        if (s is null || s.Length < 4) return 0;
        int v = (byte)s[0] | ((byte)s[1] << 8) | ((byte)s[2] << 16) | ((byte)s[3] << 24);
        return v;
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

    // Derives from HbDynamicObject so a message in any casing reaches its
    // member, as Harbour's messages are case-insensitive: EasiPOS writes
    // oError:description, hbtest o:oscode and oError:Description, and a
    // plain class threw RuntimeBinderException for all but the exact
    // spelling.
    public class HbError : HbDynamicObject
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
// Harbour passes parameters by value; `@` opts into by-reference.
// C# `ref` is per-signature, so a param that ANY caller @-passes emits
// `ref` for ALL callers. This holder reconciles the callers who did
// NOT write `@` — they must still satisfy the `ref`, but Harbour
// semantics say their variable is untouched:
//
//   * omitted slot (Foo(a, , c)) or literal -> `ref HbDiscard<T>.Value`
//     — no input to preserve; the write-back is discarded.
//   * bare variable  (Foo(x) into a by-ref param) -> the callee still
//     sees x's VALUE as input, but the write-back must NOT reach the
//     caller's x. `ref HbDiscard<T>.Seed(x)` seeds the throwaway with
//     x, hands out a ref to it, and drops the write — exactly Harbour's
//     by-value semantics for an un-@'d argument (in-out and out-only
//     both correct).
//
// [ThreadStatic] so concurrent calls (the app builds with
// -DMULTITHREAD) don't race on the shared slot; Seed always assigns
// before the ref is read within the same call expression.
public static class HbDiscard<T>
{
   [System.ThreadStatic] public static T Value;
   public static ref T Seed(T v) { Value = v; return ref Value; }
}
