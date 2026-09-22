using System;
using static HbRuntime;
using static Program;

// Test 40: BEGIN SEQUENCE / END SEQUENCE with no RECOVER/ALWAYS.
//
// Before the fix, this emitted `try { ... }` with nothing following,
// which C# rejects (CS1524). Without RECOVER, Harbour ends a BREAK in
// the body at END SEQUENCE; a runtime error still goes to the error
// block. So the emitted catch is `catch (HbBreak) { }` (test114 has
// the rest of BREAK and BEGIN SEQUENCE); only BEGIN SEQUENCE WITH and
// no RECOVER, which swallows every error, draws a W-level warning.
public static partial class Program
{
    public static void Main(string[] args)
    {
        bool lBodyRan = false;

        try
        {
            lBodyRan = true;
        }
        catch (HbBreak)
        {
        }
        HbRuntime.QOut("body: " + (lBodyRan ? "yes" : "no"));

        return;
    }
}
