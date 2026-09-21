using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

// Dates and timestamps: the date functions, the timestamp functions of
// rtl/dateshb.c, rtl/dates.c and common/hbdate.c, Seconds(), and Year() /
// Month() / Day() / DoW().
// One part of HbRuntime; HbRuntime.cs says what the whole is.

public static partial class HbRuntime
{
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

    // ---- Time ----

    // Seconds(): seconds since midnight, to the millisecond
    // (common/hbdate.c, hb_dateSeconds)
    public static decimal Seconds() =>
        DateTime.Now.TimeOfDay.Ticks / TimeSpan.TicksPerMillisecond / 1000m;

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
}
