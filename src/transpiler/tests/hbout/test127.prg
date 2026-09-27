#include "astype.ch"
// Test 127: a hash keyed by numbers is named hn<...> (Alex, 2026-09-27).
//
// C# commits a dictionary to one key type. The inference used to guess it
// from the keys the code happened to use, and the guess went wrong where a
// key came from a macro or an `x` value (easipos languages.prg's lang
// tables). The name declares it now: h<...> is keyed by strings, the
// default, and hn<...> / shn<...> by numbers, an OrderedDictionary<long,
// dynamic> - for a local, a static, a member and a parameter alike. A key
// of the other kind is W0035 (tests/errors/hash_key_name.prg); a key of no
// known type is taken as the name says.

#include "hbclass.ch"

STATIC shnSeen127 := {=>} AS HASH

CLASS Ledger127

   DATA hnRows127 AS HASHN
   METHOD Fill127()

ENDCLASS

METHOD Fill127() AS OBJECT CLASS Ledger127
   ::hnRows127 := {=>}
   ::hnRows127[12] := "twelve"
RETURN Self

STATIC FUNCTION Count127( hnFrom AS HASH ) AS NUMERIC
RETURN Len(hnFrom)

PROCEDURE Main()
   LOCAL hnById := {=>} AS HASH
   LOCAL hByName := {=>} AS HASH
   LOCAL oLedger127 := Ledger127():New():Fill127() AS OBJECT
   LOCAL nId := 7 AS NUMERIC
   // a key of no known type: Eval() answers whatever its block does
   LOCAL xKey := Eval({|| Seven127() + 2}) AS USUAL

   hnById[nId] := "seven"
   hnById[xKey] := "nine"
   hByName["seven"] := nId
   shnSeen127[3] := .T.

   QOut(hnById[7], hnById[9], hByName["seven"], Count127(hnById))
   QOut(oLedger127:hnRows127[12], hb_HHasKey(shnSeen127, 3), Len(shnSeen127))

RETURN

STATIC FUNCTION Seven127() AS NUMERIC
RETURN 7
