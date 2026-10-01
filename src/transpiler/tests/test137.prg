// Test 137: downcasts (plan C11).
//
// A name or a declaration of a subclass, given a value of its parent,
// keeps its class: the typed view after a test of the kind
// (`oItemLine137 := oLine` after `oLine:nKind == KIND_ITEM`), a parameter
// named or declared the subclass, a local initialised with the parent, a
// function declared returning the subclass, a member declared it. The
// name or the declaration is the claim, and C# needs the cast Harbour has
// no way to write, so the emitter writes it (`oItemLine137 =
// (ItemLine137)oLine;`). A wrong claim throws InvalidCastException there
// in C#, where Harbour goes on to the first message the object does not
// answer. An element of a declared array is `dynamic` in C# and needs no
// cast. A class unrelated to the name still wins over it, and is W0041
// against a declaration (tests/errors/declared_types.prg).

#include "../include/astype.ch"
#include "hbclass.ch"

#define KIND_FCN   1
#define KIND_ITEM  2

CLASS Line137
   VAR nKind INIT 0
   VAR cText INIT ""
ENDCLASS

CLASS FcnLine137 INHERIT Line137
   VAR nFixedNo INIT 0
   METHOD New( cText, nFixedNo ) CONSTRUCTOR
ENDCLASS

METHOD New( cText, nFixedNo ) CLASS FcnLine137
   ::nKind := KIND_FCN
   ::cText := cText
   ::nFixedNo := nFixedNo
   RETURN Self

CLASS ItemLine137 INHERIT Line137
   VAR nPrice INIT 0
   VAR nQty INIT 0
   METHOD New( cText, nPrice, nQty ) CONSTRUCTOR
   METHOD Value()
ENDCLASS

METHOD New( cText, nPrice, nQty ) CLASS ItemLine137
   ::nKind := KIND_ITEM
   ::cText := cText
   ::nPrice := nPrice
   ::nQty := nQty
   RETURN Self

METHOD Value() CLASS ItemLine137
   RETURN ::nPrice * ::nQty

CLASS Till137
   VAR aLines AS ARRAY OF CLASS Line137 INIT {}
   VAR oLastItem AS CLASS ItemLine137
   METHOD Add( oLine )
ENDCLASS

METHOD Add( oLine AS CLASS Line137 ) CLASS Till137
   AAdd( ::aLines, oLine )
   IF oLine:nKind == KIND_ITEM
      ::oLastItem := oLine               // a member declared the subclass
   ENDIF
   RETURN Self

PROCEDURE Main()
   LOCAL oTill137 := Till137():New()
   LOCAL oLine
   LOCAL oItemLine137
   LOCAL oFcn AS CLASS FcnLine137
   LOCAL nTotal := 0
   LOCAL iPos

   oTill137:Add( ItemLine137():New( "tea", 3, 2 ) )
   oTill137:Add( FcnLine137():New( "discount", 7 ) )
   oTill137:Add( ItemLine137():New( "cake", 5, 1 ) )

   FOR EACH oLine IN oTill137:aLines
      IF oLine:nKind == KIND_ITEM
         oItemLine137 := oLine           // a view by its name
         nTotal += oItemLine137:Value()
      ELSE
         oFcn := oLine                   // a view by its declaration
         ? oFcn:cText, oFcn:nFixedNo
      ENDIF
   NEXT
   ? nTotal

   FOR iPos := 1 TO Len( oTill137:aLines )
      oLine := oTill137:aLines[ iPos ]
      IF oLine:nKind == KIND_ITEM
         ? Describe137( oLine ), Price137( oLine ), Qty137( oLine )
      ENDIF
   NEXT

   ? oTill137:oLastItem:cText, ItemAt137( oTill137, 1 ):nPrice
   RETURN

// a parameter named for the subclass
FUNCTION Describe137( oItemLine137 )
   RETURN oItemLine137:cText + " x" + hb_ntos( oItemLine137:nQty )

// a parameter declared the subclass
FUNCTION Price137( oAny AS CLASS ItemLine137 )
   RETURN oAny:nPrice

// a local named for the subclass, initialised with its parent
FUNCTION Qty137( oLine AS CLASS Line137 )
   LOCAL oItemLine137 := oLine
   RETURN oItemLine137:nQty

// a function declared returning the subclass, returning its parent
FUNCTION ItemAt137( oTill137, iPos ) AS CLASS ItemLine137
   LOCAL oLine := oTill137:aLines[ iPos ]
   RETURN oLine
