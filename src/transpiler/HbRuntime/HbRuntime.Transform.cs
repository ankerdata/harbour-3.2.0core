using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

// Transform() and the picture formatting behind it (rtl/transfrm.c).
// One part of HbRuntime; HbRuntime.cs says what the whole is.

public static partial class HbRuntime
{
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
}
