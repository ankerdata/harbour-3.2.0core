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
   CLASS VAR nTotal INIT 0      // shared across every instance
   VAR nLocal INIT 0            // per-instance
   METHOD Bump()
   METHOD Total() INLINE (::nTotal)
   METHOD Doubled() INLINE (::nTotal * 2)   // arrow in a comment: a -> b
ENDCLASS

// Bump exercises the AST SEND path: a CLASS VAR and an instance VAR
// both written via `::`.
METHOD Bump() CLASS Counter
   ::nTotal := ::nTotal + 1     // CLASS VAR: becomes Counter.nTotal
   ::nLocal := ::nLocal + 1     // instance:  stays this.nLocal
RETURN Self

PROCEDURE Main()
   LOCAL oA := Counter():New()
   LOCAL oB := Counter():New()

   oA:Bump()
   oA:Bump()
   oB:Bump()                    // nTotal is shared: now 3

   ? "a_total=" + LTrim( Str( oA:Total() ) )    // INLINE read of CLASS VAR
   ? "b_total=" + LTrim( Str( oB:Total() ) )
   ? "a_local=" + LTrim( Str( oA:nLocal ) )
   ? "b_local=" + LTrim( Str( oB:nLocal ) )
   ? "doubled=" + LTrim( Str( oA:Doubled() ) )  // INLINE body, arrow in comment
RETURN
