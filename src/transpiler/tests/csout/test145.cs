using System;
using static HbRuntime;
using static Program;

// Test 145: a local that holds the only reference to a new object is
// disposed where Harbour's reference count reaches zero (hbown.c).
//
// Harbour runs an object's DESTRUCTOR the moment its last reference goes:
// at `o := NIL`, when o is given another object (after the new one is
// built), or when the routine holding o returns. C# counts nothing, and a
// Debug build keeps a routine's references alive until it returns. Where
// the transpiler proves a local holds the only reference, the C# disposes
// it at those points: a `using` declaration for one new object given at
// the top level; otherwise HbRuntime.Replace at reassignment, `?.Dispose()`
// at := NIL and a finally at the routine's exit. A value that may outlive
// the routine (a STATIC, a routine that keeps what it is given) and a
// parameter, which is its caller's, are left to the finalizer, which
// hb_gcAll() waits for. Every Probe145 says when it goes, so the order of
// the lines is the test.

// #include "hbclass.ch"
public class Probe145 : IHbObject, IDisposable
{
    public string cName145;

    public virtual Probe145 New(string cName145 = default)
    {
        this.cName145 = cName145;
        HbRuntime.QOut("built", cName145);
        return this;
    }

    public virtual decimal Touch145()
    {
        return HbRuntime.Len(cName145);
    }

    public virtual void Gone145()
    {
        HbRuntime.QOut("gone", cName145);
        return;

        // one new object, kept to the end: a `using` declaration
    }

    protected bool disposed;

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposed)
            return;
        disposed = true;
        HbRuntime.RunDestructor(() => Gone145(), disposing);
    }

    ~Probe145() => Dispose(false);
}

public static partial class Program
{
    public static dynamic test145_soKept145;
    public static void test145_AtEnd145()
    {
        using Probe145 oProbe145 = new Probe145().New("at end");

        HbRuntime.QOut("using", oProbe145.Touch145());

        return;

        // := NIL, and another object given to it
    }
    public static void test145_Released145()
    {
        Probe145 oProbe145 = new Probe145().New("first");
        try
        {
            HbRuntime.QOut("touched", oProbe145.Touch145());
            oProbe145?.Dispose();
            oProbe145 = null;
            HbRuntime.QOut("after := NIL");
            oProbe145 = new Probe145().New("second");
            oProbe145 = HbRuntime.Replace(oProbe145, new Probe145().New("third"));
            HbRuntime.QOut("after reassignment");

            return;

            // a function that returns the new object it made: its caller owns it
        }
        finally
        {
            oProbe145?.Dispose();
        }
    }
    public static Probe145 test145_Make145(string cName = default)
    {
        Probe145 oProbe145 = new Probe145().New(cName);

        return oProbe145;
    }

    public static void test145_Returned145()
    {
        using Probe145 oProbe145 = test145_Make145("returned");
        HbRuntime.QOut("used", oProbe145.Touch145());

        return;

        // passed to a routine that only reads it: still owned
    }
    public static decimal test145_Read145(Probe145 oProbe145 = default)
    {
        return oProbe145.Touch145();
    }

    public static void test145_Lent145()
    {
        using Probe145 oProbe145 = new Probe145().New("lent");

        HbRuntime.QOut("read", test145_Read145(oProbe145));

        return;

        // passed to a routine that keeps it: left to the collector
    }
    public static void test145_Keep145(Probe145 oProbe145 = default)
    {
        test145_soKept145 = oProbe145;
        return;
    }

    public static void test145_Kept145()
    {
        Probe145 oProbe145 = new Probe145().New("kept");

        test145_Keep145(oProbe145);
        HbRuntime.QOut("kept, not gone");

        return;

        // a parameter is its caller's: := NIL in the callee lets go of the
        // callee's reference only
    }
    public static void test145_Drop145(Probe145? oProbe145 = null)
    {
        oProbe145 = null;
        HbRuntime.QOut("dropped the parameter");
        return;
    }

    public static void test145_Borrowed145()
    {
        using Probe145 oProbe145 = new Probe145().New("borrowed");

        test145_Drop145(oProbe145);
        HbRuntime.QOut("still here", oProbe145.Touch145());

        return;
    }

    public static void Main(string[] args)
    {
        test145_AtEnd145();
        HbRuntime.QOut("after AtEnd145");
        test145_Released145();
        HbRuntime.QOut("after Released145");
        test145_Returned145();
        HbRuntime.QOut("after Returned145");
        test145_Lent145();
        HbRuntime.QOut("after Lent145");
        test145_Kept145();
        HbRuntime.QOut("after Kept145");
        test145_soKept145 = null;
        HbRuntime.hb_gcAll(true);
        HbRuntime.QOut("after releasing the kept one");
        test145_Borrowed145();
        HbRuntime.QOut("after Borrowed145");

        return;
    }
}
