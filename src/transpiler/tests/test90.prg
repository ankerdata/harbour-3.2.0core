// Test 90: middle gaps in method sends and constructor calls.
//
// A function call with a gap — `Foo(a, , c)` — has emitted the later
// arguments in named form since test18. A method send did not: for
// `Class():New(a, , c)` and `::Super:New(a, , c)` the emitter could not
// resolve the callee's reftab row (the receiver is a constructor call,
// or Super), fell back to positional emission and silently dropped the
// gap, so `c` landed in the second slot. In the easipos corpus that was
// eight dialog constructors with a flag or a title one slot to the
// left. The send resolver now follows a `Class()` receiver and the
// INHERIT chain (a subclass without its own New constructs through its
// parent's), and a gap that nothing can name is passed as null —
// Harbour's NIL — rather than dropped.
#include "common.ch"
#include "hbclass.ch"

CLASS Base
   VAR cLabel INIT ""
   VAR lFlag INIT .F.
   METHOD New( cLabel, nUnused, lFlag )
   METHOD Show()
ENDCLASS

METHOD New( cLabel, nUnused, lFlag ) CLASS Base
   DEFAULT nUnused TO 0
   DEFAULT lFlag TO .F.
   ::cLabel := cLabel + LTrim( Str( nUnused ) )
   ::lFlag := lFlag
RETURN Self

METHOD Show() CLASS Base
   ? ::cLabel + "/" + IIF( ::lFlag, "T", "F" )
RETURN NIL

/* ::Super:New with a gap: .T. must reach lFlag, not nUnused. */
CLASS Derived INHERIT Base
   METHOD New( cLabel )
ENDCLASS

METHOD New( cLabel ) CLASS Derived
   ::Super:New( cLabel, , .T. )
RETURN Self

/* No New of its own: Leaf():New( ... ) resolves to Base's row. */
CLASS Leaf INHERIT Base
ENDCLASS

PROCEDURE Main()
   LOCAL o

   o := Base():New( "base", , .T. )
   o:Show()
   o := Derived():New( "derived" )
   o:Show()
   o := Leaf():New( "leaf", , .T. )
   o:Show()
   o := Base():New( "all", 7 )
   o:Show()
RETURN
