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
public static partial class Program
{
    public static Dictionary<decimal, dynamic> test76_shPanels;
    public static Dictionary<string, dynamic> test76_shNames = new Dictionary<string, dynamic> { { "alpha", 1 }, { "beta", 2 } };
    public static void Main(string[] args)
    {
        Dictionary<decimal, dynamic> hById = new Dictionary<decimal, dynamic> {  };
        Dictionary<decimal, dynamic> hLit = new Dictionary<decimal, dynamic> { { 10, "ten" }, { 20, "twenty" } };

        test76_shPanels = BuildPanels();

        hById[7] = "seven";
        hById[8] = "eight";

        HbRuntime.QOut("a=", hLit[10]);
        HbRuntime.QOut("b=", hLit[20]);
        HbRuntime.QOut("c=", hById[7]);
        HbRuntime.QOut("d=", hById[8]);
        HbRuntime.QOut("e=", GetPanel(3));
        HbRuntime.QOut("f=", GetPanel(4));
        HbRuntime.QOut("g=", test76_shNames["alpha"] + test76_shNames["beta"]);
        HbRuntime.QOut("h=", HbRuntime.Len(test76_shPanels));

        return;

        // Factory in the CreateLangHash shape: its own keys flow through a
        // variable, so the literal gives no key evidence — the return-key
        // override from the `shPanels := BuildPanels()` site types it.
    }
    public static Dictionary<decimal, dynamic> BuildPanels()
    {
        Dictionary<decimal, dynamic> hOut = new Dictionary<decimal, dynamic> {  };
        int nKey = default;

        for (nKey = 1; nKey <= 5; nKey++)
        {
            hOut[nKey] = "panel" + HbRuntime.AllTrim(HbRuntime.Str(nKey));
        }

        return hOut;
    }

    public static dynamic GetPanel(decimal nNo = default)
    {
        return test76_shPanels[nNo];
    }
}
