using System;
using static HbRuntime;
using static Program;

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
        OrderedDictionary<string, dynamic> h = new OrderedDictionary<string, dynamic> { { "a", 1 }, { "b", 2 } };
        OrderedDictionary<string, dynamic> hCopy = HbRuntime.hb_HClone(h);
        dynamic[] aKeys = HbRuntime.hb_HKeys(h);
        decimal nB = HbRuntime.hb_HGetDef(h, "b", 0);
        dynamic xAny = HbRuntime.hb_HGetDef(h, "c", "none");
        string cText = HbRuntime.hb_ntos(nB + 1);
        string cKey = default;

        hCopy["c"] = 3;
        HbRuntime.QOut("clone:", HbRuntime.hb_ntos(HbRuntime.Len(h)), HbRuntime.hb_ntos(HbRuntime.Len(hCopy)));
        HbRuntime.QOut("keys:");
        foreach (dynamic __hb_fe_cKey in aKeys)
        {
            cKey = __hb_fe_cKey;
            HbRuntime.QQOut(" " + cKey);
        }

        HbRuntime.QOut("nB + 1:", cText);
        HbRuntime.QOut("xAny:", xAny);

        return;
    }
}
