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

RECORD FieldRow156( cName, cType, iLen, iDec )
RECORD Table156( cAlias, hFields AS HASH OF RECORD FieldRow156, hnNames AS INTEGER HASH OF CHARACTER )

STATIC shByName AS HASH OF RECORD FieldRow156 := { ;
   "PLUNO" => { "PLUNO", "N", 16, 0 }, ;
   "CAPTION" => { "CAPTION", "C", 40, 0 } }

PROCEDURE Main()
   LOCAL aTable := MakeTable156()
   LOCAL hnNames AS INTEGER HASH OF CHARACTER := { 1 => "one", 2 => "two" }
   LOCAL aRow AS RECORD FieldRow156
   LOCAL cKey

   hb_HSet( shByName, "PRICE1", { "PRICE1", "N", 12, 3 } )   // a record into the hash: its constructor
   shByName[ "PRICE1" ][ FLD_LEN ] := 15                       // the value's field, by a define
   ? Len( shByName ), shByName[ "CAPTION" ][ FLD_LEN ], shByName[ "PRICE1" ][ 3 ]
   FOR EACH aRow IN shByName                                   // the values, typed
      ? "  row", aRow[ FLD_NAME ], aRow[ FLD_LEN ]
   NEXT
   FOR EACH cKey IN hb_HKeys( shByName )
      ? "  key", cKey, Width156( shByName[ cKey ] )
   NEXT
   hnNames[ 3 ] := "three"
   ? Len( hnNames ), hnNames[ 2 ], hnNames[ 3 ]
   ? aTable[ 1 ], Len( aTable[ 2 ] ), aTable[ 2 ][ "CAPTION" ][ FLD_NAME ], aTable[ 3 ][ 2 ]
   ? Count156( aTable[ 2 ] ), Count156( shByName )
RETURN

// a record holding typed hashes, built from one literal
FUNCTION MakeTable156() AS RECORD Table156
RETURN { "PLU", { "CAPTION" => { "CAPTION", "C", 40, 0 } }, { 1 => "a", 2 => "b" } }

FUNCTION Width156( aRow AS RECORD FieldRow156 )
RETURN aRow[ FLD_LEN ] + aRow[ 4 ]

// a typed hash parameter: FOR EACH over its values
FUNCTION Count156( hRows AS HASH OF RECORD FieldRow156 )
   LOCAL iTotal := 0
   LOCAL aRow
   FOR EACH aRow IN hRows
      iTotal += aRow[ FLD_LEN ]
   NEXT
RETURN iTotal
