#include "astype.ch"
// Test 34: Harbour Error object stopgap round-trip.
//
// Covers the HbError POCO added to HbRuntime.cs: ErrorNew() returns
// an instance whose field surface matches what createerror.prg and
// callers poke at. The return type is marked `-` in hbfuncs.tab so
// the emitter picks `dynamic` for the LHS, letting `:severity` and
// friends late-bind (otherwise CS1061 on a static `object`).
//
// This is a stopgap — no error propagation, no SEQUENCE/Try wiring.
// Replace with idiomatic C# exceptions / Result type when the real
// error story is designed.

PROCEDURE Main()
   LOCAL oError := ErrorNew() AS USUAL

   oError:severity := 3
   oError:genCode := 42
   oError:subSystem := "BASE"
   oError:subCode := 1001
   oError:description := "sample failure"
   oError:canRetry := .F.
   oError:canDefault := .T.

   QOut("severity:    " + Str(oError:severity, 4))
   QOut("genCode:     " + Str(oError:genCode, 4))
   QOut("subSystem:   " + oError:subSystem)
   QOut("description: " + oError:description)
   QOut("canRetry:    " + IIF(oError:canRetry, "yes", "no"))
   QOut("canDefault:  " + IIF(oError:canDefault, "yes", "no"))
   QOut("classname:   " + oError:classname())
RETURN
