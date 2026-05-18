// Test 61: more HbRuntime correctness fixes.
//
//   AScan  — the codeblock form: scan evaluating a block per element.
//   AClone — duplicates nested arrays recursively, not shallowly.
//   Empty  — an empty hash is Empty().
//   Right  — a non-positive count yields "" (not an exception).
//   StrZero— a negative number keeps its sign: StrZero(-5,4) -> "-005".
//   DToC   — honours SET DATEFORMAT.
//
// Not covered here (can't round-trip through Harbour standalone):
// GETMEMBER/SETMEMBER reaching the dynamic bag, and hb_mutexLock's
// timeout — both fixed in HbRuntime alongside these.

#include "set.ch"

PROCEDURE Main()
   LOCAL aData   := { 10, 20, 30 }
   LOCAL aNested := { 1, { 2, 3 } }
   LOCAL aCopy
   LOCAL hEmpty  := { => }

   // AScan with a codeblock — 20 is at index 2
   ? "ascan_blk=" + LTrim( Str( AScan( aData, {| x | x == 20 } ) ) )

   // AClone must deep-copy the nested array
   aCopy := AClone( aNested )
   aCopy[ 2 ][ 1 ] := 99                       // must not touch the original
   ? "aclone=" + LTrim( Str( aNested[ 2 ][ 1 ] ) )   // still 2

   // Empty() of an empty hash
   ? "empty_hash=" + IIF( Empty( hEmpty ), "Y", "N" )

   // Right() with a negative count
   ? "right_neg=[" + Right( "hello", -2 ) + "]"

   // StrZero of a negative number
   ? "strzero=" + StrZero( -5, 4, 0 )

   // DToC honours SET DATEFORMAT
   Set( _SET_DATEFORMAT, "dd.mm.yyyy" )
   ? "dtoc=" + DToC( SToD( "20240115" ) )
RETURN
