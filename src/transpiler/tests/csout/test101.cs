// Test 101: date arithmetic and string ordering — both operators C#
// refuses (CS0019: DateOnly has no arithmetic, string has no `<`).
//
// `d1 - d2` is Harbour's day count, a number: it emits
// `(decimal)(d1.DayNumber - d2.DayNumber)` — decimal so a later `/`
// keeps float division (xzjobs' `Date() - oTicket:dIssueDate`, 14
// sites). `d + n`, `n + d`, `d - n` emit `d.AddDays( (int)( n ) )`,
// and `d += n` / `d -= n` become `d = d.AddDays( ... )` (fbrands'
// `dBusiness -= 1`). C# has no ordering operators for strings, so
// `<`, `<=`, `>`, `>=` on strings emit `HbRuntime.StrCmp( a, b ) <op>
// 0` and `=` stays `==` (postdr's `cTime <= "24:00"`, 12 sites).
// StrCmp is exact and ordinal: Harbour's SET EXACT OFF rule, under
// which "Lisbon" > "Lis" is false, is deliberately not reproduced
// (Alex, 2026-09-19), so the test runs under SET EXACT ON, as
// EasiPOS's startup.prg does.
// Operand types come from the emit-side probe: a local or parameter's
// declared type, a DATA member of the current class, a member of a
// typed receiver via its reftab row, a function's reftab return type
// (DockDate) or its hbfuncs.tab one (Time).
public class Voyage : IHbObject
{
    public DateOnly dDepart;
    public string cPort;

    public virtual Voyage New(DateOnly dDepart = default, string cPort = default)
    {
        this.dDepart = dDepart;
        this.cPort = cPort;
        return this;
    }

    public virtual DateOnly Arrival(decimal nDays = default)
    {
        return dDepart.AddDays((int)(nDays));
    }
}

public static partial class Program
{
    public static DateOnly DockDate()
    {
        return SToD("20260315");
    }

    public static void Main(string[] args)
    {
        DateOnly dStart = SToD("20260301");
        DateOnly dEnd = SToD("20260310");
        decimal nSpan = (decimal)(dEnd.DayNumber - dStart.DayNumber);
        Voyage oVoyage = new Voyage().New(dStart, "Lisbon");
        string cPort = "Lis";
        DateOnly dNext = default;
        Set(_SET_EXACT, "ON");
        QOut("span=" + LTrim(Str(nSpan)));
        QOut("half=" + (((decimal)(dEnd.DayNumber - dStart.DayNumber)) / 2 > 4 ? "y" : "n"));
        QOut("plus=" + DToS(dStart.AddDays((int)(7))));
        QOut("swap=" + DToS(dStart.AddDays((int)(7))));
        QOut("less=" + DToS(dEnd.AddDays(-(int)(3))));
        dNext = dStart;
        dNext = dNext.AddDays((int)(10));
        QOut("peq=" + DToS(dNext));
        dNext = dNext.AddDays(-(int)(1));
        QOut("meq=" + DToS(dNext));
        QOut("dock=" + LTrim(Str((decimal)(DockDate().DayNumber - oVoyage.dDepart.DayNumber))));
        QOut("arr=" + DToS(oVoyage.Arrival(2)));
        QOut("lt=" + (HbRuntime.StrCmp(cPort, oVoyage.cPort) < 0 ? "y" : "n"));
        QOut("ge=" + (HbRuntime.StrCmp(oVoyage.cPort, cPort) >= 0 ? "y" : "n"));
        QOut("gt=" + (HbRuntime.StrCmp(oVoyage.cPort, cPort) > 0 ? "y" : "n"));
        QOut("le=" + (HbRuntime.StrCmp(oVoyage.cPort, "Lisbon") <= 0 ? "y" : "n"));
        QOut("time=" + (HbRuntime.StrCmp(Time(), "00:00") >= 0 ? "y" : "n"));
        QOut("eq=" + (oVoyage.cPort == "Lisbon" ? "y" : "n"));
    }
}
