// Generic functions (plan C12): `AS ARRAY OF T` is bound at each call by
// the arguments. W0041 for an argument that would bind T otherwise, for a
// FIXED array (a C# array, no List) and for a literal element the binding
// refuses; W0040 for T anywhere but a function's own ARRAY OF T. The lines
// marked "quiet" must not warn.

#include "hbclass.ch"

FUNCTION Use41g( nValue )
   LOCAL aIds AS ARRAY OF INTEGER := { 1, 2 }
   LOCAL aNames AS ARRAY OF CHARACTER := { "a" }
   LOCAL aFixed AS FIXED ARRAY OF INTEGER := { 3 }
   LOCAL aPlain := { 1, "b" }
   Join41g( aIds, { 3, 4 } )                        // quiet
   Join41g( aPlain, { .T. } )                       // quiet: T binds to a plain array's elements
   Join41g( {}, aNames )                            // quiet: the literal is a List<string>
   Join41g( aIds, aNames )                          // fires W0041: aIds binds T to INTEGER
   Join41g( aIds, aPlain )                          // fires W0041: a plain array beside a List<long>
   Join41g( aIds, aFixed )                          // fires W0041: a C# array is no List
   Join41g( aIds, { nValue } )                      // fires W0041: a decimal into a List<long>
RETURN Len( aIds )

FUNCTION Join41g( aInto AS ARRAY OF T, aFrom AS ARRAY OF T ) AS ARRAY OF T
   LOCAL aSeen AS ARRAY OF T := {}                  // quiet: the function's own T
   LOCAL iItem
   FOR iItem := 1 TO Len( aFrom )
      AAdd( aSeen, aFrom[ iItem ] )
      AAdd( aInto, aFrom[ iItem ] )
   NEXT
RETURN aInto

FUNCTION Nested41g( aRows AS ARRAY OF ARRAY OF T )  // fires W0040: T nested
RETURN Len( aRows )

CLASS Box41g
   VAR aItems AS ARRAY OF T INIT {}                 // fires W0040: a member
   METHOD Put41g( aList AS ARRAY OF T )
ENDCLASS

METHOD Put41g( aList AS ARRAY OF T ) CLASS Box41g    // fires W0040: a method's parameter
   ::aItems := aList
RETURN Self
