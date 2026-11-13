// Test 173: a list of logicals is typed storage. AS ARRAY OF LOGICAL is a
// C# List<bool>, on a local, a member, a parameter and a function's
// result, and a name says it too, `al` before a capital (`alDone`, a
// static's `sal`), as `ac`, `ai` and `an` do. Its literal, AAdd(), an
// element read as a condition and an element written are checked by the
// C#, where a List<dynamic> made each read a run-time conversion.
public class Switches173 : IHbObject
{
    public List<bool> aOn = new List<bool>();

    public virtual Switches173 Add173(bool lOn = default)
    {
        AAdd(aOn, lOn);

        return this;
    }

    public virtual long CountOn173()
    {
        long iCount = 0;

        foreach (bool lOn in HbEnumValues(aOn))
        {
            if (lOn)
            {
                iCount++;
            }
        }

        return iCount;
    }
}

public static partial class Program
{
    public static List<bool> Flags173(bool lAll = default)
    {
        return new List<bool> { lAll, true, !lAll };
    }

    public static bool AnyOn173(List<bool> aFlags = default)
    {
        for (long i = 1; i <= Len(aFlags); i++)
        {
            if (aFlags[I(i)])
            {
                return true;
            }
        }

        return false;
    }

    public static void Main(string[] args)
    {
        List<bool> aFlags = Flags173(false);
        var oSwitches173 = new Switches173();
        List<bool> alDone = Flags173(true);

        QOut(Len(aFlags), aFlags[I(1)], aFlags[I(2)], aFlags[I(3)]);
        aFlags[I(1)] = true;
        AAdd(aFlags, false);
        QOut(Len(aFlags), aFlags[I(1)] && !aFlags[I(4)]);
        QOut(AnyOn173(aFlags), AnyOn173(new List<bool> { false, false }));

        oSwitches173.Add173(true).Add173(false).Add173(aFlags[I(2)]);
        QOut(oSwitches173.CountOn173());

        alDone[I(3)] = alDone[I(1)] && alDone[I(2)];
        AAdd(alDone, !alDone[I(3)]);
        QOut(Len(alDone), alDone[I(3)], alDone[I(4)], AnyOn173(alDone));
    }
}
