using System;
using static HbRuntime;
using static Program;

// Test 108: a function the program defines wins over a contrib library's
// function of the same name.
//
// The emitter routes a name a contrib .hbx lists to that library's class
// (test106: HbWin.wapi_Sleep). But Harbour's linker takes a library
// member only for a symbol no object file defines, so when the program
// defines the name itself the program's function is the one that runs.
// EasiPOS's easiutil has its own FT_Elapsed, FileSize and Random, which
// hbnf and hbct list too: routed to those library classes, their calls
// reached stubs that throw. A contrib name the reftab records as defined
// in the corpus is now emitted as the program's own function, in its
// declared spelling. Random and FileSize are hbct's names; the suite
// links no hbct, so the Harbour build runs these too.
public static partial class Program
{
    public static void Main(string[] args)
    {
        HbRuntime.QOut(Random());
        HbRuntime.QOut(FileSize(21));
        HbRuntime.QOut(HbRuntime.LTrim(HbRuntime.Str(Random() + FileSize(1))));
        return;
    }

    public static decimal Random()
    {
        return 4;
    }

    public static decimal FileSize(decimal nHandle = default)
    {
        return nHandle * 2;
    }
}
