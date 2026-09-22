#include "astype.ch"
// Test 40: BEGIN SEQUENCE / END SEQUENCE with no RECOVER/ALWAYS.
//
// Before the fix, this emitted `try { ... }` with nothing following,
// which C# rejects (CS1524). Without RECOVER, Harbour ends a BREAK in
// the body at END SEQUENCE; a runtime error still goes to the error
// block. So the emitted catch is `catch (HbBreak) { }` (test114 has
// the rest of BREAK and BEGIN SEQUENCE); only BEGIN SEQUENCE WITH and
// no RECOVER, which swallows every error, draws a W-level warning.

PROCEDURE Main()
   LOCAL lBodyRan := .F. AS LOGICAL

   BEGIN SEQUENCE
      lBodyRan := .T.
   END SEQUENCE

   QOut("body: " + IIF(lBodyRan, "yes", "no"))

RETURN
