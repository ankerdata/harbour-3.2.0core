using System;
using static HbRuntime;
using static Program;

// Test 20a + 20b: cross-file return-type propagation.
//
// These two files exercise the type-inference engine's ability to
// learn the return type of a user function from one file and use it
// when type-checking calls in another file. The pair is built into a
// single executable test20 (special-cased in the build scripts).
//
// test20a.prg defines:
//
//    FUNCTION DoubleIt( n )
//       RETURN n * 2
//
// On its own, this transpiles correctly: intra-function inference
// notes that `n * 2` is NUMERIC, so DoubleIt's return type becomes
// `decimal` and the C# parameter for n becomes `decimal` too.
//
// test20b.prg defines:
//
//    FUNCTION QuadrupleIt( n )
//       RETURN DoubleIt( DoubleIt( n ) )
//
// Without cross-file return-type propagation, the transpiler does NOT
// know what DoubleIt returns when it's processing test20b. The chain
// looks like:
//
//    DoubleIt( n )                — return type: unknown → dynamic
//    DoubleIt( DoubleIt( n ) )    — return type: unknown → dynamic
//    QuadrupleIt's RETURN expr    — type: dynamic
//
// Result: QuadrupleIt emits as `public static dynamic QuadrupleIt(...)`,
// even though it's definitely returning a numeric.
//
// With cross-file return-type propagation in place, the -GF scan
// records DoubleIt's return type in hbreftab.tab, and the subsequent
// -GS pass on test20b sees the cross-file information and emits
// `public static decimal QuadrupleIt(decimal n)`.
//
// Both files together verify the round-trip behaviour and that the
// runtime output is identical to the .prg / .hb compiled binaries.
public static partial class Program
{
    public static decimal DoubleIt(decimal n = default)
    {
        return n * 2;
    }
}
