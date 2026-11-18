using System;
using System.Collections.Generic;
using System.Text;

// Text and bytes: what a Harbour string is here.
// One part of HbRuntime; HbRuntime.cs says what the whole is.
//
// A Harbour string is bytes, and the program runs in the UTF8EX code page:
// its text is UTF-8, Len() and SubStr() count characters, and the hb_B*
// functions count bytes. A transpiled string is text, a char to a
// character, so the text functions are .NET's own and a string reaches a
// connector, JSON or SQLite as it is.
//
// What a string holds besides text (a printer's control byte, encrypted
// data, a bitmap) is bytes that are not UTF-8. Each such byte is carried
// as one char of its own, U+F780 to U+F7FF for the bytes 0x80 to 0xFF,
// and written back as that byte, so any sequence of bytes survives being
// a string. Harbour counts such a byte as one character too. HbBytes is
// the one place that knows this; every function that reads or writes
// bytes (files, sockets, serial ports, the codecs, the hb_B* functions)
// goes through it.
//
// Where the two differ, by ruling: a character outside the basic plane is
// one character to Harbour and two chars here; and the two halves of a
// character cut apart by a byte function and joined again are one
// character to Harbour and two chars here, though the bytes written are
// the same.

/// <summary>
/// A Harbour string as bytes and bytes as a Harbour string: UTF-8, with a
/// byte that is not UTF-8 carried as a char of its own.
/// </summary>
public static class HbBytes
{
    const int Escape = 0xF700;

    // The char that carries a byte that is not UTF-8 text
    public static bool IsByte(char c) => c >= 0xF780 && c <= 0xF7FF;

    // One byte as a string: itself below 0x80, its carrying char above
    public static char Char(int b) => (char) (b < 0x80 ? b : Escape + b);

    static bool IsAscii(string s)
    {
        foreach (char c in s)
            if (c >= 0x80)
                return false;
        return true;
    }

    // The number of bytes the string is
    public static int Count(string? s)
    {
        if (string.IsNullOrEmpty(s))
            return 0;
        int n = 0;
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (c < 0x80 || IsByte(c))
                n++;
            else if (c < 0x800)
                n += 2;
            else if (char.IsHighSurrogate(c) && i + 1 < s.Length && char.IsLowSurrogate(s[i + 1]))
            {
                n += 4;
                i++;
            }
            else
                n += 3;
        }
        return n;
    }

    // The string's bytes
    public static byte[] Of(string? s)
    {
        if (string.IsNullOrEmpty(s))
            return Array.Empty<byte>();
        var b = new byte[Count(s)];
        int n = 0;
        for (int i = 0; i < s.Length; i++)
        {
            int c = s[i];
            if (c < 0x80)
                b[n++] = (byte) c;
            else if (IsByte((char) c))
                b[n++] = (byte) (c - Escape);
            else if (c < 0x800)
            {
                b[n++] = (byte) (0xC0 | (c >> 6));
                b[n++] = (byte) (0x80 | (c & 0x3F));
            }
            else
            {
                if (char.IsHighSurrogate((char) c) && i + 1 < s.Length && char.IsLowSurrogate(s[i + 1]))
                {
                    c = char.ConvertToUtf32((char) c, s[++i]);
                    b[n++] = (byte) (0xF0 | (c >> 18));
                    b[n++] = (byte) (0x80 | ((c >> 12) & 0x3F));
                }
                else
                {
                    // half of a surrogate pair is no character: U+FFFD, as .NET writes it
                    if (char.IsSurrogate((char) c))
                        c = 0xFFFD;
                    b[n++] = (byte) (0xE0 | (c >> 12));
                }
                b[n++] = (byte) (0x80 | ((c >> 6) & 0x3F));
                b[n++] = (byte) (0x80 | (c & 0x3F));
            }
        }
        return b;
    }

    public static string Text(byte[] b) => Text(b, 0, b.Length);

    // Bytes as a string: UTF-8 where they are UTF-8, a carrying char for
    // each byte that is not. A sequence that would decode to a carrying
    // char is not taken as text, so that what was read is what is written.
    public static string Text(byte[] b, int iStart, int iCount)
    {
        var sb = new StringBuilder(iCount);
        int iEnd = iStart + iCount;
        for (int i = iStart; i < iEnd; i++)
        {
            int c = b[i];
            if (c < 0x80)
            {
                sb.Append((char) c);
                continue;
            }
            int iMore = c >= 0xC2 && c <= 0xDF ? 1 : c >= 0xE0 && c <= 0xEF ? 2 : c >= 0xF0 && c <= 0xF4 ? 3 : 0;
            int iCode = iMore == 1 ? c & 0x1F : iMore == 2 ? c & 0x0F : c & 0x07;
            bool fText = iMore > 0 && i + iMore < iEnd;
            for (int j = 1; fText && j <= iMore; j++)
            {
                if ((b[i + j] & 0xC0) != 0x80)
                    fText = false;
                else
                    iCode = (iCode << 6) | (b[i + j] & 0x3F);
            }
            // not the shortest form, half of a surrogate pair, past the last
            // code point, or a carrying char: bytes, not text
            if (fText && (iMore == 2 && iCode < 0x800 || iMore == 3 && iCode < 0x10000 || iCode > 0x10FFFF ||
                          iCode >= 0xD800 && iCode <= 0xDFFF || iCode >= 0xF780 && iCode <= 0xF7FF))
                fText = false;
            if (!fText)
            {
                sb.Append(Char(c));
                continue;
            }
            if (iCode < 0x10000)
                sb.Append((char) iCode);
            else
                sb.Append(char.ConvertFromUtf32(iCode));
            i += iMore;
        }
        return sb.ToString();
    }

    // The bytes from iStart (0 based), iCount of them, as a string. Text
    // that is all ASCII is a byte to a char and is cut as it stands.
    public static string Slice(string s, int iStart, int iCount)
    {
        if (iCount <= 0)
            return "";
        if (IsAscii(s))
            return iStart >= s.Length ? "" : s.Substring(iStart, Math.Min(iCount, s.Length - iStart));
        byte[] b = Of(s);
        return iStart >= b.Length ? "" : Text(b, iStart, Math.Min(iCount, b.Length - iStart));
    }

    // ---- Code pages ----
    // Harbour's id for the program's own code page and for the ones
    // EasiPOS translates to and from: each a Windows code page here, read
    // into a table of its 256 characters. PC1125 is not one .NET has, and
    // is PC866 with the Ukrainian letters where they differ.

    static readonly Dictionary<string, int> s_aCodePages = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ENWIN"] = 1252, ["DEWIN"] = 1252, ["PTISO"] = 28591, ["SVISO"] = 28605, ["CSWIN"] = 1250, ["PLWIN"] = 1250,
        ["PLISO"] = 28592, ["PL852"] = 852, ["RU1251"] = 1251, ["RU866"] = 866, ["UA1125"] = 866, ["ELWIN"] = 1253,
        ["ELISO"] = 28597, ["EL737"] = 737, ["TRWIN"] = 1254, ["TR857"] = 857, ["HEWIN"] = 1255,
        ["HE862"] = 862, ["LTWIN"] = 1257, ["LT775"] = 775, ["IS861"] = 861, ["PT860"] = 860,
        ["DK865"] = 865, ["DE850"] = 850, ["DE858"] = 858, ["EN"] = 437,
    };

    sealed class CodePageTable
    {
        public readonly char[] aChars = new char[256];
        public readonly Dictionary<char, byte> aBytes = new();
    }

    static readonly Dictionary<string, CodePageTable> s_aTables = new(StringComparer.OrdinalIgnoreCase);

    public static bool IsUtf8(string? cCodePage) =>
        cCodePage == null || cCodePage.Equals("UTF8EX", StringComparison.OrdinalIgnoreCase) ||
        cCodePage.Equals("UTF8", StringComparison.OrdinalIgnoreCase);

    // The table of a Harbour code page id; an id that is not listed is an
    // error, as selecting a code page that was not linked is
    static CodePageTable CodePage(string cCodePage)
    {
        lock (s_aTables)
        {
            if (!s_aTables.TryGetValue(cCodePage, out CodePageTable? oTable))
            {
                if (!s_aCodePages.TryGetValue(cCodePage, out int iPage))
                    throw new ArgumentException("code page " + cCodePage + " is not one HbRuntime knows");
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                var aAll = new byte[256];
                for (int i = 0; i < 256; i++)
                    aAll[i] = (byte) i;
                oTable = new CodePageTable();
                Encoding.GetEncoding(iPage).GetChars(aAll, 0, 256, oTable.aChars, 0);
                if (cCodePage.Equals("UA1125", StringComparison.OrdinalIgnoreCase))
                    "\u0490\u0491\u0404\u0454\u0406\u0456\u0407\u0457\u00B7".CopyTo(0, oTable.aChars, 0xF2, 9);
                for (int i = 255; i >= 0; i--)
                    oTable.aBytes[oTable.aChars[i]] = (byte) i;
                s_aTables[cCodePage] = oTable;
            }
            return oTable;
        }
    }

    // Bytes of a code page as text
    static string FromCodePage(string cCodePage, byte[] b)
    {
        char[] aChars = CodePage(cCodePage).aChars;
        var c = new char[b.Length];
        for (int i = 0; i < b.Length; i++)
            c[i] = aChars[b[i]];
        return new string(c);
    }

    // Text as bytes of a code page: '?' for a character it has not
    static byte[] ToCodePage(string cCodePage, string s)
    {
        Dictionary<char, byte> aBytes = CodePage(cCodePage).aBytes;
        var b = new byte[s.Length];
        for (int i = 0; i < s.Length; i++)
            b[i] = aBytes.TryGetValue(s[i], out byte bChar) ? bChar : (byte) '?';
        return b;
    }

    // The string's text with each byte that is not text as '?', which is
    // what a translation from UTF-8 makes of it
    static string TextOnly(string s)
    {
        StringBuilder? sb = null;
        for (int i = 0; i < s.Length; i++)
            if (IsByte(s[i]))
                (sb ??= new StringBuilder(s))[i] = '?';
        return sb?.ToString() ?? s;
    }

    // hb_Translate(): the string's bytes read as cFrom's, written as cTo's
    public static string Translate(string? s, string? cFrom, string? cTo)
    {
        if (string.IsNullOrEmpty(s))
            return "";
        bool fFrom = IsUtf8(cFrom), fTo = IsUtf8(cTo);
        if (fFrom && fTo)
            return s;
        string cText = fFrom ? TextOnly(s) : FromCodePage(cFrom!, Of(s));
        return fTo ? cText : Text(ToCodePage(cTo!, cText));
    }
}

public static partial class HbRuntime
{
    // ---- The byte functions (rtl/strfunc.c's hb_B*) ----

    // hb_BLen( <c> ): the number of bytes
    // A string literal written under `#pragma TEXTHIDDEN=1`, which the
    // emitter writes as its bytes XORed along their length so the text is
    // in no string table: the literal
    public static string HbHiddenText(byte[] aHidden)
    {
        byte[] a = new byte[aHidden.Length];
        for (int i = 0; i < a.Length; i++)
            a[i] = (byte)(aHidden[i] ^ (byte)(0xA7 + i * 31));
        return HbBytes.Text(a);
    }

    public static long hb_BLen(string? s) => HbBytes.Count(s);

    // hb_BLeft( <c>, <n> ) / hb_BRight( <c>, <n> ): the first / last n bytes
    public static string hb_BLeft(string? s, decimal n) =>
        s == null || n < 1 ? "" : HbBytes.Slice(s, 0, (int) Math.Min(n, int.MaxValue));

    public static string hb_BRight(string? s, decimal n)
    {
        if (s == null || n < 1)
            return "";
        int iLen = HbBytes.Count(s);
        return n >= iLen ? s : HbBytes.Slice(s, iLen - (int) n, (int) n);
    }

    // hb_BSubStr( <c>, <nStart>[, <nCount>] ): SubStr() by bytes. A start
    // of 0 is 1, a negative one counts back from the end (rtl/substr.c).
    public static string hb_BSubStr(string? s, decimal nStart, decimal? nCount = null)
    {
        if (string.IsNullOrEmpty(s))
            return "";
        int iLen = HbBytes.Count(s);
        long iStart = (long) Math.Truncate(nStart);
        if (iStart > 0)
            iStart--;
        else if (iStart < 0)
            iStart = Math.Max(iLen + iStart, 0);
        if (iStart >= iLen)
            return "";
        long iCount = nCount == null ? iLen - iStart : Math.Min((long) Math.Truncate(nCount.Value), iLen - iStart);
        return iCount <= 0 ? "" : iStart == 0 && iCount == iLen ? s : HbBytes.Slice(s, (int) iStart, (int) iCount);
    }

    // hb_BAt( <cSearch>, <c>, [<nStart>], [<nEnd>] ): the byte position of
    // cSearch in c, looked for from byte nStart to byte nEnd, or 0
    public static long hb_BAt(string? cSearch, string? s, decimal nStart = 1, decimal? nEnd = null)
    {
        if (string.IsNullOrEmpty(cSearch) || string.IsNullOrEmpty(s))
            return 0;
        byte[] b = HbBytes.Of(s);
        long iStart = Math.Max((long) Math.Truncate(nStart), 1) - 1;
        long iEnd = nEnd == null ? b.Length : Math.Min((long) Math.Truncate(nEnd.Value), b.Length);
        if (iStart >= iEnd)
            return 0;
        int iAt = new ReadOnlySpan<byte>(b, (int) iStart, (int) (iEnd - iStart)).IndexOf(HbBytes.Of(cSearch));
        return iAt < 0 ? 0 : iAt + iStart + 1;
    }

    // hb_BChar( <n> ): the byte n, its low eight bits
    public static string hb_BChar(decimal n) =>
        HbBytes.Char((int) ((long) Math.Truncate(n % 65536) & 0xFF)).ToString();

    // hb_BCode( <c> ): the string's first byte, 0 for an empty one
    public static long hb_BCode(string? s)
    {
        if (string.IsNullOrEmpty(s))
            return 0;
        char c = s[0];
        return c < 0x80 ? c : HbBytes.Of(s.Substring(0, char.IsHighSurrogate(c) && s.Length > 1 ? 2 : 1))[0];
    }

    // ---- UTF-8 and code pages (rtl/cdpapi.c, cdpapihb.c) ----

    // hb_utf8Chr( <n> ): the character of a code point
    public static string hb_utf8Chr(decimal n)
    {
        long iCode = (long) Math.Truncate(n);
        return iCode < 0 || iCode > 0x10FFFF || iCode >= 0xD800 && iCode <= 0xDFFF
            ? "" : char.ConvertFromUtf32((int) iCode);
    }

    // hb_StrIsUTF8( <c> ): the string is UTF-8 and has a character that
    // takes more than one byte. Plain ASCII is not, nor is an empty string.
    public static bool hb_StrIsUTF8(string? s)
    {
        bool fWide = false;
        foreach (char c in s ?? "")
        {
            if (HbBytes.IsByte(c))
                return false;
            if (c >= 0x80)
                fWide = true;
        }
        return fWide;
    }

    // hb_cdpSelect( [<cNew>] ): the program's code page, which is UTF8EX
    // and cannot be another: a string here is text, and its characters are
    // not counted by another code page's rules.
    public static string hb_cdpSelect(string? cNew = null)
    {
        if (cNew != null && !HbBytes.IsUtf8(cNew))
            throw new NotSupportedException("hb_cdpSelect( \"" + cNew + "\" ): the transpiled program runs in UTF8EX");
        return "UTF8EX";
    }

    // hb_cdpIsUTF8( [<cCodePage>] )
    public static bool hb_cdpIsUTF8(string? cCodePage = null) => HbBytes.IsUtf8(cCodePage);

    // hb_Translate( <c>, [<cFrom>], [<cTo>] ): the string in another code
    // page. A character the other code page has not is '?', and so is a
    // byte that was not text.
    public static string hb_Translate(string? s, string? cFrom = null, string? cTo = null) =>
        HbBytes.Translate(s, cFrom, cTo);

    // hb_StrToUTF8( <c>, [<cFrom>] ) / hb_UTF8ToStr( <cUTF8>, [<cTo>] )
    public static string hb_StrToUTF8(string? s, string? cFrom = null) => HbBytes.Translate(s, cFrom, null);

    public static string hb_UTF8ToStr(string? s, string? cTo = null) => HbBytes.Translate(s, null, cTo);
}
