// Test 160: a LOCAL initialised with one `new` of its declared type is
// `var` in C#; an empty array or hash literal is the type's empty
// instance, `new List<dynamic>()`, `new OrderedDictionary<…>()` and a
// FIXED array's `System.Array.Empty<T>()`.
//
// `var`: an array literal, a hash literal, an empty one of each, a class's
// New(). The type written out: a scalar, a function's result, a call on
// the new object, an empty FIXED array.
public class Box160 : IHbObject
{
    public List<dynamic> aItems = new List<dynamic>();

    public dynamic Count() => Len( aItems );
}

public static partial class Program
{
    public static void Main(string[] args)
    {
        QOut(Literals160());
        QOut(Objects160());
        QOut(Fixed160());
    }

    public static dynamic Literals160()
    {
        var aNames = new List<dynamic> { "a", "b" };
        var aEmpty = new List<dynamic>();
        var hAges = new OrderedDictionary<string, dynamic>
        {
            { "x", 1 },
            { "y", 2 }
        };
        var hNone = new OrderedDictionary<string, dynamic>();
        string cJoined = "";
        List<dynamic> aCopy = AClone(aNames);

        AAdd(aEmpty, "c");
        hNone["z"] = 3;
        AEval(aNames, ((Func<dynamic, dynamic>)((c) => cJoined += c)));

        return cJoined + aEmpty[I(1)] + Str(hAges["y"] + hNone["z"], 2) + Str(Len(aCopy), 2);
    }

    public static string Objects160()
    {
        var oBox160 = new Box160();
        decimal nItems = new Box160().Count();

        AAdd(oBox160.aItems, "one");

        return Str(oBox160.Count(), 2) + Str(nItems, 2);
    }

    public static string Fixed160()
    {
        string[] aNone = System.Array.Empty<string>();

        return Str(Len(aNone), 2);
    }
}
