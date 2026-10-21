// Test 160: a LOCAL initialised with one `new` of its declared type is
// `var` in C#; an empty array or hash literal is the type's empty
// instance, `new List<dynamic>()`, `new OrderedDictionary<…>()` and a
// FIXED array's `System.Array.Empty<T>()`.
//
// `var`: an array literal, a hash literal, an empty one of each, a class's
// New(). The type written out: a scalar, a function's result, a call on
// the new object, an empty FIXED array.

#include "hbclass.ch"
#include "../include/astype.ch"

CLASS Box160
   VAR aItems AS ARRAY INIT {}
   METHOD Count() INLINE Len( ::aItems )
ENDCLASS

PROCEDURE Main()

   ? Literals160()
   ? Objects160()
   ? Fixed160()

   RETURN

FUNCTION Literals160()

   LOCAL aNames := { "a", "b" }
   LOCAL aEmpty := {}
   LOCAL hAges := { "x" => 1, "y" => 2 }
   LOCAL hNone := { => }
   LOCAL cJoined := ""
   LOCAL aCopy := AClone( aNames )

   AAdd( aEmpty, "c" )
   hNone[ "z" ] := 3
   AEval( aNames, {| c | cJoined += c } )

   RETURN cJoined + aEmpty[ 1 ] + Str( hAges[ "y" ] + hNone[ "z" ], 2 ) + ;
      Str( Len( aCopy ), 2 )

FUNCTION Objects160()

   LOCAL oBox160 := Box160():New()
   LOCAL nItems := Box160():New():Count()

   AAdd( oBox160:aItems, "one" )

   RETURN Str( oBox160:Count(), 2 ) + Str( nItems, 2 )

FUNCTION Fixed160()

   LOCAL aNone AS FIXED ARRAY OF CHARACTER := {}

   RETURN Str( Len( aNone ), 2 )
