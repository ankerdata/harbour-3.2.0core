// Test 130: inside a method, a call to a function that shares a member's
// name is the function.
//
// Harbour's `name( ... )` is always a function call; a method is reached
// only by a send. C# binds an unqualified call inside a class to the member
// first, so easipos dialog.prg's `method EnableScanners()`, whose body calls
// scanio.prg's procedure EnableScanners(), called itself until the stack
// overflowed. The call is emitted `Program.EnableScanners()`, and so is one
// from a subclass, where the inherited method would take it.

#include "hbclass.ch"

STATIC snCalls130 := 0

CLASS Panel130
   VAR lOn130 INIT .T.
   METHOD Wake130()
ENDCLASS

METHOD Wake130() CLASS Panel130
   IF ::lOn130
      Wake130()
   ENDIF
   RETURN Self

CLASS Door130 INHERIT Panel130
   METHOD Open130()
ENDCLASS

METHOD Open130() CLASS Door130
   Wake130()
   RETURN Self

PROCEDURE Wake130()
   snCalls130++
   RETURN

PROCEDURE Main()
   Panel130():New():Wake130()
   Door130():New():Open130()
   ? snCalls130
   RETURN
