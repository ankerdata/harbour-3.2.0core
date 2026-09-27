using System;
using static HbRuntime;
using static Program;

// Test 76: hash key-type inference (HASH / HASHC / HASHN family).
//
// Harbour hashes are keyed by strings or numerics; C# dictionaries
// must commit to one key type. The transpiler infers it per hash:
//   - literal keys type the literal (HASHC strings / HASHN numerics);
//   - `h[idx]` subscripts upgrade a weak keys-unknown HASH from the
//     index type (locals via the Pass-2 observation walker, file
//     statics via the gencsharp emit pre-pass);
//   - a factory function whose own keys are untypeable but whose
//     result lands in a key-typed static adopts the target's key type
//     (return-key override), including its returned local.
// Emission: HASHN → Dictionary<decimal, dynamic>, HASHC/HASH →
// Dictionary<string, dynamic>; empty `{ => }` initializers inherit
// the declared variable's key type.
//
// Since 2026-09-27 the name declares the key type (test127): the hashes
// keyed by numbers here are hn<...> / shn<...>.
public static partial class Program
{
    public static OrderedDictionary<long, dynamic> test76_shnPanels;
    public static OrderedDictionary<string, dynamic> test76_shNames = new OrderedDictionary<string, dynamic> { { "alpha", 1 }, { "beta", 2 } };
    public static void Main(string[] args)
    {
        OrderedDictionary<long, dynamic> hnById = new OrderedDictionary<long, dynamic> {  };
        OrderedDictionary<long, dynamic> hnLit = new OrderedDictionary<long, dynamic> { { 10, "ten" }, { 20, "twenty" } };

        test76_shnPanels = BuildPanels();

        hnById[7] = "seven";
        hnById[8] = "eight";

        HbRuntime.QOut("a=", hnLit[10]);
        HbRuntime.QOut("b=", hnLit[20]);
        HbRuntime.QOut("c=", hnById[7]);
        HbRuntime.QOut("d=", hnById[8]);
        HbRuntime.QOut("e=", GetPanel(3));
        HbRuntime.QOut("f=", GetPanel(4));
        HbRuntime.QOut("g=", test76_shNames["alpha"] + test76_shNames["beta"]);
        HbRuntime.QOut("h=", HbRuntime.Len(test76_shnPanels));

        return;

        // Factory in the CreateLangHash shape: its own keys flow through a
        // variable, so the literal gives no key evidence — the return-key
        // override from the `shnPanels := BuildPanels()` site types it.
    }
    public static OrderedDictionary<long, dynamic> BuildPanels()
    {
        OrderedDictionary<long, dynamic> hnOut = new OrderedDictionary<long, dynamic> {  };
        long nKey = default;

        for (nKey = 1; nKey <= 5; nKey++)
        {
            hnOut[nKey] = "panel" + HbRuntime.AllTrim(HbRuntime.Str(nKey));
        }

        return hnOut;
    }

    public static dynamic GetPanel(decimal nNo = default)
    {
        return test76_shnPanels[(long)(nNo)];
    }
}
