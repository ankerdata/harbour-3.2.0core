using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

// Family 13: encodings and codecs — JSON, base64, MD5, zlib, UTF-8 and
// the regular expressions, as rtl/hbjson.c, rtl/base64.c, rtl/hbmd5.c,
// contrib-free src/rtl/hbzlib.c and rtl/hbregex.c have them.
//
// This is what EasiPOS's protocols are made of: every message to EasiWin
// and to the BOH server is hb_jsonEncode()d and read back with
// hb_jsonDecode(), the SQLite Server's traffic is hb_ZCompress()ed, and
// bitmaps and card payloads travel base64. So the texts matter to the
// byte, and own_codecs.prg pins them against Harbour's own output.
//
// A Harbour string is bytes, held here one byte per char (Latin-1), so
// the encoder passes the high half through as it stands and the caller
// converts the finished message with hb_StrToUTF8(), exactly as
// socketloop.prg does.
public static partial class HbRuntime
{
    // ---- JSON ----------------------------------------------------------

    // hb_jsonEncode( <x>, [<lHuman>|<nIndent>] ): .T. indents by two
    // spaces (INDENT_SIZE), a number by that many, .F. or nothing not at
    // all. The end of line is SET EOL, "\r\n" here as on Harbour's
    // Windows build.
    public static string hb_jsonEncode(object? xValue, object? xIndent = null)
    {
        int nIndent = xIndent is bool lHuman
            ? (lHuman ? JsonIndentSize : 0)
            : xIndent != null && IsNumeric(xIndent) ? (int) Convert.ToInt64(xIndent, INV) : 0;
        var sb = new StringBuilder();
        JsonEncode(xValue, sb, 0, false, nIndent, new List<object>());
        if (nIndent != 0)
            sb.Append(JsonEol());   // hb_jsonEncodeCP() ends an indented text with one
        return sb.ToString();
    }

    const int JsonIndentSize = 2;

    static void JsonEncode(object? x, StringBuilder sb, int nLevel, bool fEol,
                           int nIndent, List<object> aPath)
    {
        // A structure that contains itself is "null", as deep as the path
        // it is found in (hbjson.c walks pCtx->pId the same way).
        if (x is object[] or System.Collections.IDictionary && JsonSize(x) > 0)
        {
            if (aPath.Exists(xSeen => ReferenceEquals(xSeen, x)))
            {
                if (nIndent != 0)
                    JsonIndent(sb, nLevel, nIndent);
                sb.Append("null");
                return;
            }
            aPath.Add(x!);
        }

        // The key wrote ": " and the value is a structure of its own: the
        // space goes again and the structure starts on the next line.
        if (fEol)
        {
            sb.Length--;
            sb.Append(JsonEol());
        }

        switch (x)
        {
            case null:
                sb.Append("null");
                break;
            case string cText:
                JsonEncodeString(cText, sb);
                break;
            case bool lValue:
                sb.Append(lValue ? "true" : "false");
                break;
            case DateOnly dDate:
                sb.Append('"').Append(DToS(dDate)).Append('"');
                break;
            case DateTime tStamp:
                sb.Append('"').Append(tStamp.ToString("yyyyMMddHHmmssfff", INV)).Append('"');
                break;
            case long or int or short or byte or sbyte or uint or ulong or ushort:
                sb.Append(Convert.ToInt64(x, INV).ToString(INV));
                break;
            case decimal or double or float:
                // Harbour prints "%.*f" with the value's own decimals. A
                // C# decimal carries its scale, so ToString gives exactly
                // that: 2.50 stays "2.50", 42 is "42".
                sb.Append(Convert.ToDecimal(x, INV).ToString(INV));
                break;
            case object[] aValues:
                JsonEncodeArray(aValues, sb, nLevel, nIndent, aPath);
                break;
            case System.Collections.IDictionary hValues:
                JsonEncodeHash(hValues, sb, nLevel, nIndent, aPath);
                break;
            default:
                // An object, a codeblock, a pointer: hbjson.c has no form
                // for them and writes null.
                sb.Append("null");
                break;
        }

        if (aPath.Count > 0 && ReferenceEquals(aPath[^1], x))
            aPath.RemoveAt(aPath.Count - 1);
    }

    static string JsonEol() => Set(_SET_EOL) as string ?? "\r\n";

    static int JsonSize(object? x) => x switch
    {
        object[] a => a.Length,
        System.Collections.IDictionary d => d.Count,
        _ => 0
    };

    static void JsonIndent(StringBuilder sb, int nLevel, int nIndent)
    {
        if (nLevel > 0)
            sb.Append(nIndent > 0 ? ' ' : '\t', nLevel * (nIndent > 0 ? nIndent : 1));
    }

    // A byte below a space is escaped, as are the quote and the backslash;
    // everything else — the high half included — goes as it stands.
    static void JsonEncodeString(string cText, StringBuilder sb)
    {
        sb.Append('"');
        foreach (char ch in cText)
            switch (ch)
            {
                case '\\': sb.Append("\\\\"); break;
                case '"':  sb.Append("\\\""); break;
                case '\b': sb.Append("\\b"); break;
                case '\f': sb.Append("\\f"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (ch < ' ')
                        sb.Append("\\u00").Append(((int) ch).ToString("X2", INV));
                    else
                        sb.Append(ch);
                    break;
            }
        sb.Append('"');
    }

    static void JsonEncodeArray(object[] aValues, StringBuilder sb, int nLevel,
                                int nIndent, List<object> aPath)
    {
        if (aValues.Length == 0)
        {
            sb.Append("[]");
            return;
        }
        if (nIndent != 0)
            JsonIndent(sb, nLevel, nIndent);
        sb.Append('[');
        for (int i = 0; i < aValues.Length; i++)
        {
            if (i > 0)
                sb.Append(',');
            if (nIndent != 0)
            {
                sb.Append(JsonEol());
                if (JsonSize(aValues[i]) == 0)
                    JsonIndent(sb, nLevel + 1, nIndent);
            }
            JsonEncode(aValues[i], sb, nLevel + 1, false, nIndent, aPath);
        }
        if (nIndent != 0)
        {
            sb.Append(JsonEol());
            JsonIndent(sb, nLevel, nIndent);
        }
        sb.Append(']');
    }

    static void JsonEncodeHash(System.Collections.IDictionary hValues, StringBuilder sb,
                               int nLevel, int nIndent, List<object> aPath)
    {
        if (hValues.Count == 0)
        {
            sb.Append("{}");
            return;
        }
        if (nIndent != 0)
            JsonIndent(sb, nLevel, nIndent);
        sb.Append('{');
        bool fFirst = true;
        foreach (System.Collections.DictionaryEntry e in hValues)
        {
            // A key that is not a string has no JSON form: hbjson.c skips
            // the pair, comma and all. EasiPOS's messages are string-keyed.
            if (e.Key is not string cKey)
                continue;
            if (!fFirst)
                sb.Append(',');
            fFirst = false;
            if (nIndent != 0)
            {
                sb.Append(JsonEol());
                JsonIndent(sb, nLevel + 1, nIndent);
            }
            JsonEncodeString(cKey, sb);
            bool fEol = false;
            if (nIndent != 0)
            {
                sb.Append(": ");
                fEol = JsonSize(e.Value) > 0;
            }
            else
                sb.Append(':');
            JsonEncode(e.Value, sb, nLevel + 1, fEol, nIndent, aPath);
        }
        if (nIndent != 0)
        {
            sb.Append(JsonEol());
            JsonIndent(sb, nLevel, nIndent);
        }
        sb.Append('}');
    }

    // hb_jsonDecode( <cJSON> ): the value, or NIL when it does not parse.
    // Harbour reads ONE value and stops, so text after it is ignored (the
    // by-ref form returns how much was read); it is "much less strict to
    // number format than JSON syntax definition" (hbjson.c), and so is
    // this: a bare 1.2.3 reads as 1.2.
    public static dynamic? hb_jsonDecode(string? cJson)
    {
        if (cJson == null)
            return null;
        int i = 0;
        object? xValue = JsonParse(cJson, ref i);
        return xValue is JsonFailure ? null : xValue;
    }

    sealed class JsonFailure { public static readonly JsonFailure Value = new(); }

    static void JsonSkipWs(string c, ref int i)
    {
        while (i < c.Length && (c[i] == ' ' || c[i] == '\t' || c[i] == '\r' || c[i] == '\n'))
            i++;
    }

    static object? JsonParse(string c, ref int i)
    {
        JsonSkipWs(c, ref i);
        if (i >= c.Length)
            return JsonFailure.Value;
        switch (c[i])
        {
            case '{': return JsonParseHash(c, ref i);
            case '[': return JsonParseArray(c, ref i);
            case '"': return JsonParseString(c, ref i);
            case 't': return JsonWord(c, ref i, "true") ? true : JsonFailure.Value;
            case 'f': return JsonWord(c, ref i, "false") ? false : (object) JsonFailure.Value;
            case 'n': return JsonWord(c, ref i, "null") ? null : (object) JsonFailure.Value;
            default:
                if (c[i] == '-' || (c[i] >= '0' && c[i] <= '9'))
                    return JsonParseNumber(c, ref i);
                return JsonFailure.Value;
        }
    }

    static bool JsonWord(string c, ref int i, string cWord)
    {
        if (i + cWord.Length > c.Length || string.CompareOrdinal(c, i, cWord, 0, cWord.Length) != 0)
            return false;
        i += cWord.Length;
        return true;
    }

    static object JsonParseNumber(string c, ref int i)
    {
        int nStart = i;
        bool fNeg = c[i] == '-';
        if (fNeg)
            i++;
        long nInt = 0;
        while (i < c.Length && c[i] >= '0' && c[i] <= '9')
            nInt = nInt * 10 + (c[i++] - '0');
        if (i == nStart || (fNeg && i == nStart + 1))
            return JsonFailure.Value;
        int nDec = 0;
        decimal nFrac = 0;
        if (i < c.Length && c[i] == '.')
        {
            i++;
            decimal nMult = 1;
            while (i < c.Length && c[i] >= '0' && c[i] <= '9')
            {
                nMult /= 10;
                nFrac += (c[i++] - '0') * nMult;
                nDec++;
            }
        }
        // An exponent reads as Harbour's does: the value scaled, decimals kept.
        if (i < c.Length && (c[i] == 'e' || c[i] == 'E'))
        {
            int nSign = 1, nExp = 0;
            i++;
            if (i < c.Length && (c[i] == '+' || c[i] == '-'))
                nSign = c[i++] == '-' ? -1 : 1;
            while (i < c.Length && c[i] >= '0' && c[i] <= '9')
                nExp = nExp * 10 + (c[i++] - '0');
            double dValue = (nInt + (double) nFrac) * Math.Pow(10, nSign * nExp);
            return (decimal) (fNeg ? -dValue : dValue);
        }
        if (nDec == 0)
            return fNeg ? -nInt : nInt;
        decimal nValue = nInt + nFrac;
        return Math.Round(fNeg ? -nValue : nValue, nDec);
    }

    static object JsonParseString(string c, ref int i)
    {
        var sb = new StringBuilder();
        i++;   // the opening quote
        while (i < c.Length && c[i] != '"')
        {
            char ch = c[i];
            if (ch == '\\')
            {
                if (++i >= c.Length)
                    return JsonFailure.Value;
                switch (c[i])
                {
                    case '"': sb.Append('"'); break;
                    case '\\': sb.Append('\\'); break;
                    case '/': sb.Append('/'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'u':
                    {
                        if (i + 4 >= c.Length)
                            return JsonFailure.Value;
                        int nCode = 0;
                        for (int k = 0; k < 4; k++)
                        {
                            int nDigit = HexDigit(c[++i]);
                            if (nDigit < 0)
                                return JsonFailure.Value;
                            nCode = (nCode << 4) + nDigit;
                        }
                        // Harbour hands the code point to the VM codepage,
                        // one byte out of a single-byte one. A byte string
                        // here is Latin-1, so a code point it cannot hold
                        // is '?', as a codepage translation gives.
                        sb.Append(nCode <= 0xFF ? (char) nCode : '?');
                        break;
                    }
                    default:
                        return JsonFailure.Value;
                }
                i++;
            }
            else if (ch >= ' ')
            {
                sb.Append(ch);
                i++;
            }
            else
                return JsonFailure.Value;   // a raw control character
        }
        if (i >= c.Length)
            return JsonFailure.Value;
        i++;   // the closing quote
        return sb.ToString();
    }

    static int HexDigit(char ch) =>
        ch >= '0' && ch <= '9' ? ch - '0' :
        ch >= 'A' && ch <= 'F' ? ch - 'A' + 10 :
        ch >= 'a' && ch <= 'f' ? ch - 'a' + 10 : -1;

    static object JsonParseArray(string c, ref int i)
    {
        var aValues = new List<dynamic?>();
        i++;
        JsonSkipWs(c, ref i);
        if (i < c.Length && c[i] == ']')
        {
            i++;
            return System.Array.Empty<dynamic>();
        }
        while (i < c.Length)
        {
            object? xItem = JsonParse(c, ref i);
            if (xItem is JsonFailure)
                return JsonFailure.Value;
            aValues.Add(xItem);
            JsonSkipWs(c, ref i);
            if (i < c.Length && c[i] == ',')
            {
                i++;
                continue;
            }
            if (i < c.Length && c[i] == ']')
            {
                i++;
                return aValues.ToArray();
            }
            return JsonFailure.Value;
        }
        return JsonFailure.Value;
    }

    static object JsonParseHash(string c, ref int i)
    {
        // Harbour's decoder builds a string-keyed hash in the order the
        // pairs arrive, which is what the emitter's HASH is here.
        var hValues = new OrderedDictionary<string, dynamic>();
        i++;
        JsonSkipWs(c, ref i);
        if (i < c.Length && c[i] == '}')
        {
            i++;
            return hValues;
        }
        while (i < c.Length)
        {
            JsonSkipWs(c, ref i);
            if (i >= c.Length || c[i] != '"')
                return JsonFailure.Value;
            object xKey = JsonParseString(c, ref i);
            if (xKey is JsonFailure)
                return JsonFailure.Value;
            JsonSkipWs(c, ref i);
            if (i >= c.Length || c[i] != ':')
                return JsonFailure.Value;
            i++;
            object? xItem = JsonParse(c, ref i);
            if (xItem is JsonFailure)
                return JsonFailure.Value;
            hValues[(string) xKey] = xItem;
            JsonSkipWs(c, ref i);
            if (i < c.Length && c[i] == ',')
            {
                i++;
                continue;
            }
            if (i < c.Length && c[i] == '}')
            {
                i++;
                return hValues;
            }
            return JsonFailure.Value;
        }
        return JsonFailure.Value;
    }

    // ---- base64, MD5 ---------------------------------------------------

    // hb_base64Encode( <cData> ): the bytes of cData in base64 (rtl/base64.c)
    public static string hb_base64Encode(string? cData) =>
        cData == null ? "" : Convert.ToBase64String(s_byteCP.GetBytes(cData));

    // hb_MD5( <cData> ): the digest as 32 lower-case hex digits (rtl/hbmd5.c)
    public static string hb_MD5(string? cData) =>
        Convert.ToHexString(System.Security.Cryptography.MD5.HashData(
            s_byteCP.GetBytes(cData ?? ""))).ToLowerInvariant();

    // ---- zlib ----------------------------------------------------------
    //
    // hb_ZCompress() / hb_ZUncompress() carry the SQLite Server's and the
    // BOH server's messages, framed by a four-byte length. A zlib stream
    // (RFC 1950) is what both write, so .NET's ZLibStream reads Harbour's
    // and EasiSvr's Python zlib reads ours. The bytes of a given input may
    // differ between the two compressors — the deflate stream is not
    // specified to the byte — so a test compares a round trip, never the
    // compressed text.

    // hb_ZCompress( <cData> )
    public static string hb_ZCompress(string? cData, object? x2 = null, object? x3 = null)
    {
        if (cData == null)
            return "";
        using var msOut = new MemoryStream();
        using (var zs = new ZLibStream(msOut, CompressionLevel.Optimal, true))
        {
            byte[] aData = s_byteCP.GetBytes(cData);
            zs.Write(aData, 0, aData.Length);
        }
        return s_byteCP.GetString(msOut.GetBuffer(), 0, (int) msOut.Length);
    }

    // hb_ZUncompress( <cCompressed> ): Harbour takes the length of the
    // result as its second argument or works it out; .NET reads the stream
    // to its end, so the length is not needed and is accepted and ignored.
    public static string hb_ZUncompress(string? cData, object? x2 = null, object? x3 = null)
    {
        if (string.IsNullOrEmpty(cData))
            return "";
        using var msIn = new MemoryStream(s_byteCP.GetBytes(cData));
        using var zs = new ZLibStream(msIn, CompressionMode.Decompress);
        using var msOut = new MemoryStream();
        zs.CopyTo(msOut);
        return s_byteCP.GetString(msOut.GetBuffer(), 0, (int) msOut.Length);
    }

    // hb_ZLibVersion(): Harbour answers zlib's own version string. .NET
    // has no zlib of its own to name — the deflate code is part of the
    // runtime — so this names that instead of inventing a number. Ruled
    // divergent; EasiPOS only writes it to the trace.
    public static string hb_ZLibVersion() => "System.IO.Compression";

    // ---- UTF-8 ---------------------------------------------------------
    //
    // The only two conversions in the byte-string convention: a char is a
    // byte everywhere else, and these two turn that into UTF-8 and back.

    // hb_StrToUTF8( <cString> )
    public static string hb_StrToUTF8(string? cText) =>
        cText == null ? "" : s_byteCP.GetString(Encoding.UTF8.GetBytes(cText));

    // hb_UTF8ToStr( <cUTF8> ): a character the byte string cannot hold
    // becomes '?', as a codepage translation does.
    public static string hb_UTF8ToStr(string? cText)
    {
        if (cText == null)
            return "";
        var aBytes = s_byteCP.GetBytes(cText);
        return s_byteCP.GetString(
            Encoding.Convert(Encoding.UTF8, Encoding.Latin1, aBytes));
    }

    // ---- regular expressions -------------------------------------------
    //
    // Harbour's are PCRE; .NET's engine is its own, and the two agree on
    // the syntax EasiPOS uses — character classes, counts, groups. A
    // pattern that leans on a PCRE-only construct would be a divergence to
    // rule; the corpus has none.

    // A compiled pattern is a Harbour pointer (ValType "P"), as Harbour's is.
    public sealed class HbRegex
    {
        public readonly Regex re;
        public HbRegex(Regex re) => this.re = re;
    }

    // hb_regexComp( <cRegex>, [<lCaseSensitive>], [<lNewLine>] )
    public static HbRegex hb_regexComp(string cRegex, bool lCaseSensitive = true,
                                       bool lNewLine = false)
    {
        var opts = RegexOptions.None;
        if (!lCaseSensitive)
            opts |= RegexOptions.IgnoreCase;
        if (lNewLine)
            opts |= RegexOptions.Multiline;
        return new HbRegex(new Regex(cRegex, opts));
    }

    // hb_regex( <cRegex>|<pRegex>, <cString>, [<lCaseSensitive>], [<lNewLine>] ):
    // the whole match first, then one element per group, or an empty array
    // when it does not match (rtl/hbregex.c).
    public static dynamic[] hb_regex(object? xRegex, string cString,
                                     bool lCaseSensitive = true, bool lNewLine = false)
    {
        Regex re = xRegex switch
        {
            HbRegex pRegex => pRegex.re,
            string cRegex => hb_regexComp(cRegex, lCaseSensitive, lNewLine).re,
            _ => throw new ArgumentException("Argument error (HB_REGEX)")
        };
        Match m = re.Match(cString ?? "");
        if (!m.Success)
            return System.Array.Empty<dynamic>();
        var aResult = new dynamic[m.Groups.Count];
        for (int i = 0; i < m.Groups.Count; i++)
            aResult[i] = m.Groups[i].Success ? m.Groups[i].Value : "";
        return aResult;
    }
}
