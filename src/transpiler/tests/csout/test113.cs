using System;
using static HbRuntime;
using static Program;

// Test 113: a hash keeps Harbour's order and finds its keys by value.
//
// A Harbour hash keeps its keys in the order they were added, deletes
// included (HB_HASH_KEEPORDER, the default). A .NET Dictionary reuses a
// deleted key's slot, so after hb_HDel and a new key hb_HKeys and FOR
// EACH saw another order; the emitted type is now .NET 9's
// OrderedDictionary<string, dynamic>, and a numeric-keyed hash (HASHN)
// OrderedDictionary<long, dynamic>, its key cast at the subscript.
// HbRuntime converts a key to the hash's own key type, so 7 and 7.0
// find the same entry; hb_HKeyAt / hb_HPos / hb_HSet / hb_HGet /
// hb_HClone are implemented (a clone copies nested arrays and hashes, as
// AClone now does too), and `$` finds nothing for an empty string.
public static partial class Program
{
    public static void Main(string[] args)
    {
        OrderedDictionary<string, dynamic> hStock = new OrderedDictionary<string, dynamic> { { "tea", 1 }, { "milk", 2 }, { "rusk", 3 } };
        OrderedDictionary<long, dynamic> hById = new OrderedDictionary<long, dynamic> {  };
        OrderedDictionary<string, dynamic> hSrc = new OrderedDictionary<string, dynamic>
        {
            { "a", new dynamic[] { 1, 2 } },
            { "b", new OrderedDictionary<string, dynamic> { { "x", 1 } } }
        };
        OrderedDictionary<string, dynamic> hCopy = default;
        dynamic[] aSrc = new dynamic[] { new OrderedDictionary<string, dynamic> { { "k", 1 } } };
        dynamic[] aCopy = default;
        long nId = 7;
        string cKey = default;
        string cKeys = "";

        HbRuntime.hb_HDel(hStock, "milk");
        hStock["salt"] = 4;
        foreach (dynamic __hb_fe_cKey in HbRuntime.HbEnumValues(HbRuntime.hb_HKeys(hStock)))
        {
            cKey = __hb_fe_cKey;
            cKeys += cKey + " ";
        }

        HbRuntime.QOut(cKeys);
        HbRuntime.QOut(HbRuntime.hb_HKeyAt(hStock, 2), HbRuntime.hb_HPos(hStock, "salt"), HbRuntime.hb_HPos(hStock, "milk"));
        HbRuntime.hb_HSet(hStock, "tea", 9);
        HbRuntime.QOut(HbRuntime.hb_HGet(hStock, "tea"), HbRuntime.hb_HKeyAt(hStock, 1), HbRuntime.Len(hStock));
        HbRuntime.QOut(HbRuntime.HbIn("tea", hStock), HbRuntime.HbIn("milk", hStock), HbRuntime.HbIn("", "abc"), HbRuntime.HbIn("b", "abc"));
        HbRuntime.QOut(HbRuntime.hb_HKeepOrder(hStock, true));

        hById[nId] = "seven";
        hById[3] = "three";
        HbRuntime.QOut(hById[nId], hById[3], HbRuntime.hb_HHasKey(hById, 7), HbRuntime.hb_HHasKey(hById, 7.0m));
        HbRuntime.QOut(HbRuntime.hb_HGetDef(hById, 4, "none"), HbRuntime.hb_HKeyAt(hById, 1) + 1);

        hCopy = HbRuntime.hb_HClone(hSrc);
        hCopy["a"][0] = 9;
        hCopy["b"]["x"] = 5;
        HbRuntime.QOut(hSrc["a"][0], hSrc["b"]["x"], hCopy["a"][0], hCopy["b"]["x"]);

        aCopy = HbRuntime.AClone(aSrc);
        aCopy[0]["k"] = 2;
        HbRuntime.QOut(aSrc[0]["k"], aCopy[0]["k"]);

        return;
    }
}
