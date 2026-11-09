// Test 165: a generic function. `AS ARRAY OF T`, T a single capital
// letter, is a type parameter: Join165() is `List<T> Join165<T>( List<T>,
// List<T> )`, so one function joins whole numbers, strings, rows of strings
// and plain arrays, and C# checks each call. A literal argument, an empty
// one too, is the list another argument makes T; so is the result.

#include "../include/astype.ch"

PROCEDURE Main()

   LOCAL aIds AS ARRAY OF INTEGER := { 1, 2 }
   LOCAL aNames AS ARRAY OF CHARACTER := { "one" }
   LOCAL aRows AS ARRAY OF ARRAY OF CHARACTER := { { "nId", "cName" } }
   LOCAL aMixed := { 1, "two" }
   LOCAL aAll
   LOCAL aCopy
   LOCAL iSum := 0
   LOCAL iId

   Join165( aIds, { 3, 4 } )
   Join165( aNames, Words165() )
   Join165( aRows, { { "7", "seven" } } )
   Join165( aMixed, { .T., 4.5 } )
   aAll := Join165( aIds, { 5 } )
   FOR EACH iId IN aAll
      iSum += iId
   NEXT
   aCopy := Join165( {}, aNames )
   ? Len( aIds ), iSum, aNames[ 3 ], Upper( aRows[ 2 ][ 2 ] ), Len( aMixed ), ValType( aMixed[ 4 ] )
   ? Len( aCopy ), Upper( aCopy[ 2 ] )

   RETURN

FUNCTION Join165( aInto AS ARRAY OF T, aFrom AS ARRAY OF T ) AS ARRAY OF T

   LOCAL i

   FOR i := 1 TO Len( aFrom )
      AAdd( aInto, aFrom[ i ] )
   NEXT

RETURN aInto

FUNCTION Words165() AS ARRAY OF CHARACTER

RETURN { "two", "three" }
