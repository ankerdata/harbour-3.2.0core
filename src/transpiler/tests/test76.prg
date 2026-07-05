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

static shPanels
static shNames := { "alpha" => 1, "beta" => 2 }

PROCEDURE Main()
    LOCAL hById := { => }
    LOCAL hLit  := { 10 => "ten", 20 => "twenty" }

    shPanels := BuildPanels()

    hById[7] := "seven"
    hById[8] := "eight"

    ? "a=", hLit[10]
    ? "b=", hLit[20]
    ? "c=", hById[7]
    ? "d=", hById[8]
    ? "e=", GetPanel(3)
    ? "f=", GetPanel(4)
    ? "g=", shNames["alpha"] + shNames["beta"]
    ? "h=", Len(shPanels)

RETURN

// Factory in the CreateLangHash shape: its own keys flow through a
// variable, so the literal gives no key evidence — the return-key
// override from the `shPanels := BuildPanels()` site types it.
function BuildPanels()
    local hOut := { => }
    local nKey

    for nKey := 1 to 5
        hOut[nKey] := "panel" + AllTrim(Str(nKey))
    next

return hOut

function GetPanel(nNo)
return shPanels[nNo]
