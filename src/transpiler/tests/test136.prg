// Test 136: declared types, `AS CLASS X` and `AS ARRAY OF …` (plan C11).
//
// A name types a variable as far as a prefix can: `o<Class>` names a
// class, `a` an array. Where the name cannot say more, the declaration
// does, in every position: a local, a parameter, a file or routine
// static, a class member (`VAR`), a function's or a method's return.
// A declared variable is what its declaration says, and nothing in the
// body moves it. An array declared `AS ARRAY OF CLASS X` keeps its
// List<dynamic> storage, but a read of an element is an X: a send on one
// is cast, `((Gizmo136)aGizmos[i]).nSize`, and checked when C# builds,
// and a FOR EACH variable over it is an X. The element type lives only
// where the source declares it; into anything undeclared an
// ARRAY OF X is a plain array again.
//
// Harbour accepts the declarations in fewer places (none on a member,
// a method or a function's return) and wants a forward declaration of a
// class from another file; include/astype.ch removes them for it, as
// EasiPOS's include/astype.ch does, so the generated C is the same.

#include "../include/astype.ch"
#include "hbclass.ch"

STATIC soSpare136 AS CLASS Gizmo136
STATIC saShelf136 AS ARRAY OF CLASS Gizmo136

CLASS Gizmo136
   VAR cName INIT ""
   VAR nSize INIT 0
   METHOD New( cName, nSize ) CONSTRUCTOR
   METHOD Label()
ENDCLASS

METHOD New( cName, nSize ) CLASS Gizmo136
   ::cName := cName
   ::nSize := nSize
   RETURN Self

METHOD Label() CLASS Gizmo136
   RETURN ::cName + "(" + hb_ntos( ::nSize ) + ")"

// A member only a subclass declares, read through an element declared
// Gizmo136, is read as it stands: the element is `dynamic` in C# (a
// List<dynamic> slot), so it needs no cast (test138).
CLASS Bigizmo136 INHERIT Gizmo136
   VAR cColour INIT "red"
ENDCLASS

CLASS Crate136
   VAR aGizmos AS ARRAY OF CLASS Gizmo136 INIT {}
   VAR oFirst AS CLASS Gizmo136
   METHOD Add( oGizmo )
   METHOD Largest() AS CLASS Gizmo136
ENDCLASS

METHOD Add( oGizmo AS CLASS Gizmo136 ) CLASS Crate136
   AAdd( ::aGizmos, oGizmo )
   IF ::oFirst == NIL
      ::oFirst := oGizmo
   ENDIF
   RETURN Self

METHOD Largest() CLASS Crate136
   RETURN Biggest136( ::aGizmos )

PROCEDURE Main()
   LOCAL oCrate := Crate136():New()
   LOCAL oAny AS CLASS Gizmo136
   LOCAL aNames AS ARRAY OF CHARACTER := {}
   LOCAL oItem
   LOCAL iPos

   oCrate:Add( Gizmo136():New( "bolt", 3 ) )
   oCrate:Add( Gizmo136():New( "beam", 9 ) )
   oCrate:Add( Gizmo136():New( "nut", 1 ) )
   oCrate:Add( Bigizmo136():New( "plate", 5 ) )

   FOR EACH oItem IN oCrate:aGizmos
      AAdd( aNames, oItem:Label() )
   NEXT
   ? aNames[ 2 ], Len( aNames[ 2 ] )

   FOR iPos := 1 TO Len( oCrate:aGizmos )
      ? oCrate:aGizmos[ iPos ]:cName, oCrate:aGizmos[ iPos ]:nSize
   NEXT

   ? oCrate:aGizmos[ 4 ]:cColour, oCrate:aGizmos[ 4 ]:nSize

   oAny := oCrate:Largest()
   ? oAny:Label()
   ? oCrate:oFirst:Label()

   saShelf136 := oCrate:aGizmos
   soSpare136 := saShelf136[ 3 ]
   ? soSpare136:Label()
   ? Len( Names136( saShelf136 ) ), Names136( saShelf136 )[ 1 ]
   ? Total136()
   RETURN

FUNCTION Biggest136( aList AS ARRAY OF CLASS Gizmo136 ) AS CLASS Gizmo136
   LOCAL oBest AS CLASS Gizmo136
   LOCAL oOne
   FOR EACH oOne IN aList
      IF oBest == NIL .OR. oOne:nSize > oBest:nSize
         oBest := oOne
      ENDIF
   NEXT
   RETURN oBest

STATIC FUNCTION Names136( aList AS ARRAY OF CLASS Gizmo136 ) AS ARRAY OF CHARACTER
   LOCAL aOut := {}
   LOCAL iPos
   FOR iPos := 1 TO Len( aList )
      AAdd( aOut, aList[ iPos ]:cName )
   NEXT
   RETURN aOut

STATIC FUNCTION Total136()
   STATIC snCalls AS NUMERIC
   LOCAL nTotal := 0
   LOCAL oG
   FOR EACH oG IN saShelf136
      nTotal += oG:nSize
   NEXT
   snCalls := 1
   RETURN nTotal
