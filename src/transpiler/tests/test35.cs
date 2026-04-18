using System;
using static HbRuntime;

// Test 35: file-scope MEMVAR declaration round-trip.
//
// Pairs with test33 (file-scope STATIC). File-scope MEMVAR is
// rare in real code — no file under easiutil / sharedx / easiposx
// declares one at file scope — but the AST path is symmetric with
// file-scope STATIC: both live in the synthetic startup function's
// body and the emitters must pull them out as top-level
// declarations when round-tripping.
//
// The declaration tells the compiler to treat bare `gCount`
// references in this file as memvar accesses; PUBLIC is what
// actually creates and initializes the memvar at runtime.
public static partial class Program
{
    public static dynamic gCount;
    public static void Main(string[] args)
    {
        gCount = 0;
        Bump();
        Bump();
        Bump();
        HbRuntime.QOUT("count=" + HbRuntime.STR(gCount));
        return;
    }

    public static void Bump()
    {
        gCount++;
        return;
    }
}
