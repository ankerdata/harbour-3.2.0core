// Test 174: a whole-number parameter's default from an untyped value.
//
// `DEFAULT iNo TO saSettings174[ 1 ]` gives an `i` parameter, a C# long,
// an element of a plain array, which holds whatever was put there: here a
// decimal, as a number read from a file is. C# takes an untyped whole
// number into a long by itself and refuses an untyped decimal at run time,
// so the default is cast as an assignment to an `i` local is. A default
// the scan knows whole (a literal, another `i` name) gets no cast.

#include "common.ch"

STATIC saSettings174 := {}

FUNCTION Label174( iNo, iWidth )

   DEFAULT iNo TO saSettings174[ 1 ]
   DEFAULT iWidth TO 4

RETURN PadL( hb_ntos( iNo ), iWidth, "0" )

PROCEDURE Main()

   AAdd( saSettings174, Val( "7" ) )

   ? Label174()
   ? Label174( 12 )
   ? Label174( , 2 )

RETURN
