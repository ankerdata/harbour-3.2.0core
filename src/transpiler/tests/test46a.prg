// Test 46a + 46b: an array passed with @ to a function in another file,
// every caller at the full arity.
//
//    test46a.prg  defines  PROCEDURE SaveFlags( cFile, aFlag, nCount )
//    test46b.prg  calls    SaveFlags( "out.dat", @aArr, 3 )  // full only
//
// The array's parameter is the List itself in C#, a reference without
// `ref`, so the call passes aArr as written, and SaveFlags is declared
// once.
PROCEDURE SaveFlags( cFile, aFlag, nCount )

   LOCAL i

   ASize( aFlag, nCount )
   FOR i := 1 TO nCount
      aFlag[ i ] := cFile + "#" + Str( i, 1 )
   NEXT

RETURN
