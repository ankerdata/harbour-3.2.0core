using System;
using System.Collections.Generic;
using static HbRuntime;
using static Program;

// Test 104: W0018 — the extra-argument check — in the scan walk.
//
// Harbour drops arguments beyond the declaration silently; C# refuses
// them (CS1501). The check lived in the -GS emitter only, so the -GF
// scan, which is the pipeline's gate, never saw it: a merge brought a
// four-argument call to the three-parameter MakeSQLiteIndexes, the
// scan passed clean and only gen caught it. The walk visits every
// call site with the callee key resolved anyway, so the count compare
// now happens there, in both modes: `Tariff( 10, 2, 99 )` warns
// against the free function, `oT:Post( 5, "extra" )` against a method
// Treasurer only inherits from Bursar, found through the parent link.
// The emitter still drops the extras, so both programs run and agree;
// the warnings are on stderr, not in the output.
//
// The drop counts against the row the call resolves to, and a
// file-static function shadowing a public one elsewhere has its own
// row: `nLevy` here is STATIC with three parameters while test100's
// public `nLevy` takes one — the deliberate collision. Counting
// against the bare name once dropped 23 of rmio2700's 27 RoomCharge
// arguments (the public RoomCharge takes four).
// #include "hbclass.ch"
public class Bursar
{
    public decimal nTotal = 0;

    public dynamic Post(decimal nAmount = default)
    {
        this.nTotal += nAmount;
        return this;
    }
}

public class Treasurer : Bursar
{
    public string cName = "t";

}

public static partial class Program
{
    public static decimal test104_nLevy(decimal nA = default, decimal nB = default, decimal nC = default)
    {
        return nA + nB + nC;
    }

    public static decimal Tariff(decimal nBase = default, decimal nRate = default)
    {
        return nBase * nRate;
    }

    public static void Main(string[] args)
    {
        Treasurer oT = new Treasurer();
        HbRuntime.QOut("tariff=" + HbRuntime.LTrim(HbRuntime.Str(Tariff(10, 2))));
        oT.Post(5);
        HbRuntime.QOut("total=" + HbRuntime.LTrim(HbRuntime.Str(oT.nTotal)));
        HbRuntime.QOut("plain=" + HbRuntime.LTrim(HbRuntime.Str(Tariff(3, 4))));
        HbRuntime.QOut("levy=" + HbRuntime.LTrim(HbRuntime.Str(test104_nLevy(1, 2, 3))));
        return;
    }
}
