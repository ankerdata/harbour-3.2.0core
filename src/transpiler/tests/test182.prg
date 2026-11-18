// Test 182: a codeblock parameter typed from its callers' blocks.
// A `b` parameter is a delegate in C# when every caller passes a block of
// one shape: a literal, a local made from one, or the caller's own typed
// parameter. A parameter the routine gives a block itself (DEFAULT), or
// whose callers disagree, stays a plain block. A block of another shape
// reaching a typed parameter through a plain variable is called through
// a delegate of the parameter's shape.

#include "common.ch"

PROCEDURE Main()

   LOCAL bBig182 := { |nValue| nValue > 10 }
   LOCAL xAny182 := AnyBlock182()

   // a literal, a local, and NIL
   ? CountIf182( { 5, 15, 25 }, { |nValue| nValue > 10 } )
   ? CountIf182( { 5, 15, 25 }, bBig182 )
   ? CountIf182( { 5, 15, 25 } )

   // a parameter passed on
   ? CountTwice182( { 1, 11, 21, 31 }, bBig182 )

   // a block of another shape, through a plain variable
   ? CountIf182( { 5, 15, 25 }, xAny182 )

   // the routine gives itself a block
   ? FirstOr182( { 3, 4 } )
   ? FirstOr182( { 3, 4 }, { |nValue| nValue * 2 } )

   // two callers that disagree
   ? Describe182( { |cName| "name " + cName }, "a" )
   ? Describe182( { |nValue| nValue + 1 }, 1 )

   RETURN

FUNCTION CountIf182( aValues, bTest182 )

   LOCAL nValue
   LOCAL nCount := 0

   IF bTest182 == NIL
      RETURN -1
   ENDIF
   FOR EACH nValue IN aValues
      IF Eval( bTest182, nValue )
         nCount++
      ENDIF
   NEXT

   RETURN nCount

FUNCTION AnyBlock182()

   RETURN { || .T. }

FUNCTION CountTwice182( aValues, bPass182 )

   RETURN CountIf182( aValues, bPass182 ) * 2

FUNCTION FirstOr182( aValues, bMap182 )

   DEFAULT bMap182 TO { |nValue| nValue }

   RETURN Eval( bMap182, aValues[ 1 ] )

FUNCTION Describe182( bWhat182, xValue )

   RETURN Eval( bWhat182, xValue )
