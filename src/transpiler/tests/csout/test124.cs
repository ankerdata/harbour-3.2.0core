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

// NIL on failure, Self on success, as the device classes do
public class Gadget124 : IHbObject
{
    public string cPort124;
    public bool lOpen124;

    public virtual Gadget124 Init(string cPort = default)
    {
        cPort124 = cPort;
        lOpen124 = !Empty(cPort);
        if (!lOpen124)
        {
            return null;
        }

        return this;

        // the Transaction / POSStatus shape: set up, return NIL
    }
}

public class Ledger124 : IHbObject
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
        Ledger124 oLedger = Initialised(new Ledger124(), self => self.Init());
        Gadget124 oGadget = Initialised(new Gadget124(), self => self.Init(""));

        QOut(oLedger == null, oLedger.nLines124);
        QOut(oGadget == null, oGadget.lOpen124);
        QOut((Initialised(new Gadget124(), self => self.Init("COM1"))).cPort124);
        QOut(new Gadget124().Init("") == null, new Gadget124().Init("COM2") == null);
    }
}
