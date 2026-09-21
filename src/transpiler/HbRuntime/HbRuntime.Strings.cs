using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

// Strings: the string functions of rtl/.
// One part of HbRuntime; HbRuntime.cs says what the whole is.

public static partial class HbRuntime
{
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
}
