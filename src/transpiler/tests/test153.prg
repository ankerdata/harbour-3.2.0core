// Test 153: a write through `::` into an inherited AS INTEGER member is
// coerced like one through a typed receiver.
//
// A member declared AS INTEGER is a C# long, and a decimal written into
// it needs a `(long)` cast. The emitter gave one through a typed
// receiver (`oLine:nType := nValue`), walking the INHERIT chain for the
// member's declaration, but through Self it looked at the current
// class's own declarations only: TextTranLine's `::nType := nType`,
// nType declared by TranLine, compiled only while the value was a long
// constant and was CS0266 as soon as it was a decimal parameter
// (EasiPOS, plan C4's first pass). The Self rule now walks the chain
// through the reftab, as the receiver rule does.

#include "hbclass.ch"

CLASS Base153
   VAR nKind AS INTEGER INIT 0
   VAR nLevel AS INTEGER INIT 0
ENDCLASS

CLASS Leaf153 INHERIT Base153
   METHOD New( nKind )
   METHOD Deeper( nBy )
ENDCLASS

METHOD New( nKind ) CLASS Leaf153
   hb_default( @nKind, 7 )
   ::nKind := nKind                 // a decimal parameter into the parent's long
RETURN Self

METHOD Deeper( nBy ) CLASS Leaf153
   ::nLevel += nBy                  // a compound write, the same coercion
   ::nLevel := ::nLevel * 2 + nBy / 1
RETURN ::nLevel

PROCEDURE Main()
   LOCAL oLeaf := Leaf153():New( 3 )
   LOCAL nValue := 5

   ? oLeaf:nKind
   oLeaf:nKind := nValue            // through the typed receiver, as before
   ? oLeaf:nKind
   ? oLeaf:Deeper( 2 ), oLeaf:Deeper( 1 )
RETURN
