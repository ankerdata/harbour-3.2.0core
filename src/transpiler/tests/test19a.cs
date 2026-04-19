using System;
using static HbRuntime;
using static Program;

// Test 19a + 19b: cross-file by-ref scan demo.
//
// These two files demonstrate why the transpiler needs a separate scan
// pass (-GF) before generating code for a multi-file project. The pair
// is built into a single executable test19 in prgexe/, hbexe/, and csexe/
// (special-cased in the build scripts).
//
//    test19a.prg  defines  PROCEDURE Swap( nX, nY )
//                          ↑ no @ call sites in this file
//
//    test19b.prg  calls    Swap( @nA, @nB )
//                          ↑ the by-ref info lives here
//
// Without -GF, transpiling test19a.prg in isolation would produce
//
//    public static void Swap( decimal nX = default, decimal nY = default )
//
// because the file's only call sites for `Swap` are inside test19b.prg,
// which the transpiler never sees during a single-file run. With the
// pre-scan in place, the resulting C# is the correct
//
//    public static void Swap( ref decimal nX, ref decimal nY )
//
// Workflow:
//
//    # 1. Scan every .prg in the project. Updates hbreftab.tab with
//    #    whole-program signature information.
//    hbtranspiler -GF -Iinclude test19a.prg
//    hbtranspiler -GF -Iinclude test19b.prg
//
//    # 2. Now transpile each file. -GS / -GT both auto-load
//    #    hbreftab.tab so they see the cross-file by-ref data.
//    hbtranspiler -GS -Iinclude test19a.prg
//    hbtranspiler -GS -Iinclude test19b.prg
//
// runtests.sh / buildcs.sh / buildhb.sh / buildprg.sh in this directory
// all run the -GF pre-scan automatically before the per-file work.
public static partial class Program
{
    public static void Swap(ref decimal nX, ref decimal nY)
    {
        decimal nT = nX;

        nX = nY;
        nY = nT;

        return;
    }
}
