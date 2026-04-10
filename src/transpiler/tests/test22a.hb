#include "astype.ch"
// Test 22a + 22b: two classes with the same method name but
// incompatible signatures, defined in separate files because the
// Harbour compiler treats METHOD bodies as globally-named functions
// and refuses to redefine `Add` in a single file.
//
// Demonstrates that hbreftab.tab keys methods by (Class, Method) so
// the two definitions stay separate. Without method-keyed entries,
// both `Numbers:Add` and `Names:Add` would share a single `add` row
// and the scanner would emit a "downgrading to USUAL" warning. With
// method-keyed entries, the two never collide:
//
//   numbers::add  -  -  1  nValue:NUMERIC:-
//   names::add    -  -  1  cValue:STRING:-
//
// and the C# emits two strongly-typed methods on two distinct classes.
#include "hbclass.ch"

CLASS Numbers

   DATA nTotal AS NUMERIC INIT 0
   METHOD New()
   METHOD Add( nValue )
   METHOD Show()

ENDCLASS

METHOD New() AS OBJECT CLASS Numbers
   ::nTotal := 0
RETURN Self

METHOD Add( nValue AS NUMERIC ) AS OBJECT CLASS Numbers
   ::nTotal := ::nTotal + nValue
RETURN Self

METHOD Show() AS OBJECT CLASS Numbers
   QOut("total=" + Str(::nTotal))
RETURN Self
