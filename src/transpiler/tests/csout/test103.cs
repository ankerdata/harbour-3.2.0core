using System;
using static HbRuntime;
using static Program;

// Test 103: METHOD ToString() overrides object.ToString; a local built
// by `X():New()` on a name the reftab does not know as a class stays
// dynamic.
//
// Every C# class inherits object.ToString(), so a Harbour METHOD
// ToString() with no parameters is that method: it emits
// `public override string ToString()` (posclass.prg, CS0114 — the one
// warning the corpus build carried). And `X():New()` types the
// receiving local as X only when X is a class in the reftab:
// `hbClass():New(…)` and `TOleAuto():New(…)` are RTL class functions
// whose New returns a runtime object, and naming the local `hbClass`
// named a C# type that does not exist (ormsql.prg's
// ConstructTableInstance, CS0246). Those locals fall through to the
// Hungarian prefix — `oCom` is `dynamic`. The branch never runs; only
// the declaration matters. TOleAuto lives in hbwin, which the suite
// does not link, so the Harbour build gets a stand-in under
// #ifndef __HB_TRANSPILER__ — the corpus's own convention for code one
// side must not see.
// #include "hbclass.ch"
public class Beacon
{
    public decimal nCode = 7;

    public override string ToString()
    {
        return "beacon#" + HbRuntime.LTrim(HbRuntime.Str(this.nCode));
    }
}

public static partial class Program
{
    public static void Main(string[] args)
    {
        Beacon oBeacon = new Beacon();
        dynamic oCom = default;
        HbRuntime.QOut(oBeacon.ToString());
        if (oBeacon.nCode > 100)
        {
            oCom = HbRuntime.TOleAuto().New("Scripting.Dictionary");
            HbRuntime.QOut(oCom.Count);
        }

        HbRuntime.QOut("done");
        return;
    }
}
