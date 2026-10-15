#include "astype.ch"
// Test 151: every array subscript goes through Ix().
//
// Harbour's subscript is 1-based, a List's 0-based. The emitter rebased
// it by hand: a literal decremented where it stood (`aCSV[19]` became
// `aCSV[18]`), anything else `[(int)(…) - 1]`. That truncated a
// fractional index silently, reported Harbour's index 0 as C#'s -1, cut
// the link between a literal and its source line, and read as noise
// (`[(int)(i + 1) - 1]`). Now every array subscript is `[Ix(…)]`, the
// literal with its source value: HbRuntime's Ix() answers the 0-based
// int and throws on an index below 1 and on a fraction. A hash's key, a
// FOR's bounds and HbRuntime's own 1-based positions are untouched.
// A fraction is the one deliberate divergence: Harbour truncates it, the
// C# reports it, so that case runs as C# beside the Harbour line.

#include "hbclass.ch"

#define POS151_THIRD 3

CLASS Shelf151

   DATA aSlots AS ARRAY
   METHOD Slot( n ) INLINE ::aSlots[ n ]
   METHOD Second() INLINE ::aSlots[ 2 ]

ENDCLASS

PROCEDURE Main()
   LOCAL aList := {10, 20, 30, 40} AS ARRAY
   LOCAL aGrid := {{1, 2}, {3, 4}} AS ARRAY
   LOCAL hnByNo := {1 => "one", 2 => "two"} AS HASH
   LOCAL hByName := {"x" => 7} AS HASH
   LOCAL iSlot AS NUMERIC
   LOCAL nPos := Val("2") AS NUMERIC
   LOCAL oError AS OBJECT
   LOCAL oShelf := Shelf151():New() AS OBJECT

   oShelf:aSlots := {"a", "b", "c"}
   // literals keep their value
   QOut(aList[1], aList[4])
   // a whole-number local
   FOR iSlot := 1 TO Len(aList)
      QOut(iSlot, aList[iSlot])
   NEXT

   // a decimal local, an expression
   QOut(aList[nPos], aList[nPos + 1])
   // a define
   QOut(aList[POS151_THIRD])
   // two dimensions
   QOut(aGrid[2][1], aGrid[1][2])
   // assignment targets
   aList[nPos] := 25
   aList[3] += 5
   QOut(aList[2], aList[3])
   // hash keys pass as they are
   QOut(hnByNo[2], hByName["x"])
   // INLINE bodies
   QOut(oShelf:Slot(3), oShelf:Second())
   // a 1-based position from the runtime
   QOut(aList[AScan(aList, 40)])

   BEGIN SEQUENCE WITH {|oErr| Break( oErr ) }
      // index 0: Harbour's bound error
      QOut(aList[0])
   RECOVER USING oError
      QOut("index 0:", IIF(oError == NIL, "no error", "bound error"))
   END SEQUENCE

   nPos := Val("9")
   BEGIN SEQUENCE WITH {|oErr| Break( oErr ) }
      // past the end: the List's
      QOut(aList[nPos])
   RECOVER USING oError
      QOut("index 9:", IIF(oError == NIL, "no error", "bound error"))
   END SEQUENCE

   // a fraction: Harbour truncates 2.5 to 2 and reads on, the C# reports it

#ifdef __HB_TRANSPILER__
#pragma BEGINCSHARP
   try
   {
       Ix(2.5m);
       HbRuntime.QOut("fraction:", "no error");
   }
   catch (System.ArgumentOutOfRangeException)
   {
       HbRuntime.QOut("fraction:", "error");
   }
#pragma ENDCSHARP
#endif
RETURN
