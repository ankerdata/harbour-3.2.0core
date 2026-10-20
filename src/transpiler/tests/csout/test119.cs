using System;
using static HbRuntime;
using static Program;

// Test 119: an IIF is the type its two branches share.
//
// easiutil/settflag.prg and shared/buffflag.prg set and clear a bit in 100
// one-line functions, RETURN IIF( lx, hb_bitOr( n, BIT ), hb_bitAnd( n,
// hb_bitNot( BIT ) ) ). Both branches are numbers, but the return type never
// looked inside the IIF and every one returned dynamic. Pinned: two numbers
// (a decimal return), two strings, a number beside NIL (still dynamic, NIL
// says nothing), and a caller that reads the typed return.
public static partial class Program
{
    public static void Main(string[] args)
    {
        decimal nFlags = 1;

        nFlags = SetBit119(nFlags, true);
        QOut("set:", nFlags, HasBit119(nFlags));
        nFlags = SetBit119(nFlags, false);
        QOut("cleared:", nFlags, HasBit119(nFlags));
        QOut("word:", Word119(true), Word119(false));
        QOut("maybe:", Maybe119(true), Maybe119(false) == null);
        QOut("sum:", SetBit119(0, true) + Half119(true) + Half119(false));

        return;
    }

    public static long SetBit119(decimal nFlags = default, bool lx = default)
    {
        return (lx ? hb_bitOr(nFlags, Test119PrgConst.FLAG119_BIT) : hb_bitAnd(nFlags, hb_bitNot(Test119PrgConst.FLAG119_BIT)));
    }

    public static bool HasBit119(decimal nFlags = default)
    {
        return hb_bitAnd(nFlags, Test119PrgConst.FLAG119_BIT) != 0;
    }

    public static string Word119(bool lx = default)
    {
        return (lx ? "on" : "off");
    }

    public static dynamic Maybe119(bool lx = default)
    {
        return (lx ? 1 : null);

        // a long branch beside a decimal one widens to decimal
    }
    public static decimal Half119(bool lx = default)
    {
        return (lx ? hb_bitShift(8, -1) : 0.5m);
    }
}
