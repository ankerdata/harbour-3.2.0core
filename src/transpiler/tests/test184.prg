// Test 184: a function that answers a value of its default's type.
// `xDefault AS T` and `AS T` make the function generic: Pick184() is
// `T Pick184<T>( ..., T xDefault )`, so a call that passes "" answers a
// string, one that passes .F. a logical, and C# checks what is done with
// each. A whole literal is a long, a decimal one a decimal; a whole
// decimal in the hash is taken for a whole T; a NIL in the hash is the
// type's zero for a number or a logical. Item184() is generic twice over,
// by its list and by its default.

#include "../include/astype.ch"

PROCEDURE Main()

   LOCAL hcData184 := { "name" => "Ann", "age" => 41, "price" => 12.50, ;
                        "open" => .T., "whole" => 7.00, "none" => NIL, ;
                        "tags" => { "a", "b" } }
   LOCAL acWords184 AS ARRAY OF CHARACTER := { "one", "two" }
   LOCAL aiIds184 AS ARRAY OF INTEGER := { 10, 20 }
   LOCAL cName184
   LOCAL iAge184
   LOCAL nPrice184
   LOCAL lOpen184
   LOCAL aTags184

   // the value's type is the default's
   cName184 := Pick184( hcData184, "name", "" )
   iAge184 := Pick184( hcData184, "age", 0 )
   nPrice184 := Pick184( hcData184, "price", 0.00 )
   lOpen184 := Pick184( hcData184, "open", .F. )
   ? Upper( cName184 ), iAge184 + 1, nPrice184 * 2, lOpen184

   // a key the hash has not: the default itself
   ? Pick184( hcData184, "missing", "-" ), Pick184( hcData184, "missing", -1 ), ;
     Pick184( hcData184, "missing", .T. )

   // used where it stands
   ? Len( Pick184( hcData184, "name", "" ) ), Pick184( hcData184, "age", 0 ) * 2

   // a whole decimal for a whole T; NIL for a number and a logical is
   // the type's zero in C#, and empty in both
   ? Pick184( hcData184, "whole", 0 ) == 7, Empty( Pick184( hcData184, "none", 0 ) ), ;
     Empty( Pick184( hcData184, "none", .F. ) )

   // a list as the default
   aTags184 := Pick184( hcData184, "tags", {} )
   ? Len( aTags184 ), Len( Pick184( hcData184, "missing", {} ) )

   // T from the list and from the default
   ? Item184( acWords184, 2, "" ), Item184( acWords184, 5, "none" )
   ? Item184( aiIds184, 1, 0 ) + 1, Item184( aiIds184, 9, -1 )

   RETURN

FUNCTION Pick184( hcData, cKey, xDefault AS T ) AS T

   IF hb_HHasKey( hcData, cKey )
      RETURN hcData[ cKey ]
   ENDIF

   RETURN xDefault

FUNCTION Item184( aArray AS ARRAY OF T, iIndex, xDefault AS T ) AS T

   IF iIndex >= 1 .AND. iIndex <= Len( aArray )
      RETURN aArray[ iIndex ]
   ENDIF

   RETURN xDefault
