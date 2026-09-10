using System;
using static HbRuntime;
using static Program;

// Test 101: date arithmetic and string ordering — both operators C#
// refuses (CS0019: DateOnly has no arithmetic, string has no `<`).
//
// `d1 - d2` is Harbour's day count, a number: it emits
// `(decimal)(d1.DayNumber - d2.DayNumber)` — decimal so a later `/`
// keeps float division (xzjobs' `Date() - oTicket:dIssueDate`, 14
// sites). `d + n`, `n + d`, `d - n` emit `d.AddDays( (int)( n ) )`,
// and `d += n` / `d -= n` become `d = d.AddDays( ... )` (fbrands'
// `dBusiness -= 1`). Strings order by hb_itemStrCmp under SET EXACT
// OFF — the shorter length decides, and a longer LEFT operand that
// carries the right one as a prefix is EQUAL, so "Lisbon" > "Lis" is
// false while "Lis" < "Lisbon" is true — which C# has no operator
// for; `<`, `<=`, `>`, `>=` on strings emit `HbRuntime.StrCmp( a, b )
// <op> 0` and `=` stays `==` (postdr's `cTime <= "24:00"`, 12 sites).
// Operand types come from the emit-side probe: a local or parameter's
// declared type, a DATA member of the current class, a member of a
// typed receiver via its reftab row, a function's reftab return type
// (DockDate) or its hbfuncs.tab one (Time).
// #include "hbclass.ch"
public class Voyage
{
    public DateOnly dDepart;
    public string cPort;

    public dynamic New(DateOnly dDepart = default, string cPort = default)
    {
        this.dDepart = dDepart;
        this.cPort = cPort;
        return this;
    }

    public DateOnly Arrival(decimal nDays = default)
    {
        return this.dDepart.AddDays((int)(nDays));
    }
}

public static partial class Program
{
    public static DateOnly DockDate()
    {
        return HbRuntime.SToD("20260315");
    }

    public static void Main(string[] args)
    {
        DateOnly dStart = HbRuntime.SToD("20260301");
        DateOnly dEnd = HbRuntime.SToD("20260310");
        decimal nSpan = (decimal)(dEnd.DayNumber - dStart.DayNumber);
        Voyage oVoyage = (Voyage)new Voyage().New(dStart, "Lisbon");
        string cPort = "Lis";
        DateOnly dNext = default;
        HbRuntime.QOut("span=" + HbRuntime.LTrim(HbRuntime.Str(nSpan)));
        HbRuntime.QOut("half=" + (((decimal)(dEnd.DayNumber - dStart.DayNumber)) / 2 > 4 ? "y" : "n"));
        HbRuntime.QOut("plus=" + HbRuntime.DToS(dStart.AddDays((int)(7))));
        HbRuntime.QOut("swap=" + HbRuntime.DToS(dStart.AddDays((int)(7))));
        HbRuntime.QOut("less=" + HbRuntime.DToS(dEnd.AddDays(-(int)(3))));
        dNext = dStart;
        dNext = dNext.AddDays((int)(10));
        HbRuntime.QOut("peq=" + HbRuntime.DToS(dNext));
        dNext = dNext.AddDays(-(int)(1));
        HbRuntime.QOut("meq=" + HbRuntime.DToS(dNext));
        HbRuntime.QOut("dock=" + HbRuntime.LTrim(HbRuntime.Str((decimal)(DockDate().DayNumber - oVoyage.dDepart.DayNumber))));
        HbRuntime.QOut("arr=" + HbRuntime.DToS(oVoyage.Arrival(2)));
        HbRuntime.QOut("lt=" + (HbRuntime.StrCmp(cPort, oVoyage.cPort) < 0 ? "y" : "n"));
        HbRuntime.QOut("ge=" + (HbRuntime.StrCmp(oVoyage.cPort, cPort) >= 0 ? "y" : "n"));
        HbRuntime.QOut("gt=" + (HbRuntime.StrCmp(oVoyage.cPort, cPort) > 0 ? "y" : "n"));
        HbRuntime.QOut("le=" + (HbRuntime.StrCmp(oVoyage.cPort, "Lisbon") <= 0 ? "y" : "n"));
        HbRuntime.QOut("time=" + (HbRuntime.StrCmp(HbRuntime.Time(), "00:00") >= 0 ? "y" : "n"));
        HbRuntime.QOut("eq=" + (oVoyage.cPort == "Lisbon" ? "y" : "n"));
        return;
    }
}
