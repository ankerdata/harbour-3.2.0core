using System;
using static HbRuntime;
using static Program;

// Test 86: `@` array parameter forwarded into a typed-receiver method.
//
// Store:Fill() reassigns its by-ref parameter (aOut := {...}). Wrap()
// never touches aOut itself — it only forwards it with `@` into
// GetStore():Fill(). For the caller to see the new array, Wrap's own
// parameter must still emit `ref`.
//
// Getting that right means resolving GetStore():Fill(@aOut) to the
// reftab key Store::Store__Fill. The scanner used to key only Self:
// and ::method() sends, so Wrap's parameter went unmarked, its `ref`
// was elided as redundant (W0023), and Fill's new array landed in
// Wrap's local where the caller never saw it: len= printed 0 instead
// of 3, and only the C# side was wrong — the Harbour output was right
// throughout, so nothing but a prg/cs comparison catches it.

// #include "hbclass.ch"
public class Store
{

    public decimal Fill(ref dynamic[] aOut)
    {
        aOut = new dynamic[] { "x", "y", "z" };

        return HbRuntime.Len(aOut);
    }
}

public static partial class Program
{
    public static Store test86_soStore;
    public static Store GetStore()
    {
        return test86_soStore;
    }

    public static dynamic Wrap(ref dynamic[] aOut)
    {
        return GetStore().Fill(ref aOut);
    }

    public static void Main(string[] args)
    {
        dynamic[] aData = new dynamic[] {  };
        decimal nCount = default;

        test86_soStore = new Store();

        nCount = Wrap(ref aData);
        HbRuntime.QOut("n=", nCount);
        HbRuntime.QOut("len=", HbRuntime.Len(aData));
        if (HbRuntime.Len(aData) >= 3)
        {
            HbRuntime.QOut("vals=", aData[0] + aData[1] + aData[2]);
        }

        return;
    }
}
