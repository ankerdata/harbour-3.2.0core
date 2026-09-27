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

static shnPanels
static shNames := { "alpha" => 1, "beta" => 2 }

PROCEDURE Main()
    LOCAL hnById := { => }
    LOCAL hnLit  := { 10 => "ten", 20 => "twenty" }

    shnPanels := BuildPanels()

    hnById[7] := "seven"
    hnById[8] := "eight"

    ? "a=", hnLit[10]
    ? "b=", hnLit[20]
    ? "c=", hnById[7]
    ? "d=", hnById[8]
    ? "e=", GetPanel(3)
    ? "f=", GetPanel(4)
    ? "g=", shNames["alpha"] + shNames["beta"]
    ? "h=", Len(shnPanels)

RETURN

// Factory in the CreateLangHash shape: its own keys flow through a
// variable, so the literal gives no key evidence — the return-key
// override from the `shnPanels := BuildPanels()` site types it.
function BuildPanels()
    local hnOut := { => }
    local nKey

    for nKey := 1 to 5
        hnOut[nKey] := "panel" + AllTrim(Str(nKey))
    next

return hnOut

function GetPanel(nNo)
return shnPanels[nNo]
