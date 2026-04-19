using System;
using static HbRuntime;
using static Program;

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

// #include "common.ch"
public static partial class Program
{
    public static void Main(string[] args)
    {
        // DATE literal → DateOnly(1970, 1, 1)
        DateOnly dBirth = new DateOnly(1970, 1, 1);
        // DATE() → DateOnly
        DateOnly dToday = HbRuntime.Date();
        string cFormatted = default;

        cFormatted = HbRuntime.DToS(dBirth);
        HbRuntime.QOut("birth:", cFormatted);

        if (dToday >= dBirth)
        {
            HbRuntime.QOut("chronology holds");
        }
        else
        {
            HbRuntime.QOut("time flows backwards");
        }

        return;
    }
}
