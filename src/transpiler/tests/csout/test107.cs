using System;
using static HbRuntime;
using static Program;

// Test 107: `#pragma BEGINCSHARP` inside a routine — C# statements in place.
//
// test105's blocks are file scope: type declarations, flushed at namespace
// level wherever they stand. A block whose text is NOT a type declaration
// and that stands inside a routine is C# statements instead: it lands in
// the method body where it stands, re-indented. That lets a routine carry
// its Harbour (9.0 keeps running it) and its C# side by side, one guarded
// stretch at a time:
//
//    #ifndef __HB_TRANSPILER__
//       <Harbour>
//    #else
//    #pragma BEGINCSHARP
//       <C#>
//    #pragma ENDCSHARP
//    #endif
//
// Position alone cannot tell the two kinds apart — Harbour has no end of
// routine, so a block after a routine's last statement is "inside" it
// until the next routine begins — but C# can: only type declarations are
// legal at namespace level, and none inside a method. A block of nothing
// but comments stays file scope (test105 keeps one in Main()).
//
// The block sees the method's emitted C# names: members bare (`nCount`),
// parameters and locals as declared. A block ending in `return …;` or
// `throw …;` never falls through, so the RETURN Harbour needs after the
// guard is dropped from the C# as unreachable (CS0162) — `Polarity` and
// the method `Tenfold` below.
// Every case prints the same from both branches. The suite scans all its
// tests into one reftab, so these names are unique across it.
// #include "hbclass.ch"
// a method: the member written as the C# member
public class Meter
{
    public decimal nCount = 0;

    public virtual dynamic Raise(decimal nBy = default)
    {
        nCount += nBy;

        return this;

        // a method whose C# returns: its trailing RETURN is unreachable in C#
    }

    public virtual decimal Tenfold()
    {
        return nCount * 10;

        // a function: a local assigned in C#, read by the Harbour RETURN
    }
}

public static partial class Program
{
    public static decimal Twice(decimal nValue = default)
    {
        decimal nResult = default;

        nResult = nValue * 2;

        return nResult;

        // C# that returns: the RETURN after the guard is unreachable there
    }
    public static decimal Polarity(decimal nValue = default)
    {
        return nValue < 0 ? -1 : 1;

        // a block inside an IF: it lands in the IF's body
    }
    public static string Grade(decimal nValue = default)
    {
        string cText = "zero";
        if (nValue > 0)
        {
            cText = "positive";
        }

        return cText;
    }

    public static void Main(string[] args)
    {
        Meter oCounter = new Meter();
        oCounter.Raise(2);
        oCounter.Raise(3);
        HbRuntime.QOut(oCounter.nCount);
        HbRuntime.QOut(oCounter.Tenfold());
        HbRuntime.QOut(Twice(21));
        HbRuntime.QOut(Polarity(-5));
        HbRuntime.QOut(Polarity(5));
        HbRuntime.QOut(Grade(1));
        HbRuntime.QOut(Grade(0));
        return;
    }
}
