using System;
using System.Collections.Generic;
using static HbRuntime;
using static Program;

// Test 144: `X():Method()` with no arguments sends the method to the new
// instance and answers its result; only New() is the constructor.
//
// The emitter wrote every `X():Method()` without arguments as `X():New()`
// would be: `X():Init()` became HbRuntime.Initialised( new X(), … ), which
// answers the object whatever Init() returned, so a device class's NIL
// ("could not start") was never seen; and `X():Start144()` on a class with
// neither New nor Init became a bare `new X()`, the method never sent.
// test124 covers the same messages with arguments.

// #include "hbclass.ch"
// NIL on failure, Self on success, as the device classes do
public class Device144 : IHbObject
{
    public bool lUp144 = false;

    public virtual Device144 Init()
    {
        if (!test144_s_lWorks144)
        {
            return null;
        }

        lUp144 = true;
        return this;

        // neither New nor Init: the class function's instance, then the method
    }
}

public class Remote144 : IHbObject
{
    public decimal nStarts144 = 0;

    public virtual Remote144 Start144()
    {
        nStarts144++;
        return this;
    }
}

public static partial class Program
{
    public static bool test144_s_lWorks144 = false;
    public static void Main(string[] args)
    {
        Device144 oDevice144 = default;
        Remote144 oRemote144 = default;

        oDevice144 = new Device144().Init();
        HbRuntime.QOut("Init() of a device that cannot start:", (oDevice144 == null ? "NIL" : "an object"));

        oDevice144 = HbRuntime.Initialised(new Device144(), self => self.Init());
        HbRuntime.QOut("New() of the same device:", (oDevice144 == null ? "NIL" : "an object"), oDevice144.lUp144);

        test144_s_lWorks144 = true;
        oDevice144 = new Device144().Init();
        HbRuntime.QOut("Init() of a device that starts:", (oDevice144 == null ? "NIL" : "an object"), oDevice144.lUp144);

        HbRuntime.QOut("Init() as a receiver:", new Device144().Init().lUp144);

        oRemote144 = new Remote144().Start144();
        HbRuntime.QOut("Start144() ran:", oRemote144.nStarts144);

        return;
    }
}
