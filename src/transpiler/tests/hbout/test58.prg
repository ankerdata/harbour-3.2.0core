#include "astype.ch"
// Test 58: `Self:` access to a CLASS VAR (static class-level member).
//
// Harbour's `CLASS VAR` is one value shared across every instance —
// it emits as a C# `static` field. Code reaches it with instance
// syntax (`::nTotal`), but C# rejects `this.staticField` (CS0176):
// a static member must be qualified by the type name.
//
// The transpiler now emits `Counter.nTotal`. Two code paths needed
// the fix and this test pins both:
//   1. a regular method body, via the AST SEND emitter
//   2. an INLINE method body, via the textual translator
//
// nLocal (a plain instance VAR) must stay `this.nLocal`.
//
// Doubled() also guards a regression: the INLINE translator rejects
// workarea-ALIAS bodies by scanning for `->`, but that scan must run
// AFTER the trailing-comment strip — a `->` in an INLINE line's `//`
// comment must not stub the method.

#include "hbclass.ch"

CLASS Counter

   CLASSDATA nTotal AS NUMERIC INIT 0 // shared across every instance
   DATA nLocal AS NUMERIC INIT 0 // per-instance
   METHOD Bump()
   METHOD Total() INLINE (::nTotal)
   METHOD Doubled() INLINE (::nTotal * 2) // arrow in a comment: a -> b

ENDCLASS

// Bump exercises the AST SEND path: a CLASS VAR and an instance VAR
// both written via `::`.
METHOD Bump() AS OBJECT CLASS Counter
   // CLASS VAR: becomes Counter.nTotal
   ::nTotal := ::nTotal + 1
   // instance:  stays this.nLocal
   ::nLocal := ::nLocal + 1
RETURN Self

PROCEDURE Main()
   LOCAL oA := Counter():New() AS OBJECT
   LOCAL oB := Counter():New() AS OBJECT

   oA:Bump()
   oA:Bump()
   // nTotal is shared: now 3
   oB:Bump()

   // INLINE read of CLASS VAR
   QOut("a_total=" + LTrim(Str(oA:Total())))
   QOut("b_total=" + LTrim(Str(oB:Total())))
   QOut("a_local=" + LTrim(Str(oA:nLocal)))
   QOut("b_local=" + LTrim(Str(oB:nLocal)))
   // INLINE body, arrow in comment
   QOut("doubled=" + LTrim(Str(oA:Doubled())))
RETURN
