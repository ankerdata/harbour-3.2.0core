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
   LOCAL oError := ErrorNew()

   oError:severity    := 3
   oError:genCode     := 42
   oError:subSystem   := "BASE"
   oError:subCode     := 1001
   oError:description := "sample failure"
   oError:canRetry    := .F.
   oError:canDefault  := .T.

   ? "severity:    " + Str( oError:severity, 4 )
   ? "genCode:     " + Str( oError:genCode, 4 )
   ? "subSystem:   " + oError:subSystem
   ? "description: " + oError:description
   ? "canRetry:    " + iif( oError:canRetry,   "yes", "no" )
   ? "canDefault:  " + iif( oError:canDefault, "yes", "no" )
   ? "classname:   " + oError:classname()
RETURN
