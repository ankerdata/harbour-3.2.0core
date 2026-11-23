// HbCt — the hbct contrib library (contrib/hbct/hbct.hbx) for
// transpiled Harbour code. The emitter writes `HbCt.<name>(...)` for
// every name that .hbx lists. Real implementations go here; HbCt.Stubs.cs
// holds NotImplementedException stubs for the rest the application calls
// (scripts/gen_library_stubs.py in easipos-transpiled).

public static partial class HbCt
{
    // Ceiling( <nNumber> ) (ctmath2.c): the smallest whole number not less
    // than nNumber, with no decimals, as C's ceil() and hb_retnlen( d, 0, 0 )
    // give it. EasiPOS asks how many tabs a list of filters fills.
    public static decimal Ceiling(decimal nNumber) => decimal.Ceiling(nNumber);

    // DoY( [<dDate>] ) (dattime2.c): the day of the year, 1 to 366, of
    // dDate or of today; 0 for an empty date.
    public static long DoY(DateOnly? dDate = null)
    {
        DateOnly d = dDate ?? DateOnly.FromDateTime(DateTime.Today);
        return d == default ? 0 : d.DayOfYear;
    }

    // IsLeap( [<dDate>] ) (dattime2.c): whether the year of dDate, or of
    // today, is a leap year; false for an empty date.
    public static bool IsLeap(DateOnly? dDate = null)
    {
        DateOnly d = dDate ?? DateOnly.FromDateTime(DateTime.Today);
        return d != default && DateTime.IsLeapYear(d.Year);
    }
}
