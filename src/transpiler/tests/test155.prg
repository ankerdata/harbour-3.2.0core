// Test 155: records and fixed arrays (plan C12 / C16).
//
// An array used as a record declares its positions once, by name and
// type: `RECORD FieldRow( cName, cType, iLen, iDec )`, each field typed by
// its prefix or an `AS` of its own. A variable, parameter, member or
// return declared `AS RECORD X` holds one; `AS FIXED ARRAY OF RECORD X`
// an array of them that never grows or shrinks. Harbour sees arrays
// (astype.ch removes the RECORD line and reads the declarations as AS
// ARRAY; the -GT round trip writes them so), C# a class of the fields
// built by its constructor from the `{ … }` literal, and a C# array for
// FIXED. A read with a constant subscript — a literal or a #define — is
// the field itself (`aRow.iLen`), typed and checked when C# builds. The
// class is plain, fields and a constructor and nothing else (Alex), so a
// subscript that is not a constant is W0046 (tests/errors/
// fixed_record.prg), and code that holds a row as `dynamic` (Dump155
// below, whose x parameter takes anything) declares it before reading
// it. The rows are shared, as Harbour's are: a write through one holder
// shows through the other.
// Harbour compiles this file itself for the reference run: astype.ch
// removes the RECORD lines and reads the declarations as AS ARRAY.

#include "../include/astype.ch"

#define FLD_NAME 1
#define FLD_TYPE 2
#define FLD_LEN  3
#define FLD_DEC  4

RECORD FieldRow( cName, cType, iLen, iDec )
RECORD IndexRows( aNames AS FIXED ARRAY OF CHARACTER, aExpressions AS FIXED ARRAY OF CHARACTER, aUnique AS FIXED ARRAY OF CHARACTER )
RECORD TableDef( cAlias, aFields AS FIXED ARRAY OF RECORD FieldRow, aIndexes AS RECORD IndexRows, hHash, cDatabase )

STATIC saFields AS FIXED ARRAY OF RECORD FieldRow := { ;
   { "PLUNO", "N", 16, 0 }, ;
   { "CAPTION", "C", 40, 0 }, ;
   { "PRICE1", "N", 12, 3 } }

PROCEDURE Main()
   LOCAL aDef := PluDef155()
   LOCAL aRow AS RECORD FieldRow
   LOCAL aFields AS FIXED ARRAY OF RECORD FieldRow := aDef[ 2 ]
   LOCAL aMore AS ARRAY OF RECORD FieldRow := { { "TAX", "N", 1, 0 } }   // a list of records, grown
   LOCAL iPos

   ? aDef[ 1 ], aDef[ 5 ], Len( aDef[ 2 ] )         // fields of the def, the fixed array's length
   ? Len( aFields ), aFields[ 1 ][ FLD_NAME ]        // the fixed array, a row's field by define
   ? aDef[ 2 ][ 3 ][ FLD_LEN ], aDef[ 3 ][ 1 ][ 2 ]  // nested: a field's field, a fixed array of strings in a record
   FOR iPos := 1 TO Len( aFields )                   // a computed subscript, then a constant
      ? iPos, aFields[ iPos ][ FLD_NAME ], aFields[ iPos ][ FLD_LEN ] + aFields[ iPos ][ FLD_DEC ]
   NEXT
   FOR EACH aRow IN aFields                          // the loop variable declared a record
      ? aRow[ FLD_TYPE ], Width155( aRow )
   NEXT
   aRow := aFields[ 2 ]                              // a row shared with the static
   aRow[ FLD_LEN ] := 45
   ? saFields[ 2 ][ FLD_LEN ], aDef[ 2 ][ 2 ][ 3 ]   // seen through every holder
   ? AScan( aFields, {| a | a[ FLD_NAME ] == "PRICE1" } )
   ? hb_HGet( aDef[ 4 ], "PLUNO" ), Len( aDef[ 3 ][ 1 ] )
   Dump155( aDef[ 2 ] )                              // the fixed array to code that takes anything
   AAdd( aMore, { "KP", "C", 8, 0 } )                // a record into a list: the literal is its constructor
   aMore[ 2 ][ FLD_LEN ] := 9
   ? Len( aMore ), aMore[ 1 ][ FLD_NAME ], aMore[ 2 ][ FLD_LEN ], Width155( aMore[ 2 ] )
   FOR EACH aRow IN aMore
      ? "  more", aRow[ FLD_NAME ], aRow[ FLD_TYPE ]
   NEXT
RETURN

// a def, as pludef.prg's PLUDef() builds one: the literal is the record's
// constructor, its fields' literals theirs
FUNCTION PluDef155() AS RECORD TableDef
   LOCAL aDef AS RECORD TableDef := { ;
      "PLU", ;
      saFields, ;
      { { "PLNUM", "PLCAP" }, { "pluno", "caption" }, { "PLNUM" } }, ;
      { "PLUNO" => "nNo", "CAPTION" => "cName" }, ;
      "PRG" }
RETURN aDef

// a record parameter: its fields typed
FUNCTION Width155( aRow AS RECORD FieldRow )
   LOCAL iWidth := aRow[ FLD_LEN ]
   IF aRow[ FLD_DEC ] > 0
      iWidth += aRow[ FLD_DEC ] + 1
   ENDIF
RETURN iWidth

// a reader that takes anything: the fixed array is an array to it, and a
// row it holds as `dynamic` is read once declared
PROCEDURE Dump155( xAny )
   LOCAL xItem
   LOCAL aRow AS RECORD FieldRow
   ? ValType( xAny ), Len( xAny )
   FOR EACH xItem IN xAny
      aRow := xItem
      ? "  row", aRow[ FLD_NAME ], aRow[ FLD_LEN ]
   NEXT
RETURN
