// Test 168: a list of records is typed storage. AS ARRAY OF RECORD X is a
// C# List<X>, as a list of strings is a List<string>: a record has no
// subclasses, so nothing a List<X> holds needs a cast. Its literal's rows
// and AAdd()'s row are the record's constructor, a parameter given one
// becomes one, a FOR EACH variable and an AScan() block's parameter are the
// record, and Array( n ) makes n empty places.
public sealed class Port168
{
    public string cName;
    public long iSpeed;
    public bool lOpen;

    public Port168(string cName, long iSpeed, bool lOpen)
    {
        this.cName = cName;
        this.iSpeed = iSpeed;
        this.lOpen = lOpen;
    }
}

public static partial class Program
{
    public static void Main(string[] args)
    {
        var aPorts = new List<Port168>
        {
            new Port168("COM1", 9600, false)
        };

        AAdd(aPorts, new Port168("COM2", 19200, true));
        aPorts[I(1)].lOpen = true;

        for (long i = 1; i <= Len(aPorts); i++)
        {
            QOut(aPorts[I(i)].cName, aPorts[I(i)].iSpeed, aPorts[I(i)].lOpen);
        }

        QOut(Fastest168(aPorts), CountOpen168(aPorts));
        QOut(AScan(aPorts, ((Func<Port168, bool>)((aRow) => aRow.cName == "COM2"))));

        foreach (Port168 aPort in HbEnumValues(aPorts))
        {
            QOut(aPort.cName);
        }

        List<Port168> aSpare = ArrayOf<Port168>(2);
        aSpare[I(2)] = new Port168("LPT1", 0, false);
        QOut(Len(aSpare), aSpare[I(1)] == null, aSpare[I(2)].cName);

        // A parameter its caller gives the list is the list
    }
    public static long Fastest168(List<Port168> aList = default)
    {
        long iBest = 0;

        for (long i = 1; i <= Len(aList); i++)
        {
            iBest = (long)(Max(iBest, aList[I(i)].iSpeed));
        }

        return iBest;
    }

    public static long CountOpen168(List<Port168> aList = default)
    {
        long iOpen = 0;

        foreach (Port168 aPort in HbEnumValues(aList))
        {
            if (aPort.lOpen)
            {
                iOpen++;
            }
        }

        return iOpen;
    }
}
