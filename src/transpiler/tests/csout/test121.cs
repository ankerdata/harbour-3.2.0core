using System;
using static HbRuntime;
using static Program;

// Test 121: `i` names beyond locals, and what returns long.
//
// test120 pinned `i` locals, statics, parameters and returns. This pins the
// rest of where an `i` name can stand - a class member with and without an
// INIT, written by the compound operators; an `@iX` argument to an `n`
// parameter and an `@nX` argument to an `i` parameter - and what a routine
// returning long does to the arithmetic around it: a division of a call
// that returns long (an `i` local, a bit operation) is still Harbour's
// float division, and an `n` local initialised from a bit operation still
// takes a fraction later, as one initialised from an integral #define does. A bit setter, RETURN IIF( lx, hb_bitOr(...),
// hb_bitAnd(...) ), returns long into an AS INTEGER member. A decimal
// written into an `i` member is cast, from inside the class or outside it.
// A member is written bare in C# unless a parameter, a local or a codeblock
// parameter of the same name makes `this.` necessary (Alex), in a method
// body and in an INLINE one.

// #include "hbclass.ch"
public class Tally121
{
    public long iCount = 0;
    public long iTotal;
    public long nFlags = 0;
    public string cName = "";

    public dynamic Label121() => cName + "=" + HbRuntime.hb_ntos( iCount );
    public dynamic Relabel121(dynamic cName = default) => this.cName = cName;
    public virtual Tally121 Add121(long iStep = default)
    {
        iCount += iStep;
        iTotal = iCount * 2;
        iTotal = (long)(HbRuntime.Len("abc") + iTotal);
        nFlags = SetFlag121(nFlags, iCount > 3);
        return this;
    }

    public virtual long Count121()
    {
        return iCount;
    }

    public virtual Tally121 Rename121(string cName = default)
    {
        // a parameter named as the member: `this.` is what tells them apart
        this.cName = cName;
        return this;
    }

    public virtual Tally121 Spread121(dynamic[] aSteps = default)
    {
        // ...and a codeblock parameter named as one
        HbRuntime.AEval(aSteps, ((Func<dynamic, dynamic>)((iCount) => iTotal += (long)(iCount + this.iCount))));
        return this;
    }
}

public static partial class Program
{
    public static void Main(string[] args)
    {
        Tally121 oTally = new Tally121();
        long iWhole = 7;
        decimal nPlain = 9;
        decimal nMask = HbRuntime.hb_bitAnd(7, 3);
        decimal nHalf = Test121PrgConst.FLAG121_BIT;

        oTally.Add121(2);
        oTally.Add121(3);
        HbRuntime.QOut("tally:", oTally.Count121(), oTally.iTotal, oTally.nFlags);

        {
            decimal _hbref_iWhole = iWhole;
            Bump121(ref _hbref_iWhole);
            iWhole = (long)(_hbref_iWhole);
        }
        {
            long _hbref_nPlain = (long)(nPlain);
            Halve121(ref _hbref_nPlain);
            nPlain = _hbref_nPlain;
        }
        HbRuntime.QOut("by ref:", iWhole, nPlain);

        HbRuntime.QOut("divided:", HbRuntime.Str((decimal)(Thrice121(7)) / 4, 6, 2), HbRuntime.Str((decimal)(HbRuntime.hb_bitAnd(7, 6)) / 4, 6, 2), HbRuntime.Str((decimal)(oTally.Count121()) / 2, 6, 2));
        nMask = nMask / 2;
        nHalf = nHalf / 4;
        HbRuntime.QOut("mask:", HbRuntime.Str(nMask, 6, 2), HbRuntime.Str(nHalf, 6, 2));

        oTally.iTotal = (long)(nPlain);
        HbRuntime.QOut("member:", oTally.iTotal);

        oTally.Rename121("t");
        oTally.Spread121(new dynamic[] { 1, 2 });
        HbRuntime.QOut("names:", oTally.Label121(), oTally.iTotal);
        oTally.Relabel121("u");
        HbRuntime.QOut("inline:", oTally.Label121());

        return;
    }

    public static void Bump121(ref decimal nValue)
    {
        nValue += 1;
        return;
    }

    public static void Halve121(ref long iValue)
    {
        iValue = (long)(HbRuntime.Int((decimal)(iValue) / 2));
        return;
    }

    public static long Thrice121(long iValue = default)
    {
        long iResult = iValue * 3;
        return iResult;
    }

    public static long SetFlag121(decimal nFlags = default, bool lx = default)
    {
        return (lx ? HbRuntime.hb_bitOr(nFlags, Test121PrgConst.FLAG121_BIT) : HbRuntime.hb_bitAnd(nFlags, HbRuntime.hb_bitNot(Test121PrgConst.FLAG121_BIT)));
    }
}
