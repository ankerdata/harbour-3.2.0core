using System;
using System.Collections.Generic;
using static HbRuntime;
using static Program;

// Test 140: a write through a typed member into a whole-number field.
//
// A member named for a class (`VAR oGauge140`, no AS) is declared as that
// class in C#, so every send through it is checked. A Harbour number
// written into one of its INTEGER members (`::oGauge140:nLevel :=
// nAmount`) needs the (long) a write through a local gets: C# will not
// narrow a decimal on its own. The emitter's check read only a variable
// receiver, so a member receiver got none and the C# was CS0266
// (ormtestsuite.prg's `::oTestOrmTable:nTestNo := nOrigTestNo`, once the
// member was named for its table). The receiver probe also reads such a
// member as its class now, so a division of the whole-number member
// keeps its decimal. From outside the class (`oPanel:oGauge140:nLevel`)
// the probe reads the class's reftab row, which the scan leaves untyped
// for a member typed by its name: it takes the name's class there too
// (EasiPOS's `oPOSStatus:oFree:nFixedNo := nTran`, once POSStatus's
// `oFree` lost its AS OBJECT).

// #include "hbclass.ch"
public class Gauge140 : IHbObject
{
    public long nLevel = 0;

}

public class Panel140 : IHbObject
{
    public Gauge140 oGauge140;

    public virtual Panel140 New()
    {
        oGauge140 = new Gauge140();
        return this;
    }

    public virtual Panel140 FillGauge140(decimal nAmount = default)
    {
        oGauge140.nLevel = (long)(nAmount);
        return this;
    }

    public virtual decimal HalfLevel140()
    {
        return (decimal)(oGauge140.nLevel) / 2;
    }
}

public static partial class Program
{
    public static void Main(string[] args)
    {
        Panel140 oPanel = new Panel140().New();
        decimal nTotal = 14;
        decimal nAmount = nTotal / 2;

        oPanel.FillGauge140(nAmount);
        HbRuntime.QOut(HbRuntime.Str(oPanel.oGauge140.nLevel, 4));
        HbRuntime.QOut(HbRuntime.Str(oPanel.HalfLevel140(), 6, 2));
        oPanel.oGauge140.nLevel = (long)(nAmount + 1);
        HbRuntime.QOut(HbRuntime.Str(oPanel.oGauge140.nLevel, 4));
        return;
    }
}
