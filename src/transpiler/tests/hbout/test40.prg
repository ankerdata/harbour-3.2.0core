#include "astype.ch"
// Test 40: BEGIN SEQUENCE / END SEQUENCE with no RECOVER/ALWAYS.
//
// Before the fix, this emitted `try { ... }` with nothing following,
// which C# rejects (CS1524). Harbour semantics are "swallow any
// runtime error in the body", so we now emit an empty catch. The
// transpiler also issues a W-level warning because the idiom is
// usually a missed RECOVER in the source.
//
// Real-code analogue: easiposx/spoolprt.prg around the paper-status
// serial-port probing (bare BEGIN SEQUENCE around oPrtPort:PurgeRX,
// oPrtPort:TimeOuts, etc. to shrug off port-not-ready errors).

PROCEDURE Main()
   LOCAL lBodyRan := .F. AS LOGICAL

   BEGIN SEQUENCE
      lBodyRan := .T.
   END SEQUENCE

   QOut("body: " + IIF(lBodyRan, "yes", "no"))

RETURN
