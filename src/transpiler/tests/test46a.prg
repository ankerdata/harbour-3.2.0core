// Test 46a + 46b: no caller uses a short arity, so the emitter must
// skip the short overload (dead-method avoidance).
//
//    test46a.prg  defines  PROCEDURE SaveFlags( cFile, aFlag, nCount )
//    test46b.prg  calls    SaveFlags( "out.dat", @aArr, 3 )  // full only
//
// The reftab's observed-arity bitmap has bit 3 set and nothing in
// 0..iFirstRef. The short-overload guard checks that intersection and
// skips emission. verify_shortoverload.sh asserts the absence by
// grepping the emitted .cs.
PROCEDURE SaveFlags( cFile, aFlag, nCount )

   LOCAL i

   ASize( aFlag, nCount )
   FOR i := 1 TO nCount
      aFlag[ i ] := cFile + "#" + Str( i, 1 )
   NEXT

RETURN
