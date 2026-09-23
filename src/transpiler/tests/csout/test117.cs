using System;
using System.Collections.Generic;
using static HbRuntime;
using static Program;

// Test 117: a method that only returns Self is typed as its own class.
//
// Harbour's constructors and chaining methods end in RETURN Self, which
// the inference reads as OBJECT — emitted as dynamic, so every New() and
// every chaining method returned dynamic (39 New()/Init() in EasiPOS).
// A method whose every RETURN is Self is now its class: the C# method
// returns it and the reftab row records it, so a chain binds statically.
// Four shapes are pinned here:
//   - Ledger117:New / :Add return Ledger117, so oLedger:Add():Add():Describe()
//     resolves without dynamic;
//   - TaxLedger117:Add has its parent's signature and returns TaxLedger117:
//     an override in C#, legal because a return type may narrow;
//   - TaxLedger117:New takes different parameters, so it is an overload;
//   - PlainLedger117 declares no New and inherits one returning Ledger117 —
//     the constructor pattern casts to the class named, which is what keeps
//     LOCAL oPlain typed as PlainLedger117;
//   - Maybe117 returns Self on one path and NIL on the other, which is how
//     Harbour says a constructor failed (33 device-class Init()s in EasiPOS
//     do it): still the class, since a class-typed C# return takes null,
//     and the caller's == NIL test reads as it did;
//   - Mixed117 returns Self and a string, so it is no identity: dynamic.

// #include "hbclass.ch"
public class Ledger117
{
    public string cName = "";
    public decimal nTotal = 0;

    public virtual Ledger117 New(string cName = default)
    {
        this.cName = cName;
        return this;
    }

    public virtual Ledger117 Add(decimal nAmount = default)
    {
        this.nTotal += nAmount;
        return this;
    }

    public virtual string Describe()
    {
        return this.cName + " " + HbRuntime.hb_ntos(this.nTotal);
    }

    public virtual Ledger117 Maybe117(bool lWant = default)
    {
        if (lWant)
        {
            return this;
        }

        return null;
    }

    public virtual dynamic Mixed117(bool lWant = default)
    {
        if (lWant)
        {
            return this;
        }

        return "gone";
    }
}

public class TaxLedger117 : Ledger117
{
    public decimal nFee = 0;

    public virtual TaxLedger117 New(string cName = default, decimal nFee = default)
    {
        base.New(cName);
        this.nFee = nFee;
        return this;
    }

    public override TaxLedger117 Add(decimal nAmount = default)
    {
        base.Add(nAmount + this.nFee);
        return this;
    }
}

public class PlainLedger117 : Ledger117
{

}

public static partial class Program
{
    public static void Main(string[] args)
    {
        Ledger117 oLedger = (Ledger117)new Ledger117().New("cash");
        TaxLedger117 oTax = (TaxLedger117)new TaxLedger117().New("vat", 3);
        PlainLedger117 oPlain = (PlainLedger117)new PlainLedger117().New("plain");

        HbRuntime.QOut("chain:", oLedger.Add(5).Add(7).Describe());
        HbRuntime.QOut("tax:", oTax.Add(100).Add(10).Describe());
        HbRuntime.QOut("inherited New:", oPlain.Add(2).Describe());
        HbRuntime.QOut("maybe:", HbRuntime.ValType(oLedger.Maybe117(true)), HbRuntime.ValType(oLedger.Maybe117(false)));
        HbRuntime.QOut("maybe nil:", oLedger.Maybe117(false) == null, oLedger.Maybe117(true).Describe());
        HbRuntime.QOut("mixed:", HbRuntime.ValType(oLedger.Mixed117(true)), oLedger.Mixed117(false));

        return;
    }
}
