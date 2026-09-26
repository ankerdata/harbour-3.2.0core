using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

// Numbers: Str(), Val(), Round() and the other numeric functions, the
// bit operations, and the binary conversions of rtl/binnum.c.
// One part of HbRuntime; HbRuntime.cs says what the whole is.

public static partial class HbRuntime
{
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
    // an argument error. The result is a long, declared so, which gives the
    // callers a type (hbfuncs.tab NUMERIC) and still compares with an
    // int/long/decimal mask (`hb_bitAnd(nFlags, MASK) == MASK`).
    static long BitOperand(object x, string cFunc) => x switch
    {
        long l => l,
        int i => i,
        decimal d => (long)Math.Truncate(d),
        double d => (long)Math.Truncate(d),
        _ when IsNumeric(x) => (long)Math.Truncate(Convert.ToDecimal(x, INV)),
        _ => throw new ArgumentException("Argument error (" + cFunc + ")")
    };

    public static long hb_bitAnd(params dynamic[] args)
    {
        long r = ~0L;
        foreach (var a in args) r &= BitOperand((object)a, "HB_BITAND");
        return r;
    }
    public static long hb_bitOr(params dynamic[] args)
    {
        long r = 0L;
        foreach (var a in args) r |= BitOperand((object)a, "HB_BITOR");
        return r;
    }
    public static long hb_bitXor(params dynamic[] args)
    {
        long r = 0L;
        foreach (var a in args) r ^= BitOperand((object)a, "HB_BITXOR");
        return r;
    }

    // hb_bitNot( <n> ): every bit inverted
    public static long hb_bitNot(dynamic n) => ~BitOperand((object)n, "HB_BITNOT");

    // hb_bitShift( <n>, <nBits> ): left for a positive count, right
    // (arithmetic, the sign kept) for a negative one
    public static long hb_bitShift(dynamic n, dynamic nBits)
    {
        long l = BitOperand((object)n, "HB_BITSHIFT");
        long b = BitOperand((object)nBits, "HB_BITSHIFT");
        return b < 0 ? l >> (int)(-b) : l << (int)b;
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
}
