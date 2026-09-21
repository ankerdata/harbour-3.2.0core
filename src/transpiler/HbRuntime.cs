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

    // Harbour bit ops (rtl/hbbit.c). hb_bitAnd / hb_bitOr / hb_bitXor are
    // variadic; every operand is a number truncated to 64 bits (hb_parnint:
    // 7.9 is 7, where Convert.ToInt64 would round it to 8), anything else
    // an argument error. The result stays long so callers can compare it
    // to an int/long/decimal mask (`hb_bitAnd(nFlags, MASK) == MASK`).
    static long BitOperand(object x, string cFunc) => x switch
    {
        long l => l,
        int i => i,
        decimal d => (long)Math.Truncate(d),
        double d => (long)Math.Truncate(d),
        _ when IsNumeric(x) => (long)Math.Truncate(Convert.ToDecimal(x, INV)),
        _ => throw new ArgumentException("Argument error (" + cFunc + ")")
    };

    public static dynamic hb_bitAnd(params dynamic[] args)
    {
        long r = ~0L;
        foreach (var a in args) r &= BitOperand((object)a, "HB_BITAND");
        return r;
    }
    public static dynamic hb_bitOr(params dynamic[] args)
    {
        long r = 0L;
        foreach (var a in args) r |= BitOperand((object)a, "HB_BITOR");
        return r;
    }
    public static dynamic hb_bitXor(params dynamic[] args)
    {
        long r = 0L;
        foreach (var a in args) r ^= BitOperand((object)a, "HB_BITXOR");
        return r;
    }

    // hb_bitNot( <n> ): every bit inverted
    public static dynamic hb_bitNot(dynamic n) => ~BitOperand((object)n, "HB_BITNOT");

    // hb_bitShift( <n>, <nBits> ): left for a positive count, right
    // (arithmetic, the sign kept) for a negative one
    public static dynamic hb_bitShift(dynamic n, dynamic nBits)
    {
        long l = BitOperand((object)n, "HB_BITSHIFT");
        long b = BitOperand((object)nBits, "HB_BITSHIFT");
        return b < 0 ? l >> (int)(-b) : l << (int)b;
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
    // nShrinkBy characters; nothing is taken off for 0 or less; "" for
    // anything but a string (rtl/hbstrsh.c)
    public static string hb_StrShrink(string s, decimal nShrinkBy = 1)
    {
        if (s is null)
            return "";
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

    // Harbour counts days as Julian day numbers (common/hbdate.c,
    // hb_dateEncode), the empty date being 0. A date is a DateOnly here and
    // the empty date default(DateOnly), 0001-01-01 — Julian day 1721426,
    // DateOnly's day 0 — so that day itself reads as empty. Year 0, which
    // Harbour allows, cannot be represented and is the empty date too.
    const long JulianOfDayZero = 1721426;

    static long Julian(DateOnly d) => d == default ? 0 : d.DayNumber + JulianOfDayZero;

    static DateOnly FromJulian(long lJulian) =>
        lJulian <= JulianOfDayZero || lJulian - JulianOfDayZero > DateOnly.MaxValue.DayNumber
            ? default : DateOnly.FromDayNumber((int)(lJulian - JulianOfDayZero));

    // hb_dateEncode: a real date, or the empty date
    static DateOnly DateEncode(long nYear, long nMonth, long nDay) =>
        nYear >= 1 && nYear <= 9999 && nMonth >= 1 && nMonth <= 12 &&
        nDay >= 1 && nDay <= DateTime.DaysInMonth((int)nYear, (int)nMonth)
            ? new DateOnly((int)nYear, (int)nMonth, (int)nDay) : default;

    // vm/set.c, hb_setUpdateEpoch: a two-digit year takes its century from
    // SET EPOCH — below the epoch's own two digits, the century after
    static long UpdateEpoch(long nYear)
    {
        if (nYear >= 0 && nYear < 100)
        {
            long nEpoch = s_sets.TryGetValue((int)_SET_EPOCH, out var e) && IsNumeric((object)e)
                ? (long)Convert.ToDecimal((object)e, INV) : 1900;
            long nCentury = nEpoch / 100;
            if (nYear < nEpoch % 100)
                nCentury++;
            nYear += nCentury * 100;
        }
        return nYear;
    }

    // CToD( <cDate> ) and hb_CToD( <cDate>[, <cFormat>] ) (rtl/dates.c,
    // hb_dateUnformatRaw): the format fixes only the ORDER of day, month
    // and year; the string's digit runs are read in that order, each ended
    // by the first non-digit after it (non-digits before the first digit
    // are skipped), three at most, the rest ignored — "1999-11-25/10" under
    // yyyy-mm-dd is 25 November 1999. An impossible date is the empty date.
    public static DateOnly CToD(string cDate) => DateUnformat(cDate, SetDateFormat());

    public static DateOnly hb_CToD(string cDate, string cFormat = null) =>
        DateUnformat(cDate, cFormat ?? SetDateFormat());

    static DateOnly DateUnformat(string cDate, string cFormat)
    {
        if (cDate is null)
            return default;
        int nDPos = 0, nMPos = 0, nYPos = 0, nUsed = 0;
        foreach (char c in cFormat ?? "")
        {
            if (nUsed >= 3)
                break;
            switch (char.ToUpperInvariant(c))
            {
                case 'D':
                    if (nDPos == 0)
                    {
                        nUsed++;
                        nDPos = nMPos == 0 && nYPos == 0 ? 1 : nMPos == 0 || nYPos == 0 ? 2 : 3;
                    }
                    break;
                case 'M':
                    if (nMPos == 0)
                    {
                        nUsed++;
                        nMPos = nDPos == 0 && nYPos == 0 ? 1 : nDPos == 0 || nYPos == 0 ? 2 : 3;
                    }
                    break;
                case 'Y':
                    if (nYPos == 0)
                    {
                        nUsed++;
                        nYPos = nMPos == 0 && nDPos == 0 ? 1 : nMPos == 0 || nDPos == 0 ? 2 : 3;
                    }
                    break;
            }
        }
        long nDay = 0, nMonth = 0, nYear = 0;
        bool fNonDigit = true;
        nUsed = 0;
        foreach (char c in cDate)
        {
            if (c >= '0' && c <= '9')
            {
                if (nDPos == 1)
                    nDay = nDay * 10 + c - '0';
                else if (nMPos == 1)
                    nMonth = nMonth * 10 + c - '0';
                else if (nYPos == 1)
                    nYear = nYear * 10 + c - '0';
                fNonDigit = false;
            }
            else if (!fNonDigit)
            {
                fNonDigit = true;
                nDPos--;
                nMPos--;
                nYPos--;
                if (++nUsed >= 3)
                    break;
            }
        }
        return DateEncode(UpdateEpoch(nYear), nMonth, nDay);
    }

    // hb_DToC( <dDate>[, <cFormat>] ): DToC() in another format; a
    // timestamp gives its date
    public static string hb_DToC(dynamic dDate, string cFormat = null) =>
        DateFormat(DateString(AsDate((object)dDate)), cFormat ?? SetDateFormat());

    // hb_Date( [<nYear>, <nMonth>, <nDay>] ): today, or that date (the
    // empty date when there is no such day)
    public static DateOnly hb_Date(decimal? nYear = null, decimal? nMonth = null, decimal? nDay = null) =>
        nYear is null && nMonth is null && nDay is null
            ? Date()
            : DateEncode((long)(nYear ?? 0), (long)(nMonth ?? 0), (long)(nDay ?? 0));

    // a date, or a timestamp's date; anything else the empty date
    static DateOnly AsDate(object x) => x switch
    {
        DateOnly d => d,
        DateTime t => t == default ? default : DateOnly.FromDateTime(t),
        _ => default
    };

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

    // ---- Timestamps ----
    // A Harbour timestamp is a Julian day and the milliseconds of that day;
    // here it is a DateTime, the empty timestamp default(DateTime) and a
    // time with no date one on 0001-01-01. Harbour keeps milliseconds,
    // so the current time is cut to them.

    const long MilliSecsPerDay = 86400000;

    static (long lJulian, long lMSec) TimeStampParts(object x) => x switch
    {
        DateTime t => (Julian(DateOnly.FromDateTime(t)), t.TimeOfDay.Ticks / TimeSpan.TicksPerMillisecond),
        DateOnly d => (Julian(d), 0),
        _ => (0, 0)
    };

    static DateTime FromTimeStampParts(long lJulian, long lMSec) =>
        FromJulian(lJulian).ToDateTime(TimeOnly.MinValue).AddMilliseconds(lMSec);

    static DateTime NowToMilliSeconds()
    {
        DateTime t = DateTime.Now;
        return new DateTime(t.Ticks - t.Ticks % TimeSpan.TicksPerMillisecond, DateTimeKind.Unspecified);
    }

    // hb_timeEncode: the milliseconds of a real time of day, else 0
    static long TimeEncode(long nHour, long nMinutes, long nSeconds, long nMSec) =>
        nHour >= 0 && nHour < 24 && nMinutes >= 0 && nMinutes < 60 &&
        nSeconds >= 0 && nSeconds < 60 && nMSec >= 0 && nMSec < 1000
            ? ((nHour * 60 + nMinutes) * 60 + nSeconds) * 1000 + nMSec : 0;

    // hb_DateTime( [<nYear>, <nMonth>, <nDay>[, <nHour>, <nMinutes>,
    // <nSeconds>, <nMSec>]] ): now, or that timestamp — an impossible date
    // or time is 0 on its own side
    public static DateTime hb_DateTime(decimal? nYear = null, decimal? nMonth = null, decimal? nDay = null,
                                       decimal? nHour = null, decimal? nMinutes = null,
                                       decimal? nSeconds = null, decimal? nMSec = null)
    {
        if (nYear is null && nMonth is null && nDay is null && nHour is null &&
            nMinutes is null && nSeconds is null && nMSec is null)
            return NowToMilliSeconds();
        long L(decimal? n) => (long)Math.Truncate(n ?? 0);
        return FromTimeStampParts(Julian(DateEncode(L(nYear), L(nMonth), L(nDay))),
                                  TimeEncode(L(nHour), L(nMinutes), L(nSeconds), L(nMSec)));
    }

    static string SetTimeFormat() =>
        s_sets.TryGetValue((int)_SET_TIMEFORMAT, out var f) && f is string cFormat ? cFormat : "hh:mm:ss.fff";

    // hb_TToC( <tTimeStamp>[, <cDateFormat>[, <cTimeFormat>]] ): the date
    // in SET DATE's format or cDateFormat, a blank, the time in SET TIME
    // FORMAT's ("hh:mm:ss.fff") or cTimeFormat; an empty date format
    // leaves the time alone (rtl/dates.c, hb_timeStampFormat)
    public static string hb_TToC(dynamic tTimeStamp, string cDateFormat = null, string cTimeFormat = null)
    {
        var (lJulian, lMSec) = TimeStampParts((object)tTimeStamp);
        string cDate = DateFormat(DateString(FromJulian(lJulian)), cDateFormat ?? SetDateFormat());
        string cTime = TimeFormat(cTimeFormat ?? SetTimeFormat(), lMSec);
        return cDate.Length > 0 ? cDate + " " + cTime : cTime;
    }

    // hb_TToD( <tTimeStamp>[, @<cTime> | @<nSeconds>[, <cTimeFormat>]] ):
    // the date; the time goes to the second argument as text in
    // cTimeFormat ("" is SET TIME FORMAT), or as seconds without a format
    public static DateOnly hb_TToD(dynamic tTimeStamp) =>
        FromJulian(TimeStampParts((object)tTimeStamp).lJulian);

    public static DateOnly hb_TToD(dynamic tTimeStamp, ref string cTime, string cTimeFormat)
    {
        var (lJulian, lMSec) = TimeStampParts((object)tTimeStamp);
        cTime = TimeFormat(string.IsNullOrEmpty(cTimeFormat) ? SetTimeFormat() : cTimeFormat, lMSec);
        return FromJulian(lJulian);
    }

    public static DateOnly hb_TToD(dynamic tTimeStamp, ref decimal nSeconds)
    {
        var (lJulian, lMSec) = TimeStampParts((object)tTimeStamp);
        nSeconds = lMSec / 1000m;
        return FromJulian(lJulian);
    }

    // hb_TToMSec( <tTimeStamp> ): milliseconds from the start of Julian day
    // 0 — the Julian day times 86400000 plus the time
    public static decimal hb_TToMSec(dynamic tTimeStamp)
    {
        var (lJulian, lMSec) = TimeStampParts((object)tTimeStamp);
        return (decimal)lJulian * MilliSecsPerDay + lMSec;
    }

    // hb_TSToUTC( <tLocalTime> ): the same instant in UTC, by the local
    // zone's offset at that time
    public static DateTime hb_TSToUTC(DateTime tLocalTime)
    {
        if (tLocalTime == default)
            return default;
        DateTime tUtc = DateTime.SpecifyKind(tLocalTime, DateTimeKind.Local).ToUniversalTime();
        return DateTime.SpecifyKind(tUtc, DateTimeKind.Unspecified);
    }

    // hb_MilliSeconds(): the UTC time in milliseconds from Julian day 0
    // (common/hbdate.c, hb_dateMilliSeconds) — for measuring intervals
    public static decimal hb_MilliSeconds()
    {
        DateTime t = DateTime.UtcNow;
        return (decimal)Julian(DateOnly.FromDateTime(t)) * MilliSecsPerDay +
               t.TimeOfDay.Ticks / TimeSpan.TicksPerMillisecond;
    }

    // hb_SToT( <cDateTime> ): "YYYYMMDDhhmmss[fff]" (hb_timeStampStrRawGet)
    // — eight digits and more, or none, then two digits and more of time
    public static DateTime hb_SToT(string cDateTime = null)
    {
        if (cDateTime is null)
            return default;
        int Digits(int iFrom)
        {
            int n = 0;
            while (n < 10 && iFrom + n < cDateTime.Length && char.IsAsciiDigit(cDateTime[iFrom + n]))
                n++;
            return n;
        }
        long lJulian = 0, lMSec = 0;
        int iPos = 0, nLen = Digits(0);
        if (nLen == 8 || nLen >= 10)
        {
            lJulian = Julian(hb_SToD(cDateTime.Substring(0, 8)));
            iPos = 8;
            nLen -= 8;
        }
        if (nLen >= 2)
        {
            int n = Digits(iPos);
            int D(int i) => cDateTime[iPos + i] - '0';
            long nHour = 0, nMinutes = 0, nSeconds = 0, nMSec = 0;
            if (n >= 2 && ((n & 1) == 0 || n == 7 || n == 9))
            {
                nHour = D(0) * 10 + D(1);
                if (n >= 4)
                {
                    nMinutes = D(2) * 10 + D(3);
                    if (n >= 6)
                    {
                        nSeconds = D(4) * 10 + D(5);
                        nMSec = (n - 6) switch
                        {
                            4 or 3 => (D(6) * 10 + D(7)) * 10 + D(8),
                            2 => (D(6) * 10 + D(7)) * 10,
                            1 => D(6) * 100,
                            _ => 0
                        };
                    }
                }
            }
            lMSec = TimeEncode(nHour, nMinutes, nSeconds, nMSec);
        }
        return FromTimeStampParts(lJulian, lMSec);
    }

    // hb_StrToTS( <cDateTime> ): a timestamp from text (common/hbdate.c,
    // hb_timeStampStrGetUTC and hb_timeStrGetUTC) — a date YYYY-MM-DD
    // (or / or . between), YYYY-Www-D or YYYY-DDD, then a time after a
    // blank, a comma or a T: hh[:mm[:ss[.fff]]], am/pm, and a Z or a UTC
    // offset (+hh[:mm]), which moves the time to UTC. A time alone is a
    // timestamp on the empty date; text that is neither is the empty
    // timestamp.
    public static DateTime hb_StrToTS(string cDateTime)
    {
        TimeStampStrGet(cDateTime, out long lJulian, out long lMSec);
        return FromTimeStampParts(lJulian, lMSec);
    }

    static bool TimeStampStrGet(string c, out long lJulian, out long lMSec)
    {
        int nYear = 0, nMonth = 0, nDay = 0;
        bool fValid = false;
        int i = 0;
        string rest = null;
        char At(int k) => k < c.Length ? c[k] : '\0';
        bool Dig(int k) => char.IsAsciiDigit(At(k));
        if (c != null)
        {
            while (i < c.Length && IsHbSpace(c[i]))
                i++;
            c = c.Substring(i);         // At() and Dig() read the trimmed text
            rest = c;                   // no date: the whole of it is the time
            i = 0;
            if (Dig(0) && Dig(1) && Dig(2) && Dig(3) && At(4) is '-' or '/' or '.')
            {
                nYear = ((At(0) - '0') * 10 + (At(1) - '0')) * 10 * 10 + (At(2) - '0') * 10 + (At(3) - '0');
                if (Dig(5) && Dig(6) && At(7) == At(4) && Dig(8) && Dig(9) && !Dig(10))
                {
                    nMonth = (At(5) - '0') * 10 + (At(6) - '0');
                    nDay = (At(8) - '0') * 10 + (At(9) - '0');
                    if (DateEncode(nYear, nMonth, nDay) != default || (nYear == 0 && nMonth == 0 && nDay == 0))
                    {
                        i = 10;
                        fValid = true;
                    }
                }
                else if (At(5) is 'W' or 'w' && Dig(6) && Dig(7) && At(8) == At(4) && Dig(9) && !Dig(10))
                {
                    DateOnly d = WeekDate(nYear, (At(6) - '0') * 10 + (At(7) - '0'), At(9) - '0');
                    if (d != default)
                    {
                        (nYear, nMonth, nDay) = (d.Year, d.Month, d.Day);
                        i = 10;
                        fValid = true;
                    }
                }
                else if (At(4) == '-' && Dig(5) && Dig(6) && Dig(7) && !Dig(8))
                {
                    int nDayOfYear = ((At(5) - '0') * 10 + (At(6) - '0')) * 10 + (At(7) - '0');
                    if (nDayOfYear > 0 && (nDayOfYear <= 365 || (nDayOfYear == 366 && DateTime.IsLeapYear(Math.Max(nYear, 1)))))
                    {
                        DateOnly d = DateEncode(nYear, 1, 1);
                        if (d != default)
                        {
                            d = d.AddDays(nDayOfYear - 1);
                            (nYear, nMonth, nDay) = (d.Year, d.Month, d.Day);
                            i = 8;
                            fValid = true;
                        }
                    }
                }
                if (fValid)
                {
                    if (At(i) is 'T' or 't')
                    {
                        if (Dig(i + 1))
                            i++;
                        fValid = false;
                    }
                    else
                    {
                        if (At(i) is ',' or ';')
                            i++;
                        while (i < c.Length && IsHbSpace(c[i]))
                            i++;
                    }
                    rest = i < c.Length ? c.Substring(i) : null;
                }
                else
                {
                    nYear = nMonth = nDay = 0;
                    rest = null;
                }
            }
        }
        int nHour = 0, nMinutes = 0, nSeconds = 0, nMSec = 0, nUTCOffset = 0;
        if (TimeStrGet(rest, ref nHour, ref nMinutes, ref nSeconds, ref nMSec, ref nUTCOffset))
            fValid = true;
        else if (rest != null)
            fValid = false;
        lJulian = Julian(DateEncode(nYear, nMonth, nDay));
        lMSec = TimeEncode(nHour, nMinutes, nSeconds, nMSec);
        if (nUTCOffset != 0 && fValid)
        {
            lMSec -= nUTCOffset * 1000L;
            if (lMSec < 0)
            {
                lMSec += MilliSecsPerDay;
                if (--lJulian < 0)
                    fValid = false;
            }
            else if (lMSec >= MilliSecsPerDay)
            {
                lMSec -= MilliSecsPerDay;
                ++lJulian;
            }
        }
        return fValid;
    }

    // hb_timeStrGetUTC: hh[:mm[:ss[.fff]]], then Z, am/pm or a UTC offset
    static bool TimeStrGet(string c, ref int nHour, ref int nMinutes, ref int nSeconds,
                           ref int nMSec, ref int nUTCOffset)
    {
        nHour = nMinutes = nSeconds = nMSec = nUTCOffset = 0;
        if (c is null)
            return false;
        int i = 0, nBlocks = 0;
        char At(int k) => k < c.Length ? c[k] : '\0';
        bool Dig(int k) => char.IsAsciiDigit(At(k));
        while (IsHbSpace(At(i)))
            i++;
        if (!Dig(i))
            return false;
        nHour = At(i++) - '0';
        if (Dig(i))
            nHour = nHour * 10 + (At(i++) - '0');
        if (At(i) == ':' && Dig(i + 1))
        {
            nBlocks++;
            i++;
            nMinutes = At(i++) - '0';
            if (Dig(i))
                nMinutes = nMinutes * 10 + (At(i++) - '0');
            if (At(i) == ':' && Dig(i + 1))
            {
                nBlocks++;
                i++;
                nSeconds = At(i++) - '0';
                if (Dig(i))
                    nSeconds = nSeconds * 10 + (At(i++) - '0');
                if (At(i) == '.' && Dig(i + 1))
                {
                    nBlocks++;
                    i++;
                    nMSec = (At(i++) - '0') * 100;
                    if (Dig(i))
                    {
                        nMSec += (At(i++) - '0') * 10;
                        if (Dig(i))
                        {
                            int nFrac = 3;
                            nMSec += At(i++) - '0';
                            while (Dig(i) && ++nFrac <= 9)
                                i++;
                        }
                    }
                }
            }
        }
        if (nBlocks > 0 && At(i) is 'Z' or 'z')
            i++;
        else
        {
            while (IsHbSpace(At(i)))
                i++;
            if (At(i) is 'p' or 'P' && At(i + 1) is 'm' or 'M')
            {
                nBlocks++;
                i += 2;
                if (nHour == 0)
                    nHour = 24;
                else if (nHour != 12)
                    nHour += 12;
            }
            else if (At(i) is 'a' or 'A' && At(i + 1) is 'm' or 'M')
            {
                nBlocks++;
                i += 2;
                if (nHour == 0)
                    nHour = 24;
                else if (nHour == 12)
                    nHour = 0;
            }
            else
            {
                if (char.ToUpperInvariant(At(i)) == 'U' && char.ToUpperInvariant(At(i + 1)) == 'T' &&
                    char.ToUpperInvariant(At(i + 2)) == 'C' && At(i + 3) is '+' or '-')
                    i += 3;
                if (At(i) is '+' or '-' && Dig(i + 1))
                {
                    bool fMinus = At(i) == '-';
                    nUTCOffset = At(i + 1) - '0';
                    i += 2;
                    if (Dig(i))
                        nUTCOffset = nUTCOffset * 10 + (At(i++) - '0');
                    nUTCOffset *= 60;
                    if (At(i) == ':' && Dig(i + 1))
                        i++;
                    if (At(i) >= '0' && At(i) <= '5' && Dig(i + 1))
                    {
                        nUTCOffset += (At(i) - '0') * 10 + (At(i + 1) - '0');
                        i += 2;
                    }
                    nUTCOffset *= 60;
                    if (fMinus)
                        nUTCOffset = -nUTCOffset;
                }
            }
        }
        while (IsHbSpace(At(i)))
            i++;
        if (i >= c.Length && nBlocks > 0 && nHour < 24 && nMinutes < 60 && nSeconds < 60 &&
            nUTCOffset >= -43200 && nUTCOffset <= 43200)
            return true;
        nHour = nMinutes = nSeconds = nMSec = nUTCOffset = 0;
        return false;
    }

    // HB_ISSPACE: blank, tab, line feed, vertical tab, form feed, return
    static bool IsHbSpace(char c) => c == ' ' || (c >= '\t' && c <= '\r');

    // ISO 8601 week date: the given day of the given week of the year,
    // week 1 being the one holding the year's first Thursday
    static DateOnly WeekDate(int nYear, int nWeek, int nDayOfWeek)
    {
        if (nYear < 1 || nYear > 9999 || nWeek < 1 || nWeek > 53 || nDayOfWeek < 1 || nDayOfWeek > 7)
            return default;
        try
        {
            return DateOnly.FromDateTime(System.Globalization.ISOWeek.ToDateTime(nYear, nWeek,
                nDayOfWeek == 7 ? DayOfWeek.Sunday : (DayOfWeek)nDayOfWeek));
        }
        catch (ArgumentOutOfRangeException)
        {
            return default;
        }
    }

    // hb_timeDecode: hours, minutes, seconds and milliseconds of a time of
    // day; 0 or less, or a day or more, is midnight
    static void TimeDecode(long lMSec, out int nHour, out int nMinutes, out int nSeconds, out int nMSec)
    {
        nHour = nMinutes = nSeconds = nMSec = 0;
        if (lMSec <= 0 || lMSec >= MilliSecsPerDay)
            return;
        nMSec = (int)(lMSec % 1000);
        lMSec /= 1000;
        nSeconds = (int)(lMSec % 60);
        lMSec /= 60;
        nMinutes = (int)(lMSec % 60);
        nHour = (int)(lMSec / 60);
    }

    // rtl/dates.c, hb_timeFormat: h, m, s and f runs (upper or lower case,
    // 16 characters of format at most) take the hour, minutes, seconds and
    // fraction — a run of 2 zero-padded, of 1 or more than 2 a single
    // digit, f up to 4 places, rounded; a p run makes the hour 12-hour and
    // writes AM/PM (an hh hour below 10 then leads with a blank); anything
    // else is copied upper-cased
    static string TimeFormat(string cFormat, long lMSec)
    {
        TimeDecode(lMSec, out int iHour, out int iMinutes, out int iSeconds, out int iMSec);
        string f = cFormat.Length > 16 ? cFormat.Substring(0, 16) : cFormat;
        int nSize = f.Length, iPM = 0, i12 = 0;
        foreach (char c in f)
            if (char.ToUpperInvariant(c) == 'P')
            {
                if (iHour >= 12)
                {
                    iPM = 1;
                    iHour -= 12;
                }
                if (iHour == 0)
                    iHour += 12;
                if (iHour < 10)
                    i12 = 1;
                break;
            }
        var sb = new System.Text.StringBuilder();
        int i = 0;
        while (i < nSize)
        {
            int nCount = -i;
            char ch = char.ToUpperInvariant(f[i]);
            ++i;
            while (i < nSize && ch == char.ToUpperInvariant(f[i]))
                ++i;
            nCount += i;
            int nValue = 0, nDigits = 0;
            switch (ch)
            {
                case 'H':
                    nValue = iHour;
                    if (nCount == 2 && nValue >= 0)
                    {
                        if (i12 != 0)
                        {
                            sb.Append(' ');
                            --nCount;
                        }
                        nDigits = nCount;
                    }
                    else
                        nDigits = 1;
                    iHour = -1;
                    break;
                case 'M':
                    nValue = iMinutes;
                    iMinutes = -1;
                    nDigits = nCount > 2 ? 1 : nCount;
                    break;
                case 'S':
                    nValue = iSeconds;
                    iSeconds = -1;
                    nDigits = nCount > 2 ? 1 : nCount;
                    break;
                case 'F':
                    nValue = iMSec;
                    iMSec = -1;
                    nDigits = nCount > 4 ? 1 : nCount;
                    switch (nDigits)
                    {
                        case 4: nValue *= 10; break;
                        case 2: nValue = (nValue + 5) / 10; break;
                        case 1: nValue = (nValue + 50) / 100; break;
                    }
                    break;
                case 'P':
                    if (iPM >= 0)
                    {
                        sb.Append(iPM != 0 ? 'P' : 'A');
                        if (--nCount != 0)
                        {
                            sb.Append('M');
                            --nCount;
                        }
                        iPM = -1;
                    }
                    break;
            }
            if (nDigits != 0 && nValue >= 0)
            {
                nCount -= nDigits;
                var buf = new char[nDigits];
                do
                {
                    buf[--nDigits] = (char)('0' + nValue % 10);
                    nValue /= 10;
                }
                while (nDigits != 0);
                sb.Append(buf);
            }
            while (nCount-- > 0)
                sb.Append(ch);
        }
        return sb.ToString();
    }

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

    // AClone(): a copy with nested arrays and hashes copied too
    // (CloneNested); objects are shared. Not an array, NIL.
    public static dynamic AClone(dynamic arr) =>
        (object)arr is object[] a
            ? CloneNested(a, new Dictionary<object, object>(ReferenceEqualityComparer.Instance))
            : null;

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
        if (x is DateOnly) return "D";
        if (x is DateTime) return "T";      // a timestamp
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

    // Seconds(): seconds since midnight, to the millisecond
    // (common/hbdate.c, hb_dateSeconds)
    public static decimal Seconds() =>
        DateTime.Now.TimeOfDay.Ticks / TimeSpan.TicksPerMillisecond / 1000m;

    // ---- Files and directories ----
    // Ports of Harbour's file layer as it behaves on Windows: rtl/philes.c
    // (the F* functions), filesys.c (open flags and seek), fserr.c (the
    // error codes), direct.c with common/hbffind.c (Directory), spfiles.c
    // (File), memofile.c, fnsplit.c and hbfilehc.c (the name helpers),
    // hbfile.c, fstemp.c, vfile.c, diskspac.c, dirdrive.c, and the .prg
    // level of hbfilehi.prg and dirscan.prg.
    //
    // A Harbour string is bytes, so what crosses into or out of a file is
    // Latin-1 — one char per byte (Alex, 2026-09-19). Only hb_StrToUTF8 /
    // hb_UTF8ToStr convert.

    static readonly System.Text.Encoding s_byteCP = System.Text.Encoding.Latin1;

    // Open handles. Harbour hands out the OS handle; these are numbers
    // from 3 up, 0-2 being the console's there. The streams are unbuffered
    // so that, as in Harbour, every FWrite reaches the file at once and two
    // handles on one file see each other's writes.
    static readonly Dictionary<long, FileStream> s_files = new();
    static long s_nextHandle = 3;

    // FError() is per thread, as Harbour's is (it lives on the thread's
    // stack), and every F* function sets it: 0 on success, else the DOS
    // error code of its last operation. t_ioError is hb_fsError(), the
    // error of the last real file operation whoever made it — the
    // functions that only look a name up leave it alone, and the ones
    // that report "the last error" (hb_vfExists(), hb_cwd()) copy it.
    [ThreadStatic] static int t_fError;
    [ThreadStatic] static int t_ioError;

    // An operation's outcome: both FError() and hb_fsError().
    static int FsError(int nError) => t_fError = t_ioError = nError;

    // fileio.ch flags that only this layer uses
    const int HB_FO_READ = 0, HB_FO_WRITE = 1, HB_FO_READWRITE = 2,
              HB_FO_EXCLUSIVE = 0x10, HB_FO_CREAT = 0x100,
              HB_FO_TRUNC = 0x200, HB_FO_EXCL = 0x400;
    const int HB_FA_READONLY = 0x01, HB_FA_HIDDEN = 0x02, HB_FA_SYSTEM = 0x04,
              HB_FA_LABEL = 0x08, HB_FA_DIRECTORY = 0x10, HB_FA_ARCHIVE = 0x20,
              HB_FA_ALL = 0;        // fileio.ch: no attribute is asked for

    // The DOS error code Harbour reports for a failed System.IO call. A
    // Win32 failure carries its code in the HRESULT (0x8007xxxx) the way
    // GetLastError() gives it to hb_fsSetIOError(); the rest are mapped by
    // kind. NotSupportedException is what .NET raises where Windows would
    // fail a read on a write-only handle with ERROR_ACCESS_DENIED.
    static int OsError(Exception e) => WinToDosError(
        (e.HResult & unchecked((int) 0xFFFF0000)) == unchecked((int) 0x80070000) ? e.HResult & 0xFFFF
        : e is FileNotFoundException ? 2
        : e is DirectoryNotFoundException ? 3
        : e is UnauthorizedAccessException or NotSupportedException ? 5
        : 31);      // ERROR_GEN_FAILURE

    // hb_WinToDosError() (rtl/fserr.c): all it translates
    static int WinToDosError(int nError) => nError switch
    {
        183 or 1314 => 5,   // ERROR_ALREADY_EXISTS, ERROR_PRIVILEGE_NOT_HELD
        123 => 2,           // ERROR_INVALID_NAME
        _ => nError,
    };

    // The errors a System.IO call raises for a file it cannot use, as
    // opposed to a bug here.
    static bool IsFsFailure(Exception e) =>
        e is IOException or UnauthorizedAccessException or ArgumentException or
             NotSupportedException or System.Security.SecurityException;

    // What Windows reports for a name that is not there: 3 when its
    // directory is missing too, 2 otherwise.
    static int MissingError(string cFile)
    {
        try
        {
            string cDir = Path.GetDirectoryName(Path.GetFullPath(cFile));
            return cDir == null || System.IO.Directory.Exists(cDir) ? 2 : 3;
        }
        catch (Exception e) when (IsFsFailure(e)) { return 3; }
    }

    static bool TryGetFile(decimal nHandle, out FileStream fs)
    {
        lock (s_files)
            return s_files.TryGetValue((long) nHandle, out fs);
    }

    static FileAttributes AttrOf(int nAttr) =>
        FileAttributes.Archive |
        ((nAttr & HB_FA_READONLY) != 0 ? FileAttributes.ReadOnly : 0) |
        ((nAttr & HB_FA_HIDDEN) != 0 ? FileAttributes.Hidden : 0) |
        ((nAttr & HB_FA_SYSTEM) != 0 ? FileAttributes.System : 0);

    // hb_fsOpenEx() with convert_open_flags() (rtl/filesys.c, the Windows
    // branch): the create disposition comes from FO_CREAT / FO_TRUNC /
    // FO_EXCL, the access from the low two bits, and the share mode from
    // FO_EXCLUSIVE / FO_DENYWRITE / FO_DENYREAD — anything else (FO_COMPAT,
    // FO_DENYNONE, FO_SHARED) shares both.
    static decimal FsOpen(string cFile, int nFlags, int nAttr)
    {
        FileMode mode =
            (nFlags & HB_FO_CREAT) != 0
               ? (nFlags & HB_FO_EXCL) != 0 ? FileMode.CreateNew
               : (nFlags & HB_FO_TRUNC) != 0 ? FileMode.Create
               : FileMode.OpenOrCreate
            : (nFlags & HB_FO_TRUNC) != 0 ? FileMode.Truncate
            : FileMode.Open;
        FileAccess access = (nFlags & 3) switch
        {
            HB_FO_WRITE => FileAccess.Write,
            HB_FO_READWRITE => FileAccess.ReadWrite,
            _ => FileAccess.Read,
        };
        FileShare share = (nFlags & 0x70) switch
        {
            HB_FO_EXCLUSIVE => FileShare.None,
            0x20 => FileShare.Read,     // FO_DENYWRITE
            0x30 => FileShare.Write,    // FO_DENYREAD
            _ => FileShare.ReadWrite,
        };

        try
        {
            var fs = new FileStream(cFile, mode, access, share, bufferSize: 1);
            if (nAttr != 0 && mode is FileMode.Create or FileMode.CreateNew)
                System.IO.File.SetAttributes(cFile, AttrOf(nAttr));
            FsError(0);
            lock (s_files)
            {
                s_files[s_nextHandle] = fs;
                return s_nextHandle++;
            }
        }
        catch (Exception e) when (IsFsFailure(e))
        {
            FsError(OsError(e));
            return F_ERROR;
        }
    }

    // FOpen( <cFile>, [<nMode>] ): the handle, or -1. No file name is the
    // undocumented Clipper argument error.
    public static decimal FOpen(string cFile, decimal nMode = 0)
    {
        if (cFile == null)
        {
            FsError(0);
            throw new ArgumentException("Argument error (FOPEN)");
        }
        return FsOpen(cFile, (int) nMode, 0);
    }

    // FCreate( <cFile>, [<nAttr>] ): create or truncate, read-write and
    // exclusive (hb_fsCreate()).
    public static decimal FCreate(string cFile, decimal nAttr = 0)
    {
        if (cFile == null) { FsError(0); return F_ERROR; }
        return FsOpen(cFile, HB_FO_READWRITE | HB_FO_CREAT | HB_FO_TRUNC | HB_FO_EXCLUSIVE, (int) nAttr);
    }

    // hb_FCreate( <cFile>, [<nAttr>], [<nFlags>] ): as FCreate(), but the
    // share mode is the caller's (hb_fsCreateEx() drops the access bits).
    public static decimal hb_FCreate(string cFile, decimal nAttr = 0, decimal nFlags = 0)
    {
        if (cFile == null) { FsError(0); return F_ERROR; }
        return FsOpen(cFile, ((int) nFlags & ~3) | HB_FO_READWRITE | HB_FO_CREAT | HB_FO_TRUNC, (int) nAttr);
    }

    // FClose( <nHandle> ): .T. when it closed. An unknown handle is
    // ERROR_INVALID_HANDLE, as closing a closed handle is in Harbour.
    public static bool FClose(decimal nHandle)
    {
        FileStream fs;
        lock (s_files)
        {
            if (!s_files.Remove((long) nHandle, out fs))
            {
                FsError(6);
                return false;
            }
        }
        try
        {
            fs.Dispose();
            FsError(0);
            return true;
        }
        catch (Exception e) when (IsFsFailure(e))
        {
            FsError(OsError(e));
            return false;
        }
    }

    // FRead( <nHandle>, @<cBuffer>, <nBytes> ): reads into the buffer in
    // place, so it keeps its length. Clipper sized the buffer with
    // _parcsiz(), so nBytes may be one more than Len( cBuffer ) — that byte
    // lands on the terminating zero and is lost — and anything longer reads
    // nothing at all and returns 0.
    public static decimal FRead(decimal nHandle, ref string cBuf, decimal nBytes)
    {
        int nError = 0;
        int nRead = 0;

        if (cBuf != null && nBytes >= 0 && nBytes <= cBuf.Length + 1)
        {
            if (TryGetFile(nHandle, out var fs))
            {
                try
                {
                    byte[] b = new byte[(int) nBytes];
                    nRead = fs.Read(b, 0, b.Length);
                    if (nRead > 0)
                    {
                        char[] a = cBuf.ToCharArray();
                        for (int i = 0; i < nRead && i < a.Length; i++)
                            a[i] = (char) b[i];
                        cBuf = new string(a);
                    }
                }
                catch (Exception e) when (IsFsFailure(e))
                {
                    nError = OsError(e);
                    nRead = 0;
                }
            }
            else
                nError = 6;
        }

        FsError(nError);
        return nRead;
    }

    // FWrite( <nHandle>, <cBuffer>, [<nBytes>] ): writes the buffer's
    // bytes, at most nBytes of them. Writing nothing truncates the file at
    // the current position, as hb_fsWriteLarge()'s SetEndOfFile() does.
    public static decimal FWrite(decimal nHandle, string cBuf, decimal nBytes = -1)
    {
        int nError = 0;
        long nWritten = 0;

        if (cBuf != null)
        {
            int nLen = cBuf.Length;
            if (nBytes >= 0 && nBytes < nLen)
                nLen = (int) nBytes;

            if (TryGetFile(nHandle, out var fs))
            {
                try
                {
                    if (nLen == 0)
                        fs.SetLength(fs.Position);
                    else
                    {
                        fs.Write(s_byteCP.GetBytes(cBuf.Substring(0, nLen)), 0, nLen);
                        nWritten = nLen;
                    }
                }
                catch (Exception e) when (IsFsFailure(e))
                {
                    nError = OsError(e);
                    nWritten = 0;
                }
            }
            else
                nError = 6;
        }

        FsError(nError);
        return nWritten;
    }

    // FSeek( <nHandle>, <nOffset>, [<nOrigin>] ): the new position, or the
    // current one when the seek failed. hb_fsSeekLarge() refuses a negative
    // FS_SET offset itself, with error 25; a relative seek before the start
    // is Windows' ERROR_NEGATIVE_SEEK. Seeking past the end is allowed.
    public static decimal FSeek(decimal nHandle, decimal nOffset, decimal nOrigin = 0)
    {
        if (!TryGetFile(nHandle, out var fs))
        {
            FsError(6);
            return 0;
        }

        try
        {
            // convert_seek_flags(): FS_END beats FS_RELATIVE, and anything
            // else — FS_SET among it — counts from the start.
            int nOrg = (int) nOrigin;
            bool lFromStart = (nOrg & ((int) FS_RELATIVE | (int) FS_END)) == 0;
            if (lFromStart && nOffset < 0)
            {
                FsError(25);            // 'Seek Error'
                return fs.Position;
            }
            long nPos = (nOrg & (int) FS_END) != 0 ? fs.Length
                      : (nOrg & (int) FS_RELATIVE) != 0 ? fs.Position
                      : 0;
            nPos += (long) nOffset;
            if (nPos < 0)
            {
                FsError(131);           // ERROR_NEGATIVE_SEEK
                return fs.Position;
            }
            fs.Position = nPos;
            FsError(0);
            return nPos;
        }
        catch (Exception e) when (IsFsFailure(e))
        {
            FsError(OsError(e));
            return 0;
        }
    }

    public static decimal FError() => t_fError;

    // DosError( [<nError>] ): the OS error code the last raised error
    // carried, or what a caller stored here. Harbour keeps it on the
    // thread's stack and sets it from the error object.
    [ThreadStatic] static int t_dosError;

    public static decimal DosError(decimal nError = -1)
    {
        int nPrev = t_dosError;
        if (nError != -1)
            t_dosError = (int) nError;
        return nPrev;
    }

    // FErase( <cFile> ): 0, or -1 (F_ERROR) with FError() set to the OS
    // error hb_fsDelete() leaves: 2 for a file that is not there, 3 for a
    // directory that is not, 5 for a directory or a read-only file. .NET's
    // Delete succeeds on a missing file, so those cases are looked for
    // first. No name is 3.
    public static decimal FErase(string cFile)
    {
        if (string.IsNullOrEmpty(cFile))
        {
            FsError(3);
            return F_ERROR;
        }
        try
        {
            if (System.IO.Directory.Exists(cFile))
                FsError(5);
            else if (!System.IO.File.Exists(cFile))
                FsError(MissingError(cFile));
            else
            {
                System.IO.File.Delete(cFile);
                FsError(0);
                return 0;
            }
        }
        catch (Exception e) when (IsFsFailure(e))
        {
            FsError(OsError(e));
        }
        return F_ERROR;
    }

    // FRename( <cOldName>, <cNewName> ): 0, or -1 with FError() set.
    // MoveFile() renames a directory too, which .NET splits in two.
    public static decimal FRename(string cOld, string cNew)
    {
        if (cOld == null || cNew == null)
        {
            FsError(2);
            return F_ERROR;
        }
        try
        {
            if (System.IO.Directory.Exists(cOld))
                System.IO.Directory.Move(cOld, cNew);
            else if (!System.IO.File.Exists(cOld))
            {
                FsError(MissingError(cOld));
                return F_ERROR;
            }
            else
                System.IO.File.Move(cOld, cNew);
            FsError(0);
            return 0;
        }
        catch (Exception e) when (IsFsFailure(e))
        {
            FsError(OsError(e));
            return F_ERROR;
        }
    }

    // ---- Finding files ----

    // One directory entry, as FindFirstFile() reports it.
    readonly record struct FoundFile(string Name, FileAttributes Attr, long Size, DateTime WriteUtc);

    static int LastDelimiter(string cName)
    {
        for (int i = cName.Length - 1; i >= 0; i--)
            if (cName[i] is '\\' or '/' or ':')
                return i;
        return -1;
    }

    // hb_fsFindFirst() / hb_fsFindNext() (common/hbffind.c): the entries a
    // Windows mask matches — "." and ".." among them — less any hidden,
    // system or directory entry the attribute mask did not ask for.
    static List<FoundFile> FindFiles(string cMask, int nAttrMask)
    {
        var aFound = new List<FoundFile>();
        int nPos = LastDelimiter(cMask);
        string cDir = cMask.Substring(0, nPos + 1);
        string cPattern = cMask.Substring(nPos + 1);

        if (cPattern.Length == 0)
            return aFound;          // FindFirstFile() fails on a bare path

        try
        {
            string cExpr = System.IO.Enumeration.FileSystemName.TranslateWin32Expression(cPattern);
            var aEntries = new System.IO.Enumeration.FileSystemEnumerable<FoundFile>(
                cDir.Length == 0 ? "." : cDir,
                (ref System.IO.Enumeration.FileSystemEntry e) =>
                    new FoundFile(e.FileName.ToString(), e.Attributes,
                                  e.IsDirectory ? 0 : e.Length, e.LastWriteTimeUtc.UtcDateTime),
                new EnumerationOptions
                {
                    AttributesToSkip = 0,
                    IgnoreInaccessible = true,
                    RecurseSubdirectories = false,
                    ReturnSpecialDirectories = true,
                })
            {
                ShouldIncludePredicate = (ref System.IO.Enumeration.FileSystemEntry e) =>
                    System.IO.Enumeration.FileSystemName.MatchesWin32Expression(cExpr, e.FileName, ignoreCase: true),
            };

            foreach (FoundFile f in aEntries)
            {
                if ((f.Attr & FileAttributes.Hidden) != 0 && (nAttrMask & HB_FA_HIDDEN) == 0)
                    continue;
                if ((f.Attr & FileAttributes.System) != 0 && (nAttrMask & HB_FA_SYSTEM) == 0)
                    continue;
                if ((f.Attr & FileAttributes.Directory) != 0 && (nAttrMask & HB_FA_DIRECTORY) == 0)
                    continue;
                aFound.Add(f);
            }
        }
        catch (Exception e) when (IsFsFailure(e)) { }

        return aFound;
    }

    // hb_fsFile(): is there a file of this name? It searches with
    // HB_FA_ALL, which is 0 — so hb_fsFindNext() drops every hidden,
    // system and directory entry, and only a plain file answers. A name
    // with wildcards has to be searched for; a plain one is asked about
    // directly, which is what FindFirstFile() amounts to. A name that is
    // only a path ("C:\DIR\") matches nothing.
    static bool FindAny(string cMask)
    {
        string cName = cMask.Substring(LastDelimiter(cMask) + 1);
        if (cName.Length == 0)
            return false;
        if (cName.IndexOf('*') < 0 && cName.IndexOf('?') < 0)
        {
            try
            {
                var fi = new FileInfo(cMask);
                return fi.Exists && (fi.Attributes & (FileAttributes.Hidden | FileAttributes.System)) == 0;
            }
            catch (Exception e) when (IsFsFailure(e)) { return false; }
        }
        return FindFiles(cMask, HB_FA_ALL).Count > 0;
    }

    static string SetString(decimal nSet) =>
        s_sets.TryGetValue((int) nSet, out dynamic v) && v is string s ? s : "";

    // File( <cFile> ) (rtl/spfiles.c, hb_spFile): the name may hold
    // wildcards. With no path of its own it is looked for under SET
    // DEFAULT first and then along each SET PATH entry.
    public static bool File(string cFile)
    {
        if (cFile == null)
            return false;

        var fn = FNameSplit(cFile);
        if (fn.Path != null)
            return FindAny(FNameMerge(fn.Path, fn.Name, fn.Ext));

        if (FindAny(FNameMerge(SetString(_SET_DEFAULT), fn.Name, fn.Ext)))
            return true;

        foreach (string cDir in SetString(_SET_PATH).Split(';'))
            if (FindAny(FNameMerge(cDir, fn.Name, fn.Ext)))
                return true;

        return false;
    }

    public static bool hb_FileExists(string cFile)
    {
        try { return cFile != null && System.IO.File.Exists(cFile); }
        catch (Exception e) when (IsFsFailure(e)) { return false; }
    }

    public static bool hb_DirExists(string cDir)
    {
        try { return cDir != null && System.IO.Directory.Exists(cDir); }
        catch (Exception e) when (IsFsFailure(e)) { return false; }
    }

    // hb_vfExists() / hb_vfErase() / hb_vfSize(): the virtual file system's
    // names for the three, on a real file. They set FError() as the F*
    // functions do.
    public static bool hb_vfExists(string cFile)
    {
        if (cFile == null) { t_fError = 2; return false; }
        bool lFound = hb_FileExists(cFile);
        t_fError = t_ioError;
        return lFound;
    }

    public static decimal hb_vfErase(string cFile) => FErase(cFile);

    public static decimal hb_vfSize(string cFile, bool lUseDirEntry = true)
    {
        if (cFile == null)
            return 0;
        try
        {
            var fi = new FileInfo(cFile);
            if (fi.Exists)
            {
                FsError(0);
                return fi.Length;
            }
            FsError(System.IO.Directory.Exists(cFile) ? 0 : MissingError(cFile));
        }
        catch (Exception e) when (IsFsFailure(e))
        {
            FsError(OsError(e));
        }
        return 0;
    }

    // ---- Directory() ----

    // hb_fsAttrEncode() (common/hbffind.c)
    static int AttrEncode(string cAttr)
    {
        int nAttr = 0;
        foreach (char c in cAttr ?? "")
            nAttr |= char.ToUpperInvariant(c) switch
            {
                'R' => HB_FA_READONLY,
                'H' => HB_FA_HIDDEN,
                'S' => HB_FA_SYSTEM,
                'A' => HB_FA_ARCHIVE,
                'D' => HB_FA_DIRECTORY,
                'V' => HB_FA_LABEL,
                _ => 0,
            };
        return nAttr;
    }

    // hb_fsAttrDecode(), in Clipper's order. A Windows entry never carries
    // the volume label or link bits Harbour would write as V and L.
    static string AttrDecode(FileAttributes nAttr)
    {
        var cAttr = new System.Text.StringBuilder(5);
        if ((nAttr & FileAttributes.ReadOnly) != 0) cAttr.Append('R');
        if ((nAttr & FileAttributes.Hidden) != 0) cAttr.Append('H');
        if ((nAttr & FileAttributes.System) != 0) cAttr.Append('S');
        if ((nAttr & FileAttributes.Archive) != 0) cAttr.Append('A');
        if ((nAttr & FileAttributes.Directory) != 0) cAttr.Append('D');
        return cAttr.ToString();
    }

    // Directory( [<cDirSpec>], [<cAttr>] ) (rtl/direct.c): one
    // { name, size, date, "hh:mm:ss", attributes } per entry. Normal and
    // read-only files always; hidden, system and directories only when
    // <cAttr> asks for them, and "D" brings "." and ".." with it. A spec
    // ending in a path or drive separator gets "*.*" added, as Clipper's
    // did.
    public static dynamic[] Directory(string cDirSpec = null, string cAttr = null)
    {
        int nMask = HB_FA_ARCHIVE | HB_FA_READONLY | AttrEncode(cAttr);

        if (string.IsNullOrEmpty(cDirSpec))
            cDirSpec = "*.*";
        else if (cDirSpec[^1] is '\\' or '/' or ':')
            cDirSpec += "*.*";

        var aDir = new List<dynamic>();
        foreach (FoundFile f in FindFiles(cDirSpec, nMask))
        {
            DateTime tWrite = f.WriteUtc.ToLocalTime();
            aDir.Add(new dynamic[]
            {
                f.Name,
                (decimal) f.Size,
                DateOnly.FromDateTime(tWrite),
                tWrite.ToString("HH:mm:ss", INV),
                AttrDecode(f.Attr),
            });
        }
        return aDir.ToArray();
    }

    // hb_strMatchWildRaw() (common/strwild.c) as hb_strMatchFile() calls it
    // on Windows: the whole name, case-insensitive, and a pattern ending in
    // "." or ".*" also matches a name with no extension.
    static bool FileMatch(string cString, string cPattern)
    {
        bool fMatch = true, fAny = false;
        var anyP = new List<int>();
        var anyV = new List<int>();
        int nPosP = 0, nPosV = 0;
        int nLen = cString.Length, nSize = cPattern.Length;

        while (nPosP < nSize || (!fAny && nPosV < nLen))
        {
            if (nPosP < nSize && cPattern[nPosP] == '*')
            {
                fAny = true;
                nPosP++;
            }
            else if (nPosV < nLen && nPosP < nSize &&
                     (cPattern[nPosP] == '?' || AsciiUpper(cPattern[nPosP]) == AsciiUpper(cString[nPosV])))
            {
                if (fAny)
                {
                    anyP.Add(nPosP);
                    anyV.Add(nPosV);
                    fAny = false;
                }
                nPosV++;
                nPosP++;
            }
            else if (nPosV == nLen && nPosP < nSize && cPattern[nPosP] == '.' &&
                     (nPosP + 1 == nSize || (nPosP + 2 == nSize && cPattern[nPosP + 1] == '*')))
                break;
            else if (fAny && nPosV < nLen)
                nPosV++;
            else if (anyP.Count > 0)
            {
                int n = anyP.Count - 1;
                nPosP = anyP[n];
                nPosV = anyV[n] + 1;
                anyP.RemoveAt(n);
                anyV.RemoveAt(n);
                fAny = true;
            }
            else
            {
                fMatch = false;
                break;
            }
        }
        return fMatch;
    }

    static char AsciiUpper(char c) => c >= 'a' && c <= 'z' ? (char) (c - 32) : c;

    public static bool hb_FileMatch(string cFile, string cMask) =>
        cFile != null && cMask != null && FileMatch(cFile, cMask);

    // hb_DirScan( [<cPath>], [<cFileMask>], [<cAttr>] ) (rtl/dirscan.prg):
    // Directory()'s entries for the whole tree, each name carrying the path
    // below <cPath> it was found at.
    public static dynamic[] hb_DirScan(string cPath = null, string cFileMask = null, string cAttr = null) =>
        DirScan(hb_DirSepAdd(cPath ?? ""), cFileMask ?? "*.*", cAttr ?? "").ToArray();

    static List<dynamic> DirScan(string cPath, string cMask, string cAttr)
    {
        var aResult = new List<dynamic>();

        foreach (dynamic[] aFile in Directory(cPath + "*.*", cAttr + "D"))
        {
            string cName = (string) aFile[0];
            bool lMatch = FileMatch(cName, cMask);

            if (((string) aFile[4]).Contains('D'))
            {
                if (lMatch && cAttr.Contains('D'))
                    aResult.Add(aFile);
                if (cName != "." && cName != ".." && cName != "")
                    foreach (dynamic[] aSub in DirScan(cPath + cName + "\\", cMask, cAttr))
                    {
                        aSub[0] = cName + "\\" + (string) aSub[0];
                        aResult.Add(aSub);
                    }
            }
            else if (lMatch)
                aResult.Add(aFile);
        }
        return aResult;
    }

    // ---- Memo files ----

    // hb_fileLoad(): the whole file as bytes, or "" when it cannot be read.
    static string FileLoad(string cFile)
    {
        if (cFile == null)
            return "";
        try
        {
            using var fs = new FileStream(cFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var ms = new MemoryStream();
            fs.CopyTo(ms);
            return s_byteCP.GetString(ms.GetBuffer(), 0, (int) ms.Length);
        }
        catch (Exception e) when (IsFsFailure(e)) { return ""; }
    }

    // MemoRead() drops a trailing EOF character (Chr( 26 )), the Clipper
    // memo terminator; hb_MemoRead() reads the file as it stands.
    public static string MemoRead(string cFile)
    {
        string cData = FileLoad(cFile);
        return cData.Length > 0 && cData[^1] == (char) 26 ? cData.Substring(0, cData.Length - 1) : cData;
    }

    public static string hb_MemoRead(string cFile) => FileLoad(cFile);

    // MemoWrit() / hb_MemoWrit(): create or truncate, exclusive, write it
    // all. MemoWrit() adds the EOF character when the write succeeded.
    static bool MemoWrite(string cFile, string cData, bool lWriteEof)
    {
        if (cFile == null || cData == null)
            return false;
        try
        {
            using var fs = new FileStream(cFile, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
            fs.Write(s_byteCP.GetBytes(cData), 0, cData.Length);
            if (lWriteEof)
                fs.WriteByte(26);
            return true;
        }
        catch (Exception e) when (IsFsFailure(e)) { return false; }
    }

    public static bool MemoWrit(string cFile, string cData) => MemoWrite(cFile, cData, true);

    public static bool hb_MemoWrit(string cFile, string cData) => MemoWrite(cFile, cData, false);

    // ---- File names ----

    // What hb_fsFNameSplit() takes a name apart into. A part Harbour leaves
    // NULL is null here, which is what tells hb_spFile() that a name has no
    // path of its own; the Harbour-level functions store "" for it.
    readonly record struct FName(string Path, string Name, string Ext, string Drive);

    // hb_fsFNameSplit() (common/hbfsapi.c): the path is everything up to
    // and including the last "\", "/" or ":"; the extension starts at the
    // last "." of what is left, unless that dot is its first character.
    static FName FNameSplit(string cFileName)
    {
        if (cFileName == null)
            return new FName(null, null, null, null);

        string cPath = null, cDrive = null;
        int nPos = LastDelimiter(cFileName);
        if (nPos >= 0)
        {
            cPath = cFileName.Substring(0, nPos + 1);
            cFileName = cFileName.Substring(nPos + 1);
            int nColon = cPath.IndexOf(':');
            if (nColon >= 0)
                cDrive = cPath.Substring(0, nColon);
        }

        string cName = cFileName, cExt = null;
        int nDot = cFileName.LastIndexOf('.');
        if (nDot > 0)
        {
            cExt = cFileName.Substring(nDot);
            cName = cFileName.Substring(0, nDot);
        }
        if (cName.Length == 0)
            cName = null;

        return new FName(cPath, cName, cExt, cDrive);
    }

    // hb_fsFNameMerge(): path, then name, then extension, with a path
    // separator put in if the path lacks one and a dot if the extension
    // lacks one. A leading separator on the name is dropped.
    static string FNameMerge(string cPath, string cName, string cExt)
    {
        var cFileName = new System.Text.StringBuilder();

        if (cName != null && cName.Length > 0 && (cName[0] is '\\' or '/' or ':'))
            cName = cName.Substring(1);

        if (cPath != null)
            cFileName.Append(cPath);

        if (cFileName.Length > 0 && (cName != null || cExt != null) &&
            !(cFileName[^1] is '\\' or '/' or ':'))
            cFileName.Append('\\');

        if (cName != null)
            cFileName.Append(cName);

        if (cExt != null)
        {
            if (cExt.Length > 0 && cExt[0] != '.')
                cFileName.Append('.');
            cFileName.Append(cExt);
        }

        return cFileName.ToString();
    }

    // hb_FNameSplit( <cFileName>, @<cPath>, @<cName>, @<cExt>, @<cDrive> ):
    // each part that is not there comes back as "". The gaps EasiPOS leaves
    // are the overload with plain arguments — Harbour only writes back
    // what was passed by reference.
    public static void hb_FNameSplit(string cFileName, ref string cPath, ref string cName, ref string cExt)
    {
        var fn = FNameSplit(cFileName ?? "");
        cPath = fn.Path ?? "";
        cName = fn.Name ?? "";
        cExt = fn.Ext ?? "";
    }

    public static void hb_FNameSplit(string cFileName, ref string cPath, ref string cName, ref string cExt, ref string cDrive)
    {
        var fn = FNameSplit(cFileName ?? "");
        cPath = fn.Path ?? "";
        cName = fn.Name ?? "";
        cExt = fn.Ext ?? "";
        cDrive = fn.Drive ?? "";
    }

    public static void hb_FNameSplit(string cFileName, object _1, object _2, ref string cExt)
    {
        cExt = FNameSplit(cFileName ?? "").Ext ?? "";
    }

    public static string hb_FNameMerge(string cPath = null, string cName = null, string cExt = null) =>
        FNameMerge(cPath, cName, cExt);

    public static string hb_FNameDir(string cFileName) => FNameSplit(cFileName ?? "").Path ?? "";

    public static string hb_FNameName(string cFileName) => FNameSplit(cFileName ?? "").Name ?? "";

    public static string hb_FNameExt(string cFileName) => FNameSplit(cFileName ?? "").Ext ?? "";

    public static string hb_FNameNameExt(string cFileName)
    {
        var fn = FNameSplit(cFileName ?? "");
        return FNameMerge(null, fn.Name, fn.Ext);
    }

    public static string hb_FNameExtSet(string cFileName, string cExt = null)
    {
        var fn = FNameSplit(cFileName ?? "");
        return FNameMerge(fn.Path, fn.Name, cExt);
    }

    public static string hb_FNameExtSetDef(string cFileName, string cExt = null)
    {
        var fn = FNameSplit(cFileName ?? "");
        return FNameMerge(fn.Path, fn.Name, fn.Ext ?? cExt);
    }

    // ---- Paths and directories ----

    public static string hb_ps() => "\\";

    public static string hb_osDriveSeparator() => ":";

    public static string hb_osFileMask() => "*.*";

    static bool IsDriveSpec(string cDir) => cDir.EndsWith(':');

    // hb_DirSepAdd() (rtl/hbfilehi.prg)
    public static string hb_DirSepAdd(string cDir)
    {
        if (cDir == null)
            return "";
        if (!Empty(cDir) && !IsDriveSpec(cDir) && !cDir.EndsWith('\\'))
            cDir += "\\";
        return cDir;
    }

    // hb_DirSepDel()
    public static string hb_DirSepDel(string cDir)
    {
        if (cDir == null)
            return "";
        while (cDir.Length > 1 && cDir.EndsWith('\\') && cDir != "\\\\" && !cDir.EndsWith(":\\"))
            cDir = cDir.Substring(0, cDir.Length - 1);
        return cDir;
    }

    // hb_PathNormalize(): takes out every "." and empty step, and every
    // step followed by "..", leaving a path that means the same thing.
    public static string hb_PathNormalize(string cPath)
    {
        if (cPath == null)
            return "";
        if (Empty(cPath))
            return cPath;

        var aDir = new List<string>(cPath.Split('\\'));

        for (int i = aDir.Count - 1; i >= 0; i--)
        {
            string cDir = aDir[i];
            bool lLast = i == aDir.Count - 1;

            if (cDir == "." ||
                (Empty(cDir) && !lLast && (i + 1 > 2 || (i + 1 == 2 && !Empty(aDir[0])))))
                aDir.RemoveAt(i);
            else if (cDir != ".." && !Empty(cDir) && !IsDriveSpec(cDir))
            {
                if (!lLast && aDir[i + 1] == "..")
                {
                    aDir.RemoveAt(i + 1);
                    aDir.RemoveAt(i);
                }
            }
        }

        cPath = string.Join("\\", aDir);
        return cPath.Length == 0 ? ".\\" : cPath;
    }

    // hb_DirBuild(): create every directory the path names that is not
    // there yet. .F. as soon as one cannot be created, or a file stands
    // where a directory should.
    public static bool hb_DirBuild(string cDir)
    {
        if (cDir == null)
            return false;

        cDir = hb_PathNormalize(cDir);

        if (!hb_DirExists(cDir))
        {
            cDir = hb_DirSepAdd(cDir);

            string cDirTemp;
            int nPos = cDir.IndexOf(':');
            if (nPos >= 0)
            {
                cDirTemp = cDir.Substring(0, nPos + 1);
                cDir = cDir.Substring(nPos + 1);
            }
            else if (cDir.StartsWith("\\\\"))    // UNC path, network share
            {
                int nShare = cDir.IndexOf('\\', 2);
                cDirTemp = nShare < 0 ? "" : cDir.Substring(0, nShare + 1);
                cDir = cDir.Substring(cDirTemp.Length);
            }
            else if (cDir.StartsWith("\\"))
            {
                cDirTemp = "\\";
                cDir = cDir.Substring(1);
            }
            else
                cDirTemp = "";

            foreach (string cDirItem in cDir.Split('\\'))
            {
                if (!cDirTemp.EndsWith('\\') && cDirTemp != "")
                    cDirTemp += "\\";
                if (cDirItem != "")      // skip the root path, if any
                {
                    cDirTemp += cDirItem;
                    if (hb_FileExists(cDirTemp))
                        return false;
                    if (!hb_DirExists(cDirTemp) && MakeDir(cDirTemp) != 0)
                        return false;
                }
            }
        }

        return true;
    }

    // MakeDir() / hb_DirCreate(): 0, or the OS error. CreateDirectory()
    // fails on a name that is taken (ERROR_ALREADY_EXISTS, which Harbour
    // reports as 5) and on a missing parent, where .NET would build the
    // whole chain instead.
    public static decimal MakeDir(string cDir)
    {
        if (cDir == null)
            return F_ERROR;
        if (cDir.Length == 0)
            return 3;
        try
        {
            if (System.IO.File.Exists(cDir) || System.IO.Directory.Exists(cDir))
                return t_ioError = 5;
            string cParent = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(Path.GetFullPath(cDir)));
            if (cParent != null && !System.IO.Directory.Exists(cParent))
                return t_ioError = 3;
            System.IO.Directory.CreateDirectory(cDir);
            return t_ioError = 0;
        }
        catch (Exception e) when (IsFsFailure(e))
        {
            return t_ioError = OsError(e);
        }
    }

    // DirRemove() / hb_DirDelete()
    public static decimal DirRemove(string cDir)
    {
        if (cDir == null)
            return F_ERROR;
        try
        {
            if (!System.IO.Directory.Exists(cDir))
                return t_ioError = MissingError(cDir);
            System.IO.Directory.Delete(cDir);
            return t_ioError = 0;
        }
        catch (Exception e) when (IsFsFailure(e))
        {
            return t_ioError = OsError(e);
        }
    }

    // DirChange(): 0, or the OS error.
    public static decimal DirChange(string cDir)
    {
        if (cDir == null)
            return F_ERROR;
        try
        {
            System.IO.Directory.SetCurrentDirectory(cDir);
            return t_ioError = 0;
        }
        catch (Exception e) when (IsFsFailure(e))
        {
            // .NET reports both missing steps as DirectoryNotFound, where
            // Windows says 2 for the last one and 3 for any before it.
            return t_ioError = e is DirectoryNotFoundException or FileNotFoundException
                             ? MissingError(cDir) : OsError(e);
        }
    }

    // CurDir( [<cDrive>] ): the current directory of that drive (the
    // current one by default), without the drive, without the leading
    // separator and without the trailing one.
    public static string CurDir(string cDrive = null)
    {
        string cDir;
        try
        {
            cDir = cDrive != null && cDrive.Length > 0 && char.IsLetter(cDrive[0])
                 ? Path.GetFullPath(cDrive.Substring(0, 1) + ":")
                 : System.IO.Directory.GetCurrentDirectory();
        }
        catch (Exception e) when (IsFsFailure(e)) { return ""; }

        if (cDir.Length > 1 && cDir[1] == ':')
            cDir = cDir.Substring(2);
        if (cDir.Length > 0 && (cDir[0] is '\\' or '/'))
            cDir = cDir.Substring(1);
        if (cDir.Length > 0 && (cDir[^1] is '\\' or '/'))
            cDir = cDir.Substring(0, cDir.Length - 1);
        return cDir;
    }

    // hb_cwd( [<cNewDir>] ): the working directory, with its trailing
    // separator, and changes it when given one.
    public static string hb_cwd(string cNewDir = null)
    {
        string cDir;
        try { cDir = System.IO.Directory.GetCurrentDirectory(); }
        catch (Exception e) when (IsFsFailure(e)) { cDir = ""; }

        if (cDir.Length > 0 && !(cDir[^1] is '\\' or '/' or ':'))
            cDir += "\\";

        if (cNewDir != null)
            DirChange(cNewDir);
        t_fError = t_ioError;
        return cDir;
    }

    // hb_CurDrive(): the current drive's letter, without the colon.
    public static string hb_CurDrive()
    {
        try
        {
            string cDir = System.IO.Directory.GetCurrentDirectory();
            return cDir.Length > 1 && cDir[1] == ':' ? cDir.Substring(0, 1).ToUpperInvariant() : "";
        }
        catch (Exception e) when (IsFsFailure(e)) { return ""; }
    }

    // hb_DirBase(): the directory the running program is in, with its
    // trailing separator (hb_fsBaseDirBuff(), the path of argv[ 0 ]).
    public static string hb_DirBase()
    {
        string cExe = Environment.ProcessPath;
        if (cExe == null)
            return AppContext.BaseDirectory;
        var fn = FNameSplit(cExe);
        return fn.Path ?? "";
    }

    // DiskSpace( [<nDrive>] ): the free space the caller may use, in
    // bytes. 0 for a drive that is not there (where Harbour raises).
    public static decimal DiskSpace(decimal nDrive = 0)
    {
        if (nDrive < 0)
            return 0;
        try
        {
            string cRoot = nDrive == 0
                ? Path.GetPathRoot(System.IO.Directory.GetCurrentDirectory())
                : (char) ('A' + (int) nDrive - 1) + ":\\";
            return new DriveInfo(cRoot).AvailableFreeSpace;
        }
        catch (Exception e) when (IsFsFailure(e) || e is System.Runtime.InteropServices.COMException) { return 0; }
    }

    // ---- Temporary files ----

    // hb_DirTemp(): the temporary directory, with its trailing separator.
    public static string hb_DirTemp()
    {
        try { return Path.GetTempPath(); }
        catch (Exception e) when (IsFsFailure(e)) { return ""; }
    }

    // hb_FTempCreateEx( @<cName>, [<cDir>], [<cPrefix>], [<cExt>],
    // [<nAttr>] ): creates a file nobody else holds, named
    // <cDir><cPrefix>xxxxxx<cExt> with six random base-36 characters, and
    // hands back its handle and its name (hb_fsCreateTempEx()).
    public static decimal hb_FTempCreateEx(ref string cName, string cDir = null, string cPrefix = null,
                                           string cExt = null, decimal nAttr = 0)
    {
        for (int nAttempt = 0; nAttempt < 99; nAttempt++)
        {
            string cFile = string.IsNullOrEmpty(cDir) ? hb_DirTemp() : hb_DirSepAdd(cDir);
            cFile += cPrefix ?? "";
            for (int i = 0; i < 6; i++)
            {
                int n = Random.Shared.Next(36);
                cFile += (char) (n + (n > 9 ? 'a' - 10 : '0'));
            }
            cFile += cExt ?? "";

            cName = cFile;
            decimal nHandle = FsOpen(cFile, HB_FO_READWRITE | HB_FO_CREAT | HB_FO_TRUNC |
                                            HB_FO_EXCLUSIVE | HB_FO_EXCL, (int) nAttr);
            if (nHandle != F_ERROR)
                return nHandle;
        }
        return F_ERROR;
    }

    // ---- The process: arguments, environment, child processes, exit ----
    // Ports of vm/cmdarg.c, rtl/gete.c, rtl/hbrunfun.c, rtl/hbprocfn.c
    // with hbproces.c, vm/hvm.c (ErrorLevel), vm/initexit.c (__Quit),
    // rtl/idle.c and vm/garbage.c, as they behave on Windows.

    // hb_argc(): how many arguments follow the program's name. Harbour
    // counts its own //switches among them, and so does this.
    public static decimal hb_argc() => Environment.GetCommandLineArgs().Length - 1;

    // hb_argv( [<n>] ): argument n, "" past the last. Argument 0 is the
    // program, as the full path GetModuleFileName() gives — where .NET's
    // own argument 0 is the entry assembly, a .dll.
    public static string hb_argv(decimal n = 0)
    {
        int i = (int) n;
        if (i == 0)
            return Environment.ProcessPath ?? "";
        string[] aArgs = Environment.GetCommandLineArgs();
        return i > 0 && i < aArgs.Length ? aArgs[i] : "";
    }

    // hb_GetEnv( <cName>, [<cDefault>] ): the variable's value; cDefault,
    // or "", when it is not set or the name is empty.
    public static string hb_GetEnv(string cName, string cDefault = null)
    {
        string cValue = string.IsNullOrEmpty(cName) ? null : Environment.GetEnvironmentVariable(cName);
        return cValue ?? cDefault ?? "";
    }

    // hb_run( <cCommand> ): what the C runtime's system() does — the
    // command through %COMSPEC% /c, in this console — and its exit code;
    // -1 when it could not be started.
    public static decimal hb_run(string cCommand)
    {
        if (cCommand == null)
            throw new ArgumentException("Argument error (HB_RUN)");
        var psi = new System.Diagnostics.ProcessStartInfo(Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe")
        {
            Arguments = "/c " + cCommand,
            UseShellExecute = false,
        };
        return RunAndWait(psi);
    }

    // hb_processRun( <cCommand> ) (rtl/hbprocfn.c, hb_fsProcessRun()): the
    // command line straight to CreateProcess(), no shell, with this
    // process's standard handles; the exit code, or -1 when it could not be
    // started, with FError() set either way.
    public static decimal hb_processRun(string cCommand)
    {
        if (cCommand == null)
            throw new ArgumentException("Argument error (HB_PROCESSRUN)");
        SplitCommandLine(cCommand, out string cProgram, out string cArgs);
        var psi = new System.Diagnostics.ProcessStartInfo(cProgram)
        {
            Arguments = cArgs,
            UseShellExecute = false,
        };
        return RunAndWait(psi);
    }

    static decimal RunAndWait(System.Diagnostics.ProcessStartInfo psi)
    {
        try
        {
            using var p = System.Diagnostics.Process.Start(psi);
            p.WaitForExit();
            FsError(0);
            return p.ExitCode;
        }
        catch (System.ComponentModel.Win32Exception e)
        {
            FsError(WinToDosError(e.NativeErrorCode));
            return -1;
        }
    }

    // How CreateProcess() reads a command line it is given without an
    // application name: the program is the first token — up to the closing
    // quote when it starts with one — and the rest is passed on as it is.
    static void SplitCommandLine(string cCommand, out string cProgram, out string cArgs)
    {
        string c = cCommand.TrimStart();
        int nEnd;
        if (c.StartsWith('"'))
        {
            nEnd = c.IndexOf('"', 1);
            cProgram = nEnd < 0 ? c.Substring(1) : c.Substring(1, nEnd - 1);
            nEnd = nEnd < 0 ? c.Length : nEnd + 1;
        }
        else
        {
            nEnd = c.IndexOfAny(new[] { ' ', '\t' });
            if (nEnd < 0)
                nEnd = c.Length;
            cProgram = c.Substring(0, nEnd);
        }
        cArgs = c.Substring(nEnd).TrimStart();
    }

    // ErrorLevel( [<nNew>] ): the exit code the program will end with,
    // which a new value replaces. One for the whole process, as in Harbour;
    // Main returns it (easipos-transpiled host/).
    static int s_errorLevel;

    public static decimal ErrorLevel() => s_errorLevel;

    public static decimal ErrorLevel(decimal nNew)
    {
        int nPrev = s_errorLevel;
        s_errorLevel = (int) nNew;
        return nPrev;
    }

    // __Quit() — QUIT: ends the program with ErrorLevel() as its exit code.
    // Harbour runs its EXIT procedures on the way out; the corpus has none.
    public static void __Quit() => Environment.Exit(s_errorLevel);

    // hb_idleSleep( <nSeconds> ): sleep. Harbour runs its idle tasks (the
    // GC) while it waits; .NET does that on threads of its own.
    public static void hb_idleSleep(decimal nSeconds = 0) =>
        System.Threading.Thread.Sleep(nSeconds <= 0 ? 0 : (int) Math.Min(nSeconds * 1000m, int.MaxValue));

    // hb_gcAll( [<lForce>] ): a full collection.
    public static void hb_gcAll(bool lForce = true) => GC.Collect();

    // Version(): what the program runs on. Once the port is the product the
    // Harbour code is gone (Alex, 2026-09-21), so this is .NET's own
    // description, ".NET 10.0.x".
    public static string Version() => System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription;

    // ---- The call stack ----
    // ProcName( [<n>] ), ProcLine( [<n>] ), ProcFile( [<n>] ) (vm/proc.c):
    // the routine n calls up the stack — 0 is the one calling ProcName() —
    // its source file and its line in it. Once the port is the product the
    // C# is the source (Alex, 2026-09-21), so they name the C# method, its
    // .cs file and its line. File and line come from the debug symbols —
    // "" and 0 without them — and an optimised build may inline a small
    // routine out of the stack altogether. The runtime's own frames
    // (reflection, the call sites behind `dynamic`) are not routines, and
    // are not counted.

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public static string ProcName(decimal nLevel = 0) => RoutineName(CallerFrame(nLevel));

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public static decimal ProcLine(decimal nLevel = 0) => CallerFrame(nLevel)?.GetFileLineNumber() ?? 0;

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public static string ProcFile(decimal nLevel = 0) => Path.GetFileName(CallerFrame(nLevel)?.GetFileName() ?? "");

    // The routine nLevel above the caller of ProcName() / ProcLine() /
    // ProcFile(): the trace starts past this method and the ProcXxx() that
    // called it.
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    static System.Diagnostics.StackFrame CallerFrame(decimal nLevel)
    {
        int n = (int) nLevel;
        if (n < 0)
            return null;
        foreach (var f in new System.Diagnostics.StackTrace(2, true).GetFrames())
        {
            var m = f.GetMethod();
            string ns = m?.DeclaringType?.Namespace ?? "";
            if (m?.DeclaringType == null || ns == "System" || ns.StartsWith("System.") || ns.StartsWith("Microsoft."))
                continue;
            if (n-- == 0)
                return f;
        }
        return null;
    }

    // A routine as C# names it: one of Program's functions by its own name,
    // a method with its class ("Transaction.SetAllValues"), and a codeblock —
    // compiled to "<Outer>b__3_0" in a closure class nested in the routine's
    // own — as Harbour writes one, "(b)" and the routine it sits in.
    static string RoutineName(System.Diagnostics.StackFrame f)
    {
        var m = f?.GetMethod();
        if (m == null)
            return "";
        string cName = m.Name;
        Type t = m.DeclaringType;
        int nEnd = cName.IndexOf('>');
        bool lBlock = cName.StartsWith('<') && nEnd > 1;
        if (lBlock)
        {
            cName = cName.Substring(1, nEnd - 1);
            while (t?.DeclaringType != null && t.Name.StartsWith('<'))
                t = t.DeclaringType;
        }
        string cRoutine = t == null || t.Name == "Program" ? cName : t.Name + "." + cName;
        return lBlock ? "(b)" + cRoutine : cRoutine;
    }

    // ---- The console ----
    // rtl/console.c, inkey.c, gx.c, accept.c. The test program is the only
    // caller of the keyboard and screen ones.

    // OutStd( ... ) / OutErr( ... ): each value as ? would write it,
    // separated by single spaces, with no newline, to stdout / stderr.
    public static void OutStd(params dynamic[] aValues) => WriteValues(Console.Out, aValues);

    public static void OutErr(params dynamic[] aValues) => WriteValues(Console.Error, aValues);

    static void WriteValues(TextWriter w, dynamic[] aValues)
    {
        for (int i = 0; i < aValues.Length; i++)
        {
            if (i > 0)
                w.Write(' ');
            w.Write(Fmt(aValues[i]));
        }
        w.Flush();
    }

    // Inkey( [<nSeconds>] ): the next key's code, 0 when there is none.
    // With no argument it does not wait; 0 waits until a key comes. With no
    // console to read (input redirected) there are no keys.
    public static decimal Inkey() => ReadKey(false, 0);

    public static decimal Inkey(decimal nSeconds) => ReadKey(true, nSeconds);

    static decimal ReadKey(bool lWait, decimal nSeconds)
    {
        if (Console.IsInputRedirected)
        {
            if (lWait && nSeconds > 0)
                hb_idleSleep(nSeconds);
            return 0;
        }
        DateTime tEnd = DateTime.Now.AddSeconds((double) nSeconds);
        try
        {
            while (!Console.KeyAvailable)
            {
                if (!lWait || (nSeconds > 0 && DateTime.Now >= tEnd))
                    return 0;
                System.Threading.Thread.Sleep(10);
            }
            return KeyCode(Console.ReadKey(true));
        }
        catch (Exception e) when (e is InvalidOperationException or IOException)
        {
            return 0;       // no console to read keys from
        }
    }

    // The inkey.ch code of a key: its character for a plain one, the K_*
    // value for the keys that have one.
    static decimal KeyCode(ConsoleKeyInfo k) => k.Key switch
    {
        ConsoleKey.UpArrow => 5,        // K_UP
        ConsoleKey.DownArrow => 24,     // K_DOWN
        ConsoleKey.LeftArrow => 19,     // K_LEFT
        ConsoleKey.RightArrow => 4,     // K_RIGHT
        ConsoleKey.Home => 1,           // K_HOME
        ConsoleKey.End => 6,            // K_END
        ConsoleKey.PageUp => 18,        // K_PGUP
        ConsoleKey.PageDown => 3,       // K_PGDN
        ConsoleKey.Insert => 22,        // K_INS
        ConsoleKey.Delete => 7,         // K_DEL
        ConsoleKey.F1 => 28,            // K_F1
        >= ConsoleKey.F2 and <= ConsoleKey.F10 => -(k.Key - ConsoleKey.F2 + 1),    // K_F2 .. K_F10
        _ => k.KeyChar,
    };

    // hb_keyStd( <nKey> ): the standard code of a key. Inkey() gives
    // standard codes already.
    public static decimal hb_keyStd(decimal nKey) => nKey;

    // SetMode( <nRows>, <nCols> ): size the screen — here, the console's
    // buffer — and say whether that worked. There is no screen to size when
    // output is redirected.
    public static bool SetMode(decimal nRows, decimal nCols)
    {
        if (Console.IsOutputRedirected || !OperatingSystem.IsWindows())
            return false;
        try
        {
            Console.SetBufferSize((int) nCols, (int) nRows);
            return true;
        }
        catch (Exception e) when (e is IOException or ArgumentOutOfRangeException) { return false; }
    }

    // __Accept( <cPrompt> ) — ACCEPT: the prompt as ? writes it, then a line
    // typed in.
    public static string __Accept(dynamic cPrompt)
    {
        QOut(cPrompt);
        return Console.ReadLine() ?? "";
    }

    // DiskChange( <cDrive> ): make that drive current (rtl/dirdrive.c). It
    // keeps its own current directory, as the drive's does in Windows.
    public static bool DiskChange(string cDrive)
    {
        if (string.IsNullOrEmpty(cDrive) || !char.IsAsciiLetter(cDrive[0]))
            return false;
        try
        {
            System.IO.Directory.SetCurrentDirectory(Path.GetFullPath(char.ToUpperInvariant(cDrive[0]) + ":"));
            return true;
        }
        catch (Exception e) when (IsFsFailure(e)) { return false; }
    }

    // ---- Date decomposition ----

    static DateTime AsDT(dynamic d) => d is DateOnly dd ? dd.ToDateTime(TimeOnly.MinValue)
                                     : d is DateTime dt ? dt
                                     : default;
    // Year() / Month() / Day() / DoW() (Sunday is 1) of a date or a
    // timestamp; for the empty date all four are 0 (hb_dateDecode of
    // Julian day 0), and CDoW() / CMonth() are ""
    static bool IsEmptyDate(object d) => AsDate(d) == default;
    public static decimal Year(dynamic d) => IsEmptyDate((object)d) ? 0 : AsDT(d).Year;
    public static decimal Month(dynamic d) => IsEmptyDate((object)d) ? 0 : AsDT(d).Month;
    public static decimal Day(dynamic d) => IsEmptyDate((object)d) ? 0 : AsDT(d).Day;
    public static decimal DoW(dynamic d) => IsEmptyDate((object)d) ? 0 : (decimal)((int)AsDT(d).DayOfWeek + 1);
    public static string CDoW(dynamic d) => IsEmptyDate((object)d) ? "" : AsDT(d).ToString("dddd", INV);
    public static string CMonth(dynamic d) => IsEmptyDate((object)d) ? "" : AsDT(d).ToString("MMMM", INV);

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

    // ---- Hashes ----
    // A hash is an OrderedDictionary<string, dynamic> — <long, dynamic>
    // for a numeric-keyed one, <dynamic, dynamic> where the emitter knows
    // no key type (gencsharp.c): Harbour's default hash keeps its keys in
    // the order they were added, deletes included (HB_HASH_KEEPORDER),
    // and compares string keys exactly (HB_HASH_BINARY). Anything that
    // implements IDictionary is taken as a hash.

    static System.Collections.IDictionary AsDict(dynamic h) => h as System.Collections.IDictionary;
    // The (object) casts below are load-bearing. AsDict(h) with a
    // dynamic argument is itself dynamically bound, which makes `d`
    // dynamic too — and then d.Contains(...) resolves against the
    // hash's RUNTIME type (e.g. OrderedDictionary<string, dynamic>, which
    // has no public Contains): RuntimeBinderException. AsDict((object)h)
    // binds statically, `d` is IDictionary, and the key casts keep the
    // member calls bound to the non-generic IDictionary surface.

    // A key as the hash is keyed. Through the non-generic IDictionary a
    // key of another CLR type is simply not there — an int literal 5 is
    // not a long 5 nor a decimal 5 — so a number is converted to the
    // hash's key type: long for a numeric-keyed hash (which must then be
    // an integer: EasiPOS keys by ids, and 1.5 would silently become 1),
    // decimal where the key type is open, as Harbour compares numbers by
    // value. Anything else is used as given.
    static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, Type> s_hashKeyTypes = new();

    static Type HashKeyType(Type t)
    {
        foreach (Type i in t.GetInterfaces())
            if (i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDictionary<,>))
                return i.GetGenericArguments()[0];
        return typeof(object);
    }

    static object HashKey(System.Collections.IDictionary d, object key)
    {
        if (key is null || !IsNumeric(key))
            return key;
        Type t = s_hashKeyTypes.GetOrAdd(d.GetType(), HashKeyType);
        decimal n = Convert.ToDecimal(key, INV);
        if (t == typeof(long))
        {
            if (n != Math.Truncate(n))
                throw new ArgumentException("A numeric-keyed hash is keyed by integers: " + n.ToString(INV));
            return (long)n;
        }
        if (t == typeof(decimal) || t == typeof(object))
            return n;
        return key;
    }

    // hb_HGetDef( <h>, <key>[, <xDefault>] ): the value, or xDefault
    public static dynamic hb_HGetDef(dynamic h, dynamic key, dynamic def = null)
    {
        var d = AsDict((object)h);
        if (d == null)
            return def;
        object k = HashKey(d, (object)key);
        return d.Contains(k) ? d[k] : def;
    }

    public static bool hb_HHasKey(dynamic h, dynamic key)
    {
        var d = AsDict((object)h);
        return d != null && d.Contains(HashKey(d, (object)key));
    }

    // hb_HGet( <h>, <key> ): the value; a key that is not there is a
    // bound error (EG_BOUND 1132)
    public static dynamic hb_HGet(dynamic h, dynamic key)
    {
        var d = AsDict((object)h);
        object k = d == null ? null : HashKey(d, (object)key);
        if (d == null || !d.Contains(k))
            throw new KeyNotFoundException("Bound error: array access (HB_HGET)");
        return d[k];
    }

    // hb_HSet( <h>, <key>, <xValue> ): add or replace — a replaced key
    // keeps its place; returns the hash
    public static dynamic hb_HSet(dynamic h, dynamic key, dynamic value)
    {
        var d = AsDict((object)h);
        if (d != null)
            d[HashKey(d, (object)key)] = (object)value;
        return h;
    }

    // hb_HDel( <h>, <key> ): returns the hash
    public static dynamic hb_HDel(dynamic h, dynamic key)
    {
        var d = AsDict((object)h);
        if (d != null)
            d.Remove(HashKey(d, (object)key));
        return h;
    }

    // hb_HPos( <h>, <key> ): the key's position, 1-based; 0 if absent
    public static decimal hb_HPos(dynamic h, dynamic key)
    {
        var d = AsDict((object)h);
        if (d == null)
            return 0;
        object k = HashKey(d, (object)key);
        int i = 0;
        foreach (object each in d.Keys)
        {
            i++;
            if (Equals(each, k))
                return i;
        }
        return 0;
    }

    // hb_HKeyAt( <h>, <nPos> ): the key at a position, 1-based; a position
    // outside the hash is a bound error (EG_BOUND 1187)
    public static dynamic hb_HKeyAt(dynamic h, decimal nPos)
    {
        var d = AsDict((object)h);
        long n = (long)Math.Truncate(nPos);
        if (d != null && n >= 1 && n <= d.Count)
        {
            long i = 0;
            foreach (object each in d.Keys)
                if (++i == n)
                    return each;
        }
        throw new ArgumentOutOfRangeException(nameof(nPos), "Bound error (HB_HKEYAT)");
    }

    // hb_HKeepOrder( <h>[, <lKeepOrder>] ): whether the hash keeps its
    // keys in the order they were added. A hash here always does; Harbour
    // would sort one told .F. — EasiPOS only ever says .T.
    public static bool hb_HKeepOrder(dynamic h, bool? lKeepOrder = null) =>
        AsDict((object)h) != null;

    // hb_HClone( <h> ): a copy of the hash, its nested arrays and hashes
    // copied too (CloneNested); NIL for anything else
    public static dynamic hb_HClone(dynamic h) =>
        (object)h is System.Collections.IDictionary d
            ? CloneNested(d, new Dictionary<object, object>(ReferenceEqualityComparer.Instance))
            : null;

    // AClone() and hb_HClone() (vm/arrays.c, hb_nestedCloneDo): nested
    // arrays and hashes are cloned as well, each once — an array reached
    // twice is one clone reached twice, a cycle stays a cycle. A hash is
    // cloned as its own C# type. An object is shared, where Harbour would
    // clone its instance variables.
    static object CloneNested(object x, Dictionary<object, object> done)
    {
        if (x is object[] a)
        {
            if (done.TryGetValue(a, out object c))
                return c;
            var copy = new object[a.Length];
            done[a] = copy;
            for (int i = 0; i < a.Length; i++)
                copy[i] = CloneNested(a[i], done);
            return copy;
        }
        if (x is System.Collections.IDictionary d)
        {
            if (done.TryGetValue(d, out object c))
                return c;
            var copy = (System.Collections.IDictionary)Activator.CreateInstance(d.GetType());
            done[d] = copy;
            foreach (System.Collections.DictionaryEntry e in d)
                copy.Add(e.Key, CloneNested(e.Value, done));
            return copy;
        }
        return x;
    }

    // Harbour `$` operator: `a $ b` is substring containment when b is
    // a string — an empty a is in nothing (hb_strAt) — and key containment
    // when b is a hash
    public static bool HbIn(dynamic needle, dynamic haystack)
    {
        if (haystack is string s)
            return needle is string n && n.Length > 0 && s.Contains(n, StringComparison.Ordinal);
        if (haystack is System.Collections.IDictionary d)
            return d.Contains(HashKey(d, (object)needle));
        return false;
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
    public const decimal _SET_TIMEFORMAT = 116;
    // common.ch
    public const string CRLF = "\r\n";

    // ---- Set() ----
    // Harbour's Set( _SET_XXX [, newVal] ) reads/writes a setting. Keyed on
    // the numeric _SET_* identifier. Returns the prior value.
    //
    // Every SET Harbour has, with its default (vm/set.c, hb_setInitialize,
    // and the values HB_FUNC( SET ) reports for the few it computes). The
    // default also fixes each SET's type, which is all Set() will take:
    // a logical SET takes .T. / .F. or "ON" / "OFF", a numeric one a
    // number, a string one a string, and anything else leaves the setting
    // as it was (set_logical(), set_number(), set_string()). Harbour keeps
    // them per thread, copied from the parent's when a thread starts; here
    // they are one set for the process until threads come (family 10).

    static readonly Dictionary<int, dynamic> s_sets = new()
    {
        [1]   = false,              // _SET_EXACT
        [2]   = false,              // _SET_FIXED
        [3]   = 2m,                 // _SET_DECIMALS
        [4]   = "mm/dd/yy",         // _SET_DATEFORMAT, century off
        [5]   = 1900m,              // _SET_EPOCH
        [6]   = "",                 // _SET_PATH
        [7]   = "",                 // _SET_DEFAULT
        [8]   = true,               // _SET_EXCLUSIVE
        [9]   = false,              // _SET_SOFTSEEK
        [10]  = false,              // _SET_UNIQUE
        [11]  = false,              // _SET_DELETED
        [12]  = true,               // _SET_CANCEL
        [13]  = false,              // _SET_DEBUG
        [14]  = 50m,                // _SET_TYPEAHEAD
        [15]  = "W/N,N/W,N,N,N/W",  // _SET_COLOR
        [16]  = 1m,                 // _SET_CURSOR, SC_NORMAL
        [17]  = true,               // _SET_CONSOLE
        [18]  = false,              // _SET_ALTERNATE
        [19]  = "",                 // _SET_ALTFILE
        [20]  = "SCREEN",           // _SET_DEVICE
        [21]  = false,              // _SET_EXTRA
        [22]  = "",                 // _SET_EXTRAFILE
        [23]  = false,              // _SET_PRINTER
        [24]  = "PRN",              // _SET_PRINTFILE
        [25]  = 0m,                 // _SET_MARGIN
        [26]  = false,              // _SET_BELL
        [27]  = false,              // _SET_CONFIRM
        [28]  = true,               // _SET_ESCAPE
        [29]  = false,              // _SET_INSERT
        [30]  = false,              // _SET_EXIT
        [31]  = true,               // _SET_INTENSITY
        [32]  = true,               // _SET_SCOREBOARD
        [33]  = false,              // _SET_DELIMITERS
        [34]  = "::",               // _SET_DELIMCHARS
        [35]  = false,              // _SET_WRAP
        [36]  = 0m,                 // _SET_MESSAGE
        [37]  = false,              // _SET_MCENTER
        [38]  = true,               // _SET_SCROLLBREAK
        [39]  = 128m,               // _SET_EVENTMASK, INKEY_KEYBOARD
        [40]  = 0m,                 // _SET_VIDEOMODE
        [41]  = 64m,                // _SET_MBLOCKSIZE
        [42]  = "",                 // _SET_MFILEEXT
        [43]  = false,              // _SET_STRICTREAD
        [44]  = true,               // _SET_OPTIMIZE
        [45]  = true,               // _SET_AUTOPEN
        [46]  = 0m,                 // _SET_AUTORDER
        [47]  = 0m,                 // _SET_AUTOSHARE
        [48]  = false,              // HbRuntime's own: SET CENTURY
        [100] = "EN",               // _SET_LANGUAGE
        [101] = true,               // _SET_IDLEREPEAT
        [102] = 0m,                 // _SET_FILECASE, mixed
        [103] = 0m,                 // _SET_DIRCASE, mixed
        [104] = "\\",               // _SET_DIRSEPARATOR
        [105] = true,               // _SET_EOF
        [106] = true,               // _SET_HARDCOMMIT
        [107] = false,              // _SET_FORCEOPT
        [108] = 0m,                 // _SET_DBFLOCKSCHEME
        [109] = true,               // _SET_DEFEXTENSIONS
        [110] = "\r\n",             // _SET_EOL
        [111] = false,              // _SET_TRIMFILENAME
        [112] = "hb_out.log",       // _SET_HBOUTLOG
        [113] = "",                 // _SET_HBOUTLOGINFO
        [114] = "EN",               // _SET_CODEPAGE
        [115] = "",                 // _SET_OSCODEPAGE
        [116] = "hh:mm:ss.fff",     // _SET_TIMEFORMAT
        [117] = "",                 // _SET_DBCODEPAGE
    };

    public static dynamic Set(decimal nSet, dynamic newVal = null)
    {
        int key = (int)nSet;
        if (!s_sets.TryGetValue(key, out dynamic prev))
            return null;                        // not a SET Harbour has
        if (newVal is not null)
        {
            object xNew = newVal;
            if (prev is bool)
                s_sets[key] = AsOnOff(xNew, (bool) prev);
            else if (prev is string)
            {
                if (xNew is string cNew)
                    s_sets[key] = cNew;
            }
            else if (IsNumeric(xNew))
                s_sets[key] = Convert.ToDecimal(xNew, INV);
            // A date format turns century on when its (first) year has
            // four digits, off otherwise (vm/set.c)
            if (key == (int)_SET_DATEFORMAT && xNew is string cFormat)
            {
                var m = System.Text.RegularExpressions.Regex.Match(cFormat, "[Yy]+");
                s_sets[(int)_SET_CENTURY] = m.Success && m.Length >= 4;
            }
        }
        return prev;
    }

    // set_logical() (vm/set.c): a logical as it is; a string starting "ON"
    // or "OFF", in any case, as .T. / .F. — the #command rules for
    // SET ... ON / OFF hand Set() the keyword as a string — and anything
    // else leaves the old value.
    static bool AsOnOff(dynamic val, bool fallback = false)
    {
        if (val is bool b)
            return b;
        if (val is string s)
        {
            if (s.StartsWith("ON", StringComparison.OrdinalIgnoreCase))
                return true;
            if (s.StartsWith("OFF", StringComparison.OrdinalIgnoreCase))
                return false;
        }
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

    // ---- Binary conversions (rtl/binnum.c) ----
    // A Harbour string is bytes, one char per byte here. Little-endian.
    // A string shorter than the value reads as zero-padded (Harbour reads
    // on into the string's terminating NUL); anything but a string is 0.

    static long BinLE(string s, int nBytes)
    {
        long v = 0;
        if (s != null)
            for (int i = Math.Min(s.Length, nBytes) - 1; i >= 0; i--)
                v = (v << 8) | (byte)s[i];
        return v;
    }

    // Bin2I( <c> ): 2 bytes, signed
    public static decimal Bin2I(string s) => (short)BinLE(s, 2);

    // Bin2W( <c> ): 2 bytes, unsigned
    public static decimal Bin2W(string s) => (ushort)BinLE(s, 2);

    // Bin2L( <c> ): 4 bytes, signed
    public static decimal Bin2L(string s) => (int)BinLE(s, 4);

    static string LEBytes(long v, int nBytes)
    {
        var b = new char[nBytes];
        for (int i = 0; i < nBytes; i++, v >>= 8)
            b[i] = (char)(v & 0xFF);
        return new string(b);
    }

    // I2Bin( <n> ) / L2Bin( <n> ): n truncated to 16 / 32 bits
    public static string I2Bin(decimal n) => LEBytes((short)(long)Math.Truncate(n), 2);
    public static string L2Bin(decimal n) => LEBytes((int)(long)Math.Truncate(n), 4);

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
