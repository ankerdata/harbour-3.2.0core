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
#include "hbclass.ch"

CLASS Voyage
   VAR dDepart AS DATE
   VAR cPort AS STRING
   METHOD New( dDepart, cPort )
   METHOD Arrival( nDays )
ENDCLASS

METHOD New( dDepart, cPort ) CLASS Voyage
   ::dDepart := dDepart
   ::cPort := cPort
RETURN Self

METHOD Arrival( nDays ) CLASS Voyage
RETURN ::dDepart + nDays

FUNCTION DockDate()
RETURN SToD( "20260315" )

PROCEDURE Main()
   LOCAL dStart := SToD( "20260301" )
   LOCAL dEnd := SToD( "20260310" )
   LOCAL nSpan := dEnd - dStart
   LOCAL oVoyage := Voyage():New( dStart, "Lisbon" )
   LOCAL cPort := "Lis"
   LOCAL dNext
   ? "span=" + LTrim( Str( nSpan ) )
   ? "half=" + IIf( ( dEnd - dStart ) / 2 > 4, "y", "n" )
   ? "plus=" + DToS( dStart + 7 )
   ? "swap=" + DToS( 7 + dStart )
   ? "less=" + DToS( dEnd - 3 )
   dNext := dStart
   dNext += 10
   ? "peq=" + DToS( dNext )
   dNext -= 1
   ? "meq=" + DToS( dNext )
   ? "dock=" + LTrim( Str( DockDate() - oVoyage:dDepart ) )
   ? "arr=" + DToS( oVoyage:Arrival( 2 ) )
   ? "lt=" + IIf( cPort < oVoyage:cPort, "y", "n" )
   ? "ge=" + IIf( oVoyage:cPort >= cPort, "y", "n" )
   ? "gt=" + IIf( oVoyage:cPort > cPort, "y", "n" )
   ? "le=" + IIf( oVoyage:cPort <= "Lisbon", "y", "n" )
   ? "time=" + IIf( Time() >= "00:00", "y", "n" )
   ? "eq=" + IIf( oVoyage:cPort == "Lisbon", "y", "n" )
RETURN
