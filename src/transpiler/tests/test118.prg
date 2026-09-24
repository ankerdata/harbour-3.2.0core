// Test 118: a hash literal assigned to an integer-keyed hash takes its key type.
//
// A declaration's initializer already did: LOCAL hById := { => } on a hash
// indexed by numbers is an OrderedDictionary<long, dynamic>. Resetting it
// with hById := { => } emitted the string-keyed default instead, which C#
// cannot assign to it (CS0029) - easipos/jsonifystate.prg empties two such
// caches that way. Pinned for a local and for a file static, each filled,
// emptied and filled again.

STATIC shSeen118 := { => }

PROCEDURE Main()

   LOCAL hCount118 := { => }
   LOCAL nKey

   FOR nKey := 1 TO 3
      hCount118[ nKey ] := nKey * 10
      shSeen118[ nKey ] := .T.
   NEXT
   ? "filled:", Len( hCount118 ), Len( shSeen118 ), hCount118[ 2 ]

   hCount118 := { => }
   shSeen118 := { => }
   ? "emptied:", Len( hCount118 ), Len( shSeen118 )

   hCount118[ 7 ] := 70
   shSeen118[ 7 ] := .T.
   ? "again:", hCount118[ 7 ], hb_HHasKey( shSeen118, 7 ), Len( hCount118 ), Len( shSeen118 )

   RETURN
