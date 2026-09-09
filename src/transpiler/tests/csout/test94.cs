using System;
using System.Collections.Generic;
using static HbRuntime;
using static Program;

// Test 94: sends on typed receivers — four shapes C# needs help with.
//
// (1) `Teapot():New( 2 ):Serve()` — the constructor call is cast to its
//     class, and as a receiver that cast must bind to the constructor
//     call, not to the chained member (17 CS0029 in the easipos corpus:
//     `nChoice := StockOperationsDialog():New(...):Run()`).
// (2) `Kettle():New( 1500 )` on a class that declares only Init:
//     Harbour's HBObject forwards New to Init (the "classy"
//     compatibility in hbclass.ch), so the C# call goes to Init too.
// (3) `oKettle:lisboiling` — Harbour is case-insensitive, C# is not: a
//     member on a typed receiver is emitted in its declared spelling
//     (`oTransaction:lSaleCdSLtyOnline` against `VAR lSaleCdSLtyOnLine`).
// (4) `oMug:nHandles` read through a Mug-typed parameter that holds a
//     BigMug — the member is declared on the subclass only, a downcast
//     Harbour never spells; the access goes through (dynamic), as an
//     undeclared Self member does. A member no class in the chain or
//     below declares stays a static access, so a real typo is still a
//     C# error, which is the finding wanted. The reftab only carries
//     rows for TYPED members (`AS INTEGER` here, as FcnTranLine's
//     nFixedNo in the corpus); an untyped VAR is invisible to both the
//     spelling and the subclass lookups.
// #include "hbclass.ch"
public class Kettle
{
    public decimal nWatts = 0;
    public bool lIsBoiling = false;

    public dynamic Init(decimal nWatts = default)
    {
        this.nWatts = nWatts;
        return this;
    }
}

public class Teapot
{
    public decimal nCups = 0;

    public dynamic New(decimal nCups = default)
    {
        this.nCups = nCups;
        return this;
    }

    public decimal Serve()
    {
        return this.nCups * 2;
    }
}

public class Mug
{
    public decimal nSize = 1;

}

public class BigMug : Mug
{
    public long nHandles = 2;

    public dynamic New()
    {
        this.nSize = 2;
        return this;

        /* Called with a Mug and a BigMug, so the slot is Mug; nHandles exists
   only on BigMug and the guard keeps the read to those. */
    }
}

public static partial class Program
{
    public static dynamic Handles(Mug oMug = default)
    {
        return (oMug.nSize > 1 ? ((dynamic)oMug).nHandles : 1);
    }

    public static void Main(string[] args)
    {
        decimal nServed = ((Teapot)new Teapot().New(2)).Serve();
        Kettle oKettle = (Kettle)new Kettle().Init(1500);
        BigMug oBigMug = (BigMug)new BigMug().New();
        oKettle.lIsBoiling = true;
        HbRuntime.QOut("served=" + HbRuntime.LTrim(HbRuntime.Str(nServed)));
        HbRuntime.QOut("watts=" + HbRuntime.LTrim(HbRuntime.Str(oKettle.nWatts)) + " boiling=" + (oKettle.lIsBoiling ? "T" : "F"));
        HbRuntime.QOut("mug=" + HbRuntime.LTrim(HbRuntime.Str(Handles(new Mug()))) + " big=" + HbRuntime.LTrim(HbRuntime.Str(Handles(oBigMug))));
        return;
    }
}
