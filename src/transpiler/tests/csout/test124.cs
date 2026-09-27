using System;
using System.Collections.Generic;
using static HbRuntime;
using static Program;

// Test 124: `X():New( ... )` answers the object when X declares only Init().
//
// HBObject's New() (rtl/tobject.prg) runs Init( ... ) and returns Self,
// whatever Init() returned. The C# was `(X)new X().Init( ... )`, which
// answered Init()'s result: easipos's Transaction and POSStatus declare an
// Init() that returns NIL, so all 75 of their `New()` calls produced null.
// It is HbRuntime.Initialised( new X(), self => self.Init( ... ) ) now.
//
// `X():Init( ... )` is a different message: it answers what Init() returns,
// which is how a device class says it could not connect (easipos fmde.prg);
// `X():New( ... )` can never say so.

// #include "hbclass.ch"
// NIL on failure, Self on success, as the device classes do
public class Gadget124
{
    public string cPort124;
    public bool lOpen124;

    public virtual Gadget124 Init(string cPort = default)
    {
        cPort124 = cPort;
        lOpen124 = !HbRuntime.Empty(cPort);
        if (!lOpen124)
        {
            return null;
        }

        return this;

        // the Transaction / POSStatus shape: set up, return NIL
    }
}

public class Ledger124
{
    public decimal nLines124;

    public virtual dynamic Init()
    {
        nLines124 = 0;
        return null;
    }
}

public static partial class Program
{
    public static void Main(string[] args)
    {
        Ledger124 oLedger = HbRuntime.Initialised(new Ledger124(), self => self.Init());
        Gadget124 oGadget = HbRuntime.Initialised(new Gadget124(), self => self.Init(""));

        HbRuntime.QOut(oLedger == null, oLedger.nLines124);
        HbRuntime.QOut(oGadget == null, oGadget.lOpen124);
        HbRuntime.QOut((HbRuntime.Initialised(new Gadget124(), self => self.Init("COM1"))).cPort124);
        HbRuntime.QOut((Gadget124)new Gadget124().Init("") == null, (Gadget124)new Gadget124().Init("COM2") == null);

        return;
    }
}
