#include "astype.ch"
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

   DATA nKind AS INTEGER INIT 0
   DATA nLevel AS INTEGER INIT 0

ENDCLASS

CLASS Leaf153 INHERIT Base153

   METHOD New( nKind )
   METHOD Deeper( nBy )

ENDCLASS

METHOD New( nKind AS NUMERIC ) AS OBJECT CLASS Leaf153
   hb_default(@nKind, 7)
   // a decimal parameter into the parent's long
   ::nKind := nKind
RETURN Self

METHOD Deeper( nBy AS NUMERIC ) AS NUMERIC CLASS Leaf153
   // a compound write, the same coercion
   ::nLevel += nBy
   ::nLevel := ::nLevel * 2 + nBy / 1
RETURN ::nLevel

PROCEDURE Main()
   LOCAL oLeaf := Leaf153():New(3) AS OBJECT
   LOCAL nValue := 5 AS NUMERIC

   QOut(oLeaf:nKind)
   // through the typed receiver, as before
   oLeaf:nKind := nValue
   QOut(oLeaf:nKind)
   QOut(oLeaf:Deeper(2), oLeaf:Deeper(1))
RETURN
