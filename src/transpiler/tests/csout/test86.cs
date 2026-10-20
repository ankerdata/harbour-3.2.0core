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
public class Store : IHbObject
{

    public virtual decimal Fill(ref List<dynamic> aOut)
    {
        aOut = new List<dynamic> { "x", "y", "z" };

        return Len(aOut);
    }
}

public static partial class Program
{
    public static Store test86_soStore;
    public static Store GetStore()
    {
        return test86_soStore;
    }

    public static dynamic Wrap(ref List<dynamic> aOut)
    {
        return GetStore().Fill(ref aOut);
    }

    public static void Main(string[] args)
    {
        List<dynamic> aData = new List<dynamic> {  };
        decimal nCount = default;

        test86_soStore = new Store();

        nCount = Wrap(ref aData);
        QOut("n=", nCount);
        QOut("len=", Len(aData));
        if (Len(aData) >= 3)
        {
            QOut("vals=", aData[Ix(1)] + aData[Ix(2)] + aData[Ix(3)]);
        }

        return;
    }
}
