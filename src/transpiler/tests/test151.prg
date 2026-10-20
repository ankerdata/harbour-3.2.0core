// Test 151: every array subscript goes through I().
//
// Harbour's subscript is 1-based, a List's 0-based. Every array subscript
// is `[I(…)]`, a literal with its source value: HbRuntime's I() answers
// the 0-based int and throws on an index below 1 and on a fraction, so
// Harbour's index 0 is a bound error, not C#'s -1. A loop's `i` does not
// hide I(), C# comparing names with their case: `a[I(i)]`. A hash's key,
// a FOR's bounds and HbRuntime's own 1-based positions are untouched.
// A fraction is the one deliberate divergence: Harbour truncates it, the
// C# reports it, so that case runs as C# beside the Harbour line.

#include "hbclass.ch"

#define POS151_THIRD 3

CLASS Shelf151
   VAR aSlots
   METHOD Slot( n ) INLINE ::aSlots[ n ]
   METHOD Second() INLINE ::aSlots[ 2 ]
ENDCLASS

PROCEDURE Main()
   LOCAL aList := { 10, 20, 30, 40 }
   LOCAL aGrid := { { 1, 2 }, { 3, 4 } }
   LOCAL hnByNo := { 1 => "one", 2 => "two" }
   LOCAL hByName := { "x" => 7 }
   LOCAL iSlot, nPos := Val( "2" ), oError
   LOCAL oShelf := Shelf151():New()

   oShelf:aSlots := { "a", "b", "c" }
   ? aList[ 1 ], aList[ 4 ]                       // literals keep their value
   FOR iSlot := 1 TO Len( aList )                 // a whole-number local
      ? iSlot, aList[ iSlot ]
   NEXT
   ? aList[ nPos ], aList[ nPos + 1 ]             // a decimal local, an expression
   ? aList[ POS151_THIRD ]                        // a define
   ? aGrid[ 2 ][ 1 ], aGrid[ 1 ][ 2 ]             // two dimensions
   aList[ nPos ] := 25                            // assignment targets
   aList[ 3 ] += 5
   ? aList[ 2 ], aList[ 3 ]
   ? hnByNo[ 2 ], hByName[ "x" ]                  // hash keys pass as they are
   ? oShelf:Slot( 3 ), oShelf:Second()            // INLINE bodies
   ? aList[ AScan( aList, 40 ) ]                  // a 1-based position from the runtime

   BEGIN SEQUENCE WITH {| oErr | Break( oErr ) }  // index 0: Harbour's bound error
      ? aList[ 0 ]
   RECOVER USING oError
      ? "index 0:", IIf( oError == NIL, "no error", "bound error" )
   END SEQUENCE
   nPos := Val( "9" )
   BEGIN SEQUENCE WITH {| oErr | Break( oErr ) }  // past the end: the List's
      ? aList[ nPos ]
   RECOVER USING oError
      ? "index 9:", IIf( oError == NIL, "no error", "bound error" )
   END SEQUENCE

   // a fraction: Harbour truncates 2.5 to 2 and reads on, the C# reports it
#ifndef __HB_TRANSPILER__
   ? "fraction:", "error"
#else
#pragma BEGINCSHARP
   try
   {
       I(2.5m);
       HbRuntime.QOut("fraction:", "no error");
   }
   catch (System.ArgumentOutOfRangeException)
   {
       HbRuntime.QOut("fraction:", "error");
   }
#pragma ENDCSHARP
#endif
RETURN
