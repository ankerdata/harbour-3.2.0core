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

PROCEDURE Main()

   LOCAL hcStock := { "tea" => 1, "milk" => 2, "rusk" => 3 }
   LOCAL hnById := { => }
   LOCAL hcSrc := { "a" => { 1, 2 }, "b" => { "x" => 1 } }
   LOCAL hcCopy
   LOCAL aSrc := { { "k" => 1 } }
   LOCAL aCopy
   LOCAL nId := 7
   LOCAL cKey
   LOCAL cKeys := ""

   hb_HDel( hcStock, "milk" )
   hcStock[ "salt" ] := 4
   FOR EACH cKey IN hb_HKeys( hcStock )
      cKeys += cKey + " "
   NEXT
   ? cKeys
   ? hb_HKeyAt( hcStock, 2 ), hb_HPos( hcStock, "salt" ), hb_HPos( hcStock, "milk" )
   hb_HSet( hcStock, "tea", 9 )
   ? hb_HGet( hcStock, "tea" ), hb_HKeyAt( hcStock, 1 ), Len( hcStock )
   ? "tea" $ hcStock, "milk" $ hcStock, "" $ "abc", "b" $ "abc"
   ? hb_HKeepOrder( hcStock, .T. )

   hnById[ nId ] := "seven"
   hnById[ 3 ] := "three"
   ? hnById[ nId ], hnById[ 3 ], hb_HHasKey( hnById, 7 ), hb_HHasKey( hnById, 7.0 )
   ? hb_HGetDef( hnById, 4, "none" ), hb_HKeyAt( hnById, 1 ) + 1

   hcCopy := hb_HClone( hcSrc )
   hcCopy[ "a" ][ 1 ] := 9
   hcCopy[ "b" ][ "x" ] := 5
   ? hcSrc[ "a" ][ 1 ], hcSrc[ "b" ][ "x" ], hcCopy[ "a" ][ 1 ], hcCopy[ "b" ][ "x" ]

   aCopy := AClone( aSrc )
   aCopy[ 1 ][ "k" ] := 2
   ? aSrc[ 1 ][ "k" ], aCopy[ 1 ][ "k" ]

RETURN
