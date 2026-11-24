// Test 191: an INLINE method's parameters are typed by their names, as a
// method's are: `cLabel` a string, `iCount` a whole number, `xExtra`
// dynamic. The C# method declares them so, and the reftab row the scan
// checks a caller's arguments against has the same types.
public class Tally191 : IHbObject
{

    public string Label191(string cLabel = default, long iCount = default, dynamic xExtra = default) =>  cLabel + "=" + LTrim( Str( iCount ) ) + (( xExtra == null) ? ( "") : ( xExtra )) ;
    public virtual Tally191 New()
    {
        return this;
    }
}

public static partial class Program
{
    public static void Main(string[] args)
    {
        Tally191 oTally191 = new Tally191().New();

        QOut(oTally191.Label191("cups", 3));
        QOut(oTally191.Label191("jars", 12, "!"));
    }
}
