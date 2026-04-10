// Test 21: class method with by-reference parameter.
//
// Exercises the (Class::Method)-keyed by-ref scan:
//
//   1. The scanner sees `oCalc:Adjust(@n)` and needs to mark the
//      parameter byref. Without method-keyed entries, the bitmap
//      would land on a bare `adjust` and could collide with any
//      free function or other class method called Adjust.
//
//   2. The C# emitter then has to find the same Class::Method entry
//      so the parameter declaration in `Adjust` gets `ref decimal`,
//      and the call site gets `oCalc.Adjust(ref n)`.
//
// The expected output is `40` because Adjust doubles its argument
// in place, and we then double it again.
#include "hbclass.ch"

CLASS Calculator
   METHOD New()
   METHOD Adjust( nValue )
ENDCLASS

METHOD New() CLASS Calculator
RETURN Self

METHOD Adjust( nValue ) CLASS Calculator
   nValue := nValue * 2
RETURN Self

PROCEDURE Main()
   LOCAL oCalc := Calculator():New()
   LOCAL n := 10

   ? "before: n=" + Str( n )
   oCalc:Adjust( @n )
   ? "after:  n=" + Str( n )
   oCalc:Adjust( @n )
   ? "after:  n=" + Str( n )

RETURN
