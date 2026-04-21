#include "astype.ch"
// Test 45b: Main that calls LoadFlags (from test45a.prg) at full
// arity. A 1-arg call to LoadFlags lives in a dead `IF .F.` branch
// below: the scanner sees the call site in source (records arity 1
// in the observed-call-arity bitmap) so the emitter generates the
// short overload, but Harbour and C# both skip the body at runtime
// so neither tries to run LoadFlags("short.dat") — which would
// crash both pipelines (can't ASize NIL) and, worse, crash them
// with DIFFERENT errors (BASE/2023 vs IndexOutOfRangeException)
// that the compare step would flag as a bogus transpilation
// divergence. The short-overload *emission* remains directly
// asserted by scripts/verify_shortoverload.sh via grep.

PROCEDURE Main()

   LOCAL aArr := {} AS ARRAY

   LoadFlags("full.dat", @aArr, 4)
   QOut("full[1]=" + aArr[1])
   QOut("full[4]=" + aArr[4])

   IF .F.
      LoadFlags("short.dat")
   ENDIF

RETURN
