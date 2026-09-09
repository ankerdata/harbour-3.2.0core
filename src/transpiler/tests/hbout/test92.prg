#include "astype.ch"
// Test 92: `::Super:Method(...)` with dynamic arguments.
//
// C# binds a `base.Method(...)` call statically and cannot when any
// argument is `dynamic` (CS1971: "needs to be dynamically dispatched,
// but cannot be because it is part of a base access expression"). A
// parameter typed OBJECT — an o-name with no class of that name, or a
// method slot no typed caller ever refined — is `dynamic` in C#, and
// every Dialog subclass in the easipos corpus forwards two of them
// through ::Super:New (17 CS1971). The emitter now casts a dynamic-
// typed variable argument in a base call: `(object)` for a dynamic
// slot (statically bound, converts to dynamic for free), the slot's
// own type for a typed one. Concrete-typed arguments are left alone so
// a real mismatch still reads as CS1503.
#include "hbclass.ch"

CLASS Gadget

   DATA cName AS STRING INIT "gadget"

ENDCLASS

CLASS Crate

   DATA oItem AS OBJECT
   DATA nCount AS NUMERIC INIT 0
   METHOD New( oItem, nCount )

ENDCLASS

CLASS Tandem INHERIT Crate

   METHOD New( oItem, xCount )

ENDCLASS

METHOD New( oItem AS OBJECT, nCount AS NUMERIC ) AS OBJECT CLASS Crate
   ::oItem := oItem
   ::nCount := nCount
RETURN Self

   /* oItem is OBJECT (no class named Item) and xCount is USUAL: both
   dynamic, into a dynamic slot and a decimal slot respectively. Main
   passes an `x` local (USUAL by prefix, and that is sticky across the
   later assignment) so the call site cannot sharpen oItem — a
   `Tandem()` receiver is typed since test93 — and the slot stays
   dynamic, which is the shape this test is about. */
METHOD New( oItem AS OBJECT, xCount AS USUAL ) AS OBJECT CLASS Tandem
   ::Super:New(oItem, xCount)
RETURN Self

PROCEDURE Main()
   LOCAL xItem AS USUAL
   LOCAL oTandem AS OBJECT
   xItem := Gadget():New()
   oTandem := Tandem():New(xItem, 2)
   QOut("count=" + LTrim(Str(oTandem:nCount)) + " item=" + oTandem:oItem:cName)
RETURN
