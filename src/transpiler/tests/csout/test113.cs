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
//
// Since 2026-09-27 the name declares the key type (test127): the hashes
// keyed by numbers here are hn<...> / shn<...>.
public static partial class Program
{
    public static void Main(string[] args)
    {
        var hcStock = new OrderedDictionary<string, dynamic>
        {
            { "tea", 1 },
            { "milk", 2 },
            { "rusk", 3 }
        };
        var hnById = new OrderedDictionary<long, dynamic>();
        var hcSrc = new OrderedDictionary<string, dynamic>
        {
            { "a", new List<dynamic> { 1, 2 } },
            { "b", new OrderedDictionary<string, dynamic> { { "x", 1 } } }
        };
        var aSrc = new List<dynamic>
        {
            new OrderedDictionary<string, dynamic> { { "k", 1 } }
        };
        long nId = 7;
        string cKeys = "";

        hb_HDel(hcStock, "milk");
        hcStock["salt"] = 4;
        foreach (string cKey in HbEnumValues(hb_HKeys(hcStock)))
        {
            cKeys += cKey + " ";
        }

        QOut(cKeys);
        QOut(hb_HKeyAt(hcStock, 2), hb_HPos(hcStock, "salt"), hb_HPos(hcStock, "milk"));
        hb_HSet(hcStock, "tea", 9);
        QOut(hb_HGet(hcStock, "tea"), hb_HKeyAt(hcStock, 1), Len(hcStock));
        QOut(HbIn("tea", hcStock), HbIn("milk", hcStock), HbIn("", "abc"), HbIn("b", "abc"));
        QOut(hb_HKeepOrder(hcStock, true));

        hnById[nId] = "seven";
        hnById[3] = "three";
        QOut(hnById[nId], hnById[3], hb_HHasKey(hnById, 7), hb_HHasKey(hnById, 7.0m));
        QOut(hb_HGetDef(hnById, 4, "none"), hb_HKeyAt(hnById, 1) + 1);

        OrderedDictionary<string, dynamic> hcCopy = hb_HClone(hcSrc);
        hcCopy["a"][I(1)] = 9;
        hcCopy["b"]["x"] = 5;
        QOut(hcSrc["a"][I(1)], hcSrc["b"]["x"], hcCopy["a"][I(1)], hcCopy["b"]["x"]);

        List<dynamic> aCopy = AClone(aSrc);
        aCopy[I(1)]["k"] = 2;
        QOut(aSrc[I(1)]["k"], aCopy[I(1)]["k"]);
    }
}
