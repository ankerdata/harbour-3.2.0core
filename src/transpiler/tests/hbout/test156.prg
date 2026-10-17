#include "astype.ch"
// Test 156: typed hashes (plan C12 / C16). `AS HASH OF RECORD X` is a
// hash keyed by strings whose values are records, `AS INTEGER HASH OF
// CHARACTER` one keyed by whole numbers whose values are strings; C# gets
// OrderedDictionary<string, X> and OrderedDictionary<long, string>, the
// values typed in storage, as a FIXED array's are. A `{ k => v }` literal
// under the declaration is the dictionary of those types, each value a
// record's constructor; hb_HSet()'s value takes the type too. A read
// `h[ k ]` is the value, so `h[ k ][ FLD_LEN ]` is the record's field;
// FOR EACH over the hash types the loop variable. A key of the other
// kind is W0035, a value of another type W0041 (tests/errors/
// typed_hash.prg). Harbour sees AS HASH: astype.ch, and the -GT round
// trip, write the declarations so.

#include "../include/astype.ch"

#define FLD_NAME 1
#define FLD_LEN  3

STATIC shByName := {"PLUNO" => {"PLUNO", "N", 16, 0}, "CAPTION" => {"CAPTION", "C", 40, 0}} AS HASHC<FieldRow156>

PROCEDURE Main()
   LOCAL aTable := MakeTable156() AS OBJECT
   LOCAL hnNames := {1 => "one", 2 => "two"} AS HASHN<STRING>
   LOCAL aRow AS ARRAY
   LOCAL cKey AS STRING

   // a record into the hash: its constructor
   hb_HSet(shByName, "PRICE1", {"PRICE1", "N", 12, 3})
   // the value's field, by a define
   shByName["PRICE1"][FLD_LEN] := 15
   QOut(Len(shByName), shByName["CAPTION"][FLD_LEN], shByName["PRICE1"][3])
   // the values, typed
   FOR EACH aRow IN shByName
      QOut("  row", aRow[FLD_NAME], aRow[FLD_LEN])
   NEXT

   FOR EACH cKey IN hb_HKeys(shByName)
      QOut("  key", cKey, Width156(shByName[cKey]))
   NEXT

   hnNames[3] := "three"
   QOut(Len(hnNames), hnNames[2], hnNames[3])
   QOut(aTable[1], Len(aTable[2]), aTable[2]["CAPTION"][FLD_NAME], aTable[3][2])
   QOut(Count156(aTable[2]), Count156(shByName))
RETURN

   // a record holding typed hashes, built from one literal
FUNCTION MakeTable156() AS ARRAY
RETURN {"PLU", {"CAPTION" => {"CAPTION", "C", 40, 0}}, {1 => "a", 2 => "b"}}

FUNCTION Width156( aRow AS ARRAY )
RETURN aRow[FLD_LEN] + aRow[4]

   // a typed hash parameter: FOR EACH over its values
FUNCTION Count156( hRows AS HASHC<FieldRow156> ) AS NUMERIC
   LOCAL iTotal := 0 AS NUMERIC
   LOCAL aRow AS OBJECT
   FOR EACH aRow IN hRows
      iTotal += aRow[FLD_LEN]
   NEXT

RETURN iTotal
