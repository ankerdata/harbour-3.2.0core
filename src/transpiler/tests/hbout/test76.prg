#include "astype.ch"
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

STATIC shnPanels AS HASH
STATIC shNames := {"alpha" => 1, "beta" => 2} AS HASH

PROCEDURE Main()
   LOCAL hnById := {=>} AS HASH
   LOCAL hnLit := {10 => "ten", 20 => "twenty"} AS HASH

   shnPanels := BuildPanels()

   hnById[7] := "seven"
   hnById[8] := "eight"

   QOut("a=", hnLit[10])
   QOut("b=", hnLit[20])
   QOut("c=", hnById[7])
   QOut("d=", hnById[8])
   QOut("e=", GetPanel(3))
   QOut("f=", GetPanel(4))
   QOut("g=", shNames["alpha"] + shNames["beta"])
   QOut("h=", Len(shnPanels))

RETURN

   // Factory in the CreateLangHash shape: its own keys flow through a
   // variable, so the literal gives no key evidence — the return-key
   // override from the `shnPanels := BuildPanels()` site types it.
FUNCTION BuildPanels() AS HASH
   LOCAL hnOut := {=>} AS HASH
   LOCAL nKey AS NUMERIC

   FOR nKey := 1 TO 5
      hnOut[nKey] := "panel" + AllTrim(Str(nKey))
   NEXT

RETURN hnOut

FUNCTION GetPanel( nNo AS NUMERIC )
RETURN shnPanels[nNo]
