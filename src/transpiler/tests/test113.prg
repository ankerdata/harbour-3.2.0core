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

PROCEDURE Main()

   LOCAL hStock := { "tea" => 1, "milk" => 2, "rusk" => 3 }
   LOCAL hById := { => }
   LOCAL hSrc := { "a" => { 1, 2 }, "b" => { "x" => 1 } }
   LOCAL hCopy
   LOCAL aSrc := { { "k" => 1 } }
   LOCAL aCopy
   LOCAL nId := 7
   LOCAL cKey
   LOCAL cKeys := ""

   hb_HDel( hStock, "milk" )
   hStock[ "salt" ] := 4
   FOR EACH cKey IN hb_HKeys( hStock )
      cKeys += cKey + " "
   NEXT
   ? cKeys
   ? hb_HKeyAt( hStock, 2 ), hb_HPos( hStock, "salt" ), hb_HPos( hStock, "milk" )
   hb_HSet( hStock, "tea", 9 )
   ? hb_HGet( hStock, "tea" ), hb_HKeyAt( hStock, 1 ), Len( hStock )
   ? "tea" $ hStock, "milk" $ hStock, "" $ "abc", "b" $ "abc"
   ? hb_HKeepOrder( hStock, .T. )

   hById[ nId ] := "seven"
   hById[ 3 ] := "three"
   ? hById[ nId ], hById[ 3 ], hb_HHasKey( hById, 7 ), hb_HHasKey( hById, 7.0 )
   ? hb_HGetDef( hById, 4, "none" ), hb_HKeyAt( hById, 1 ) + 1

   hCopy := hb_HClone( hSrc )
   hCopy[ "a" ][ 1 ] := 9
   hCopy[ "b" ][ "x" ] := 5
   ? hSrc[ "a" ][ 1 ], hSrc[ "b" ][ "x" ], hCopy[ "a" ][ 1 ], hCopy[ "b" ][ "x" ]

   aCopy := AClone( aSrc )
   aCopy[ 1 ][ "k" ] := 2
   ? aSrc[ 1 ][ "k" ], aCopy[ 1 ][ "k" ]

RETURN
