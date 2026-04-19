using System;
using static HbRuntime;
using static Program;

// Test 33: regression for the dropped-PARAM parser bug.
//
// Before commit 9ce8f0ab76, any file with a file-scope STATIC
// initializer (e.g. `static snTrace := -1`) would silently drop
// the parameter list from the LAST user function in the file.
// Emitted C# looked like `string GetThreadName()` even though the
// PRG had `function GetThreadName(nID, lShort, lBrackets)`, and
// body references to those params then failed with CS0103.
//
// Root cause: at end-of-parse, hb_compAddInitFunc( pInitFunc )
// appends the synthetic `(_INITSTATICS)` frame and swaps
// functions.pLast to it, but hb_astEndFunc for the last user
// function wasn't being called until *after* that swap. The
// final AST node then captured pLocals from the init frame
// (empty) instead of the user function's real param list.
//
// Fix: call hb_astEndFunc at the top of the finalization phase,
// before synthetic init/line frames get appended.
//
// This test mirrors the minimal repro used to debug it: a
// file-scope STATIC, a regular function in the middle, and
// another function at the very end whose params *must* survive
// to the emitted C# signature.
public static partial class Program
{
    static decimal test33___hbInit_TEST33_snState = 0;
    public static void Main(string[] args)
    {
        Bump();
        Bump();
        HbRuntime.QOUT(Greet("Alex", true, "!"));
        return;
    }

    public static void Bump()
    {
        test33___hbInit_TEST33_snState++;
        return;

        // The last function in the file — the one the bug dropped params on.
    }
    public static string Greet(string? cName = null, bool? lExclaim = null, string cSuffix = default)
    {
        string cOut = "Hello " + cName;
        if (lExclaim == true)
        {
            cOut += cSuffix;
        }

        return cOut;
    }
}
