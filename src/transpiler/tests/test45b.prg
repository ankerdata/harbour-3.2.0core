// Test 45b: Main that calls LoadFlags (from test45a.prg) at full
// arity. A 1-arg call to LoadFlags lives in a dead `IF .F.` branch
// below: the emitter compiles the call (see test45a.prg), but Harbour
// and C# both skip the body at runtime so neither tries to run
// LoadFlags("short.dat") — which would crash both pipelines (can't
// ASize NIL) and, worse, crash them with DIFFERENT errors (BASE/2023
// vs IndexOutOfRangeException) that the compare step would flag as a
// bogus transpilation divergence.

PROCEDURE Main()

   LOCAL aArr := {}

   LoadFlags( "full.dat", @aArr, 4 )
   ? "full[1]=" + aArr[ 1 ]
   ? "full[4]=" + aArr[ 4 ]

   IF .F.
      LoadFlags( "short.dat" )
   ENDIF

RETURN
