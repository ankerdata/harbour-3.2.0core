// Test 166: array names that declare their elements. `an`, `ai` and `ac`
// before a capital (a static's `san`, `sai`, `sac`) are an array of
// numbers, of whole numbers and of strings, as AS ARRAY OF NUMERIC,
// INTEGER and CHARACTER declare one: a List<decimal>, a List<long> and a
// List<string>, on a local, a static, a parameter, a member and a
// record's field, and a literal given one is built as it. AS ARRAY OF
// STRING is AS ARRAY OF CHARACTER. AAdd() puts a whole literal into a
// List<long>, hb_AIns() and hb_ADel() with lAutoSize work on a value
// list, which never holds NIL, and the literal default of hb_default() and
// hb_defaultValue() is the value's own type, a static left NIL included.

#include "../include/astype.ch"
#include "hbclass.ch"

#define TALLY166_AMOUNTS  2

RECORD Tally166( cName, anAmounts )

STATIC saiSeen166 := {}
STATIC saiUnset166

CLASS Till166
   VAR acNames INIT {}
   METHOD Add166( cName )
ENDCLASS

METHOD Add166( cName ) CLASS Till166

   AAdd( ::acNames, cName )

RETURN Self

PROCEDURE Main()

   LOCAL aiIds := { 1, 2 }
   LOCAL anPrices := { 1.5, 2 }
   LOCAL aWords AS ARRAY OF STRING := { "one" }
   LOCAL oTill166 := Till166():New()
   LOCAL aTally AS RECORD Tally166 := { "lunch", { 2.25, 3 } }
   LOCAL aiUnset := hb_defaultValue( Unset166(), {} )

   AAdd( aiIds, 3 )
   hb_AIns( aiIds, 1, 7, .T. )
   AAdd( anPrices, 0.5 )
   hb_ADel( anPrices, 1, .T. )
   AAdd( saiSeen166, Len( aiIds ) )
   oTill166:Add166( "first" ):Add166( "second" )
   ? Sum166( aiIds ), aiIds[ 1 ], Total166( anPrices ), Total166( aTally[ TALLY166_AMOUNTS ] )
   ? Len( aWords ), oTill166:acNames[ 2 ], saiSeen166[ 1 ], Words166( "a" )[ 1 ], Sum166(), Len( aiUnset )

   RETURN

FUNCTION Sum166( aiValues )

   LOCAL iSum := 0
   LOCAL iValue

   hb_default( @aiValues, {} )
   FOR EACH iValue IN aiValues
      iSum += iValue
   NEXT

RETURN iSum

FUNCTION Total166( anValues )

   LOCAL nTotal := 0
   LOCAL nValue

   FOR EACH nValue IN anValues
      nTotal += nValue
   NEXT

RETURN nTotal

FUNCTION Unset166()

RETURN saiUnset166

FUNCTION Words166( cWord ) AS ARRAY OF STRING

   LOCAL acWords := { cWord }

RETURN acWords
