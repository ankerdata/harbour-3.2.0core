// Test 139: `!` over a relational or arithmetic operand.
//
// Harbour's `!` binds looser than every relational and arithmetic
// operator: `!Left( c, 3 ) == "TH-"` is `!( Left( c, 3 ) == "TH-" )`.
// C#'s `!` binds tighter than all of them, and the AST keeps only the
// parentheses the source wrote, so the emitter wraps a binary operand
// itself. Without it the C# was `!Left( c, 3 ) == "TH-"`, `!` applied to
// a string: CS0023 once the operand was typed, a run-time binder error
// while it was `dynamic` (easiwig.prg's Transaction History test). `$`
// and `^` emit as calls and need nothing; source parentheses stay as
// written; `!` still binds tighter than `.AND.` and `.OR.`.

#include "hbclass.ch"

CLASS Ticket139
   VAR cRef INIT ""
   VAR nCount INIT 0
   METHOD New( cRef, nCount ) CONSTRUCTOR
ENDCLASS

METHOD New( cRef, nCount ) CLASS Ticket139
   ::cRef := cRef
   ::nCount := nCount
   RETURN Self

STATIC FUNCTION PayAgain139( oTicket )
   // Transaction History must not be paid again
   IF !Left( oTicket:cRef, 3 ) == "TH-"
      RETURN .T.
   ENDIF
   RETURN .F.

PROCEDURE Main()
   LOCAL oFresh := Ticket139():New( "AB-1", 2 )
   LOCAL oHistory := Ticket139():New( "TH-9", 0 )
   LOCAL nCount := 3
   LOCAL cName := "Lisbon"
   LOCAL lOpen := .F.

   ? PayAgain139( oFresh ), PayAgain139( oHistory )
   ? !oFresh:nCount == 0, !oHistory:nCount == 0
   ? !nCount > 5, !nCount + 1 == 4
   ? !cName < "M", !cName >= "Paris"
   ? !"bon" $ cName, !( nCount == 3 )
   ? !lOpen .AND. nCount == 3, !lOpen .OR. nCount == 0
   ? !oHistory:nCount != 0
   RETURN
