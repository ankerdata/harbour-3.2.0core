using System;
using static HbRuntime;
using static Program;

// Test 55: the `$` (IN) operator — substring containment when the
// right operand is a string, key containment when it's a hash.
//
// Harbour overloads `$` by operand type: `a $ b` is a substring
// test when b is a string, a key-existence test when b is a hash.
// The C# emit can't use `b.Contains(a)` for both — a
// Dictionary<string,dynamic> has no .Contains(), only
// .ContainsKey(). HbRuntime.HbIn dispatches on the runtime type
// of b: string -> substring, IDictionary -> key.
//
// Before HbIn, every `"key" $ hHash` produced CS1929 (24 of them
// in jsonupdates.prg alone). This test pins both branches.
public static partial class Program
{
    public static void Main(string[] args)
    {
        string cStr = "Hello World";
        Dictionary<string, dynamic> hData = new Dictionary<string, dynamic> { { "Alpha", 1 }, { "Beta", 2 } };

        HbRuntime.QOut("sub_hit=" + (HbRuntime.HbIn("World", cStr) ? "Y" : "N"));
        HbRuntime.QOut("sub_miss=" + (HbRuntime.HbIn("xyz", cStr) ? "Y" : "N"));
        HbRuntime.QOut("key_hit=" + (HbRuntime.HbIn("Alpha", hData) ? "Y" : "N"));
        HbRuntime.QOut("key_miss=" + (HbRuntime.HbIn("Gamma", hData) ? "Y" : "N"));
        return;
    }
}
