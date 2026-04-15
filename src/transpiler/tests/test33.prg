// Test 33: regression for the dropped-PARAM parser bug.
//
// Before commit 9ce8f0ab76, any file with a file-scope STATIC
// initializer (e.g. `static snTrace := -1`) would silently drop
// the parameter list from the LAST user function in the file.
// Emitted C# looked like `string GetThreadName()` even though the
// PRG had `function GetThreadName(nID, lShort, lBrackets)`, and
// body references to those params then failed with CS0103.
//
// Root cause: at end-of-parse, hb_compAddInitFunc( pInitFunc )
// appends the synthetic `(_INITSTATICS)` frame and swaps
// functions.pLast to it, but hb_astEndFunc for the last user
// function wasn't being called until *after* that swap. The
// final AST node then captured pLocals from the init frame
// (empty) instead of the user function's real param list.
//
// Fix: call hb_astEndFunc at the top of the finalization phase,
// before synthetic init/line frames get appended.
//
// This test mirrors the minimal repro used to debug it: a
// file-scope STATIC, a regular function in the middle, and
// another function at the very end whose params *must* survive
// to the emitted C# signature.

STATIC snState := 0

PROCEDURE Main()
   Bump()
   Bump()
   ? Greet( "Alex", .T., "!" )
RETURN

PROCEDURE Bump()
   snState++
RETURN

// The last function in the file — the one the bug dropped params on.
FUNCTION Greet( cName, lExclaim, cSuffix )
   LOCAL cOut := "Hello " + cName
   IF lExclaim
      cOut += cSuffix
   ENDIF
RETURN cOut
