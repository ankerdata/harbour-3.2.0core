// Test 185: a method that answers a value of its default's type.
// A method declares a type parameter as a function does: Take185() is
// `T Take185<T>( string cKey, T xDefault )` in a class that is not
// generic itself, and a subclass's method that calls it through Self or
// through an object gets a string for "" and a whole number for 0.
public class Answers185 : IHbObject
{
    public OrderedDictionary<string, dynamic> hcResult185;

    public virtual Answers185 New()
    {
        hcResult185 = new OrderedDictionary<string, dynamic>
        {
            { "name", "Ann" },
            { "count", 3 },
            { "rate", 1.25m },
            { "ids", new List<dynamic> { 4, 5 } }
        };

        return this;
    }

    public virtual T Take185<T>(string cKey = default, T xDefault = default)
    {
        if (hb_HHasKey(hcResult185, cKey))
        {
            return HbAs<T>(hcResult185[cKey]);
        }

        return xDefault;
    }

    public virtual string Name185()
    {
        return Take185("name", "");
    }

    public virtual long Count185()
    {
        return (long)(Take185("count", 0L));
    }
}

public static partial class Program
{
    public static void Main(string[] args)
    {
        Answers185 oAnswers185 = new Answers185().New();

        QOut(Upper(oAnswers185.Name185()), oAnswers185.Count185() + 1);

        string cName185 = oAnswers185.Take185("name", "");
        decimal nRate185 = oAnswers185.Take185("rate", 0.00m);
        List<dynamic> aIds185 = oAnswers185.Take185("ids", new List<dynamic>());
        QOut(Len(cName185), nRate185 * 4, Len(aIds185));

        QOut(oAnswers185.Take185("missing", "none"), oAnswers185.Take185("missing", -1L), oAnswers185.Take185("missing", true));
    }
}
