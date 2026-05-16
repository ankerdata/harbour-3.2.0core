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

PROCEDURE Main()
   LOCAL cStr  := "Hello World"
   LOCAL hData := { "Alpha" => 1, "Beta" => 2 }

   ? "sub_hit="  + IIF( "World" $ cStr,  "Y", "N" )
   ? "sub_miss=" + IIF( "xyz" $ cStr,    "Y", "N" )
   ? "key_hit="  + IIF( "Alpha" $ hData, "Y", "N" )
   ? "key_miss=" + IIF( "Gamma" $ hData, "Y", "N" )
RETURN
