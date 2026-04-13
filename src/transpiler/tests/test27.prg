// Test 27: DATE → DateOnly and TIMESTAMP → DateTime type maps plus
// literal date emission.
//
// The transpiler used to map both Harbour DATE and TIMESTAMP to C#
// DateTime. That lost the distinction (Harbour DATE has no time
// component) and required DateTime.Today-style runtime calls even
// for date-only values. DATE now maps to .NET 6+ DateOnly and
// TIMESTAMP continues to map to DateTime.
//
// Separately, date literals used to emit as placeholder
// `new DateTime(/* date: 0x... */)` stubs that compiled to a zero
// argument list (syntax error). They now decode the Julian day via
// hb_dateDecode and emit proper DateOnly / DateTime constructors.

#include "common.ch"

PROCEDURE Main()
   LOCAL dBirth := 0d19700101      // DATE literal → DateOnly(1970, 1, 1)
   LOCAL dToday := Date()          // DATE() → DateOnly
   LOCAL cFormatted

   cFormatted := DToS( dBirth )
   ? "birth:", cFormatted

   IF dToday >= dBirth
      ? "chronology holds"
   ELSE
      ? "time flows backwards"
   ENDIF

RETURN
