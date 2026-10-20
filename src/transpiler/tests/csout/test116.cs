// Test 116: a core function whose C# return type says nothing.
//
// hbfuncs.tab gives each HbRuntime function the return type of its C#
// signature, and "-" for one returning dynamic, void or a class. Such a
// call used to type the local it initialises as USUAL whatever the
// local's Hungarian prefix said, so hCopy := hb_HClone( h ) was dynamic,
// not a hash. A "-" says nothing now and the prefix decides: a hash, an
// array, a number, and dynamic only for an x name. A typed row still types
// the call: hb_ntos() is STRING.
public static partial class Program
{
    public static void Main(string[] args)
    {
        OrderedDictionary<string, dynamic> h = new OrderedDictionary<string, dynamic>
        {
            { "a", 1 },
            { "b", 2 }
        };
        OrderedDictionary<string, dynamic> hCopy = hb_HClone(h);
        List<dynamic> aKeys = hb_HKeys(h);
        decimal nB = hb_HGetDef(h, "b", 0);
        dynamic xAny = hb_HGetDef(h, "c", "none");
        string cText = hb_ntos(nB + 1);
        string cKey;

        hCopy["c"] = 3;
        QOut("clone:", hb_ntos(Len(h)), hb_ntos(Len(hCopy)));
        QOut("keys:");
        foreach (string __hb_fe_cKey in HbEnumValues(aKeys))
        {
            cKey = __hb_fe_cKey;
            QQOut(" " + cKey);
        }

        QOut("nB + 1:", cText);
        QOut("xAny:", xAny);
    }
}
