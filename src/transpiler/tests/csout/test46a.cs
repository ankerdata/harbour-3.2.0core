using System;
using static HbRuntime;
using static Program;

// Test 46a + 46b: no caller uses a short arity, so the emitter must
// skip the short overload (dead-method avoidance).
//
//    test46a.prg  defines  PROCEDURE SaveFlags( cFile, aFlag, nCount )
//    test46b.prg  calls    SaveFlags( "out.dat", @aArr, 3 )  // full only
//
// The reftab's observed-arity bitmap has bit 3 set and nothing in
// 0..iFirstRef. The short-overload guard checks that intersection and
// skips emission. verify_shortoverload.sh asserts the absence by
// grepping the emitted .cs.
public static partial class Program
{
    public static void SaveFlags(string cFile, ref dynamic[] aFlag, decimal nCount = default)
    {
        decimal i = default;

        HbRuntime.ASize(aFlag, nCount);
        for (i = 1; i <= nCount; i++)
        {
            aFlag[(int)(i) - 1] = cFile + "#" + HbRuntime.Str(i, 1);
        }

        return;
    }
}
