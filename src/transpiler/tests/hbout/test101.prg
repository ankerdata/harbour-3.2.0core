#include "astype.ch"
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
#include "hbclass.ch"

CLASS Voyage

   DATA dDepart AS DATE
   DATA cPort AS STRING
   METHOD New( dDepart, cPort )
   METHOD Arrival( nDays )

ENDCLASS

METHOD New( dDepart AS DATE, cPort AS STRING ) AS OBJECT CLASS Voyage
   ::dDepart := dDepart
   ::cPort := cPort
RETURN Self

METHOD Arrival( nDays AS NUMERIC ) AS DATE CLASS Voyage
RETURN ::dDepart + nDays

FUNCTION DockDate() AS DATE
RETURN SToD("20260315")

PROCEDURE Main()
   LOCAL dStart := SToD("20260301") AS DATE
   LOCAL dEnd := SToD("20260310") AS DATE
   LOCAL nSpan := dEnd - dStart AS NUMERIC
   LOCAL oVoyage := Voyage():New(dStart, "Lisbon") AS OBJECT
   LOCAL cPort := "Lis" AS STRING
   LOCAL dNext AS DATE
   Set(_SET_EXACT, "ON")
   QOut("span=" + LTrim(Str(nSpan)))
   QOut("half=" + IIF((dEnd - dStart) / 2 > 4, "y", "n"))
   QOut("plus=" + DToS(dStart + 7))
   QOut("swap=" + DToS(7 + dStart))
   QOut("less=" + DToS(dEnd - 3))
   dNext := dStart
   dNext += 10
   QOut("peq=" + DToS(dNext))
   dNext -= 1
   QOut("meq=" + DToS(dNext))
   QOut("dock=" + LTrim(Str(DockDate() - oVoyage:dDepart)))
   QOut("arr=" + DToS(oVoyage:Arrival(2)))
   QOut("lt=" + IIF(cPort < oVoyage:cPort, "y", "n"))
   QOut("ge=" + IIF(oVoyage:cPort >= cPort, "y", "n"))
   QOut("gt=" + IIF(oVoyage:cPort > cPort, "y", "n"))
   QOut("le=" + IIF(oVoyage:cPort <= "Lisbon", "y", "n"))
   QOut("time=" + IIF(Time() >= "00:00", "y", "n"))
   QOut("eq=" + IIF(oVoyage:cPort == "Lisbon", "y", "n"))
RETURN
