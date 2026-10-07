using System;
using System.Collections.Generic;
using static HbRuntime;
using static Program;

// Test 122: DESTRUCTOR runs, and a READONLY member is assigned by its class.
//
// A DESTRUCTOR was emitted as an ordinary method nothing called: C# needs a
// finalizer that calls it, and hb_gcAll() has to wait for finalizers, as
// Harbour's runs destructors before it returns. ormsql.prg's registry of
// open tables depends on it: an exclusive table is released by its
// destructor.
//
// When: Harbour runs a destructor as soon as the last reference goes
// (reference counting). C# runs it when the collector reclaims the object,
// and unoptimised code (a Debug build, and every method's first, tier-0
// compilation) keeps each reference in a routine's frame alive until the
// routine returns, so `o := NIL; hb_gcAll()` inside one routine frees
// nothing there. The objects here are dropped by a routine returning,
// where both agree.
//
// A READONLY member was a get-only property, which C# lets only a
// constructor assign; Harbour lets the class's own methods assign it
// (ormsql.prg's InitInstance sets cTableName). It is `{ get; protected set; }`,
// and a PROTECTED one `{ get; set; }` (C# rejects `protected ... protected set`).
//
// A MODULE FRIENDLY class lets the functions of its own file use its
// PROTECTED and HIDDEN members (ormsql.prg's PreparedSeek() reads SQLtTable's
// hSeekStmts): `protected internal` / `internal` in C#.

// #include "hbclass.ch"
public class Handle122 : IHbObject
{
    public string cLabel122 { get; protected set; }

    public virtual Handle122 Label122(string cLabel = default)
    {
        cLabel122 = cLabel;
        return this;

        // a destructor that returns a value is called through a lambda
    }

    public virtual Handle122 Release122()
    {
        test122_snFreed122++;
        return this;

        // spelled otherwise than its declaration: the definition takes the declared Peek122
    }

    ~Handle122() => HbRuntime.RunDestructor(() => Release122());
}

public class Vault122 : IHbObject
{
    protected internal decimal nInside122 = 0;
    protected internal string cSealed122 { get; set; }

    public virtual string Peek122()
    {
        return HbRuntime.hb_ntos(nInside122) + " " + cSealed122;

        // a function of the file, reaching the FRIENDLY class's protected members
    }
}

public static partial class Program
{
    public static decimal test122_snFreed122 = 0;
    public static void test122_Fill122(Vault122 oVault = default)
    {
        oVault.nInside122 = 5;
        oVault.cSealed122 = "sealed";
        return;
    }

    public static void Main(string[] args)
    {
        test122_Open122("first");
        HbRuntime.hb_gcAll(true);
        HbRuntime.QOut("freed:", test122_snFreed122);

        test122_Open122("second");
        HbRuntime.hb_gcAll(true);
        HbRuntime.QOut("freed:", test122_snFreed122);

        test122_Vault122Check();

        return;

        // an object that goes with its routine
    }
    public static void test122_Open122(string cLabel = default)
    {
        Handle122 oHandle = new Handle122();

        oHandle.Label122(cLabel);
        HbRuntime.QOut("label:", oHandle.cLabel122);

        return;
    }

    public static void test122_Vault122Check()
    {
        Vault122 oVault = new Vault122();

        test122_Fill122(oVault);
        HbRuntime.QOut("vault:", oVault.Peek122());

        return;
    }
}
