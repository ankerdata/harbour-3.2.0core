#include "astype.ch"
// Test 118: a hash literal assigned to an integer-keyed hash takes its key type.
//
// A declaration's initializer already did: LOCAL hById := { => } on a hash
// indexed by numbers is an OrderedDictionary<long, dynamic>. Resetting it
// with hById := { => } emitted the string-keyed default instead, which C#
// cannot assign to it (CS0029) - easipos/jsonifystate.prg empties two such
// caches that way. Pinned for a local and for a file static, each filled,
// emptied and filled again.
//
// Since 2026-09-27 the name declares the key type (test127): the hashes
// keyed by numbers here are hn<...> / shn<...>.

STATIC shnSeen118 := {=>} AS HASH

PROCEDURE Main()

   LOCAL hnCount118 := {=>} AS HASH
   LOCAL nKey AS NUMERIC

   FOR nKey := 1 TO 3
      hnCount118[nKey] := nKey * 10
      shnSeen118[nKey] := .T.
   NEXT

   QOut("filled:", Len(hnCount118), Len(shnSeen118), hnCount118[2])

   hnCount118 := {=>}
   shnSeen118 := {=>}
   QOut("emptied:", Len(hnCount118), Len(shnSeen118))

   hnCount118[7] := 70
   shnSeen118[7] := .T.
   QOut("again:", hnCount118[7], hb_HHasKey(shnSeen118, 7), Len(hnCount118), Len(shnSeen118))

RETURN
