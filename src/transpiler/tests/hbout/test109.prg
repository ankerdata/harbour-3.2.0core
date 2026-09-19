#include "astype.ch"
// Test 109: a function the program defines wins over an HbRuntime
// method of the same name.
//
// hbfuncs.tab gives a name the HbRuntime prefix whenever HbRuntime.cs
// declares a method of that name, and every call to it was emitted as
// HbRuntime.<name>. HbRuntime.cs once carried placeholders for EasiPOS's
// own functions, so all 9,060 calls to its GetFlag returned NIL. Harbour's
// linker takes a library function only when the program defines none
// (test108 is the same rule for a contrib library's name), so a name the
// reftab records as defined in the corpus is the program's function.
// Pow and StrCmp are HbRuntime helpers for the emitter, not Harbour
// functions, so the Harbour build links these.

PROCEDURE Main()
   QOut(Pow("ab", 3))
   QOut(StrCmp("left", "right"))
   QOut(Len(Pow("xyz", 2)))
RETURN

FUNCTION Pow( cUnit AS STRING, nTimes AS NUMERIC ) AS STRING
RETURN Replicate(cUnit, nTimes)

FUNCTION StrCmp( cLeft AS STRING, cRight AS STRING ) AS STRING
RETURN cLeft + " vs " + cRight
