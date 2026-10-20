// Test 45a + 45b: an array passed with @ to a function in another file,
// and a call that stops before the array.
//
// test45a defines LoadFlags( cFile, aFlag, nCount ), which sizes and
// fills aFlag; test45b calls it with the full arity, the array by
// reference, and in a dead branch with the first argument alone. An
// array is a reference in C#, so its parameter is the List itself, not a
// `ref`, and cFile is a string from its prefix:
//
//    LoadFlags(string cFile, List<dynamic> aFlag, decimal nCount = default)
//
// The short call pads the slot that has no default:
// `LoadFlags("short.dat", default(List<dynamic>))`. The short overload a
// by-reference scalar gets is test149's and test150's; test46 is this
// pair with full-arity callers only.

PROCEDURE LoadFlags( cFile, aFlag, nCount )

   LOCAL i

   ASize( aFlag, nCount )
   FOR i := 1 TO nCount
      aFlag[ i ] := cFile + ":" + Str( i, 1 )
   NEXT

RETURN
