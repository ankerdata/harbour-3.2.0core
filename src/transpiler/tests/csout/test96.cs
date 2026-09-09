using System;
using static HbRuntime;
using static Program;

// Test 96: FOR EACH with the enumerator messages.
//
// Harbour's FOR EACH binds the variable to the VALUE and exposes the
// key through `x:__enumKey()`. C# iterating a Dictionary yields pairs,
// so a loop whose body reads an enumerator message runs over
// HbRuntime.HbEnumPairs — (key, value) for a hash, (1-based index,
// element) for an array — with the variable bound to the pair's Value
// and `__enumKey()` / `__enumValue()` reading the pair. A loop that
// never asks for the key is emitted as before.
public static partial class Program
{
    public static void Main(string[] args)
    {
        Dictionary<string, dynamic> hAges = new Dictionary<string, dynamic> { { "ann", 31 }, { "bob", 42 } };
        dynamic xItem = default;
        string cOut = "";
        decimal nTotal = 0;

        foreach (var __hb_kv_xItem in HbRuntime.HbEnumPairs(hAges))
        {
            xItem = __hb_kv_xItem.Value;
            cOut += __hb_kv_xItem.Key + "=" + HbRuntime.LTrim(HbRuntime.Str(xItem)) + " ";
        }

        HbRuntime.QOut(cOut);

        foreach (var __hb_kv_xItem in HbRuntime.HbEnumPairs(hAges))
        {
            xItem = __hb_kv_xItem.Value;
            nTotal += __hb_kv_xItem.Value;
        }

        HbRuntime.QOut("total=" + HbRuntime.LTrim(HbRuntime.Str(nTotal)));

        // no message: emitted as before
        foreach (dynamic __hb_fe_xItem in HbRuntime.HbEnumValues(hAges))
        {
            xItem = __hb_fe_xItem;
            nTotal += xItem;
        }

        HbRuntime.QOut("twice=" + HbRuntime.LTrim(HbRuntime.Str(nTotal)));
        return;
    }
}
