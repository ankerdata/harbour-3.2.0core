#include "astype.ch"
// Test 125: a Harbour array is a List<dynamic>, one object every holder
// shares.
//
// A C# array cannot grow: AAdd() and ASize() took theirs by `ref` and
// handed back a new one, so only the variable passed saw the change. An
// array held by a second variable, a member, or an element of another
// array kept its old length, and `AAdd( aX[ i ], y )` grew a copy nobody
// held (easipos's ORM Restructure read past an empty list for it). A
// List<dynamic> grows in place, as Harbour's array does, and needs no `ref`
// anywhere: not at the call, and not on a parameter only passed to AAdd().

#include "hbclass.ch"

CLASS Stock125

   DATA aItems125 AS ARRAY
   METHOD Fill125()

ENDCLASS

METHOD Fill125() AS OBJECT CLASS Stock125
   ::aItems125 := {}
RETURN Self

   // an array parameter the routine only grows: a plain List<dynamic>
STATIC FUNCTION AddTwice125( aTarget AS ARRAY )
   AAdd(aTarget, "x")
   AAdd(aTarget, "y")
RETURN NIL

PROCEDURE Main()
   LOCAL aRows125 := {{}, {}} AS ARRAY
   LOCAL aAlias125 AS ARRAY
   LOCAL aGrid125 := Array(2, 3) AS ARRAY
   LOCAL aSeen125 := {1, 2} AS ARRAY
   LOCAL oStock125 := Stock125():New():Fill125() AS OBJECT
   LOCAL x AS USUAL

   // an element grows in place
   AAdd(aRows125[1], "a")
   AAdd(aRows125[1], "b")
   QOut(Len(aRows125[1]), aRows125[1][2])
   hb_AIns(aRows125[1], 1, "z", .T.)
   QOut(Len(aRows125[1]), aRows125[1][1])

   // a second holder sees the change, whoever makes it
   aAlias125 := aRows125[2]
   ASize(aRows125[2], 3)
   QOut(Len(aAlias125))
   AddTwice125(aAlias125)
   QOut(Len(aRows125[2]), aRows125[2][5])
   hb_ADel(aAlias125, 1, .T.)
   QOut(Len(aRows125[2]))

   // a member
   AAdd(oStock125:aItems125, "first")
   QOut(Len(oStock125:aItems125), oStock125:aItems125[1])

   // every dimension of Array()
   QOut(Len(aGrid125), Len(aGrid125[2]), ValType(aGrid125[2][3]))

   // FOR EACH sees an element its body adds
   FOR EACH x IN aSeen125
      IF x < 4
         AAdd(aSeen125, x + 2)
      ENDIF
   NEXT

   QOut(Len(aSeen125), aSeen125[5])

RETURN
