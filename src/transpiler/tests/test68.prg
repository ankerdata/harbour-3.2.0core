// Test 68: ref-shim temp naming.
//
// The by-ref shim names each temp after the lvalue it backs — a plain
// @var becomes `_hbref_<var>`. Two things this guards:
//   1. Two by-ref args in ONE call get distinct, readable temps from
//      their variable names (here _hbref_nLo and _hbref_nHi).
//   2. Sequential calls — each wrapped in its own `{ }` scope — reuse the
//      same names rather than appending an ever-climbing counter; the
//      shim only adds a depth prefix (_hbref1_<var>) for a *nested* block.
//
// MinMax has two USUAL by-ref outputs (x-prefixed); the caller passes
// numeric locals, so each @arg mismatches the `ref dynamic` parameter and
// is shimmed. The values must round-trip identically through -GT and -GS.

FUNCTION MinMax( aData, xLo, xHi )
   LOCAL i
   xLo := aData[ 1 ]
   xHi := aData[ 1 ]
   FOR i := 2 TO Len( aData )
      IF aData[ i ] < xLo
         xLo := aData[ i ]
      ENDIF
      IF aData[ i ] > xHi
         xHi := aData[ i ]
      ENDIF
   NEXT
   RETURN NIL

PROCEDURE Main()
   LOCAL nLo, nHi
   MinMax( { 3, 1, 4, 1, 5 }, @nLo, @nHi )      // two refs in one call
   ? "a lo=" + Str( nLo, 2 ) + " hi=" + Str( nHi, 2 )
   MinMax( { 9, 2, 6, 8, 7 }, @nLo, @nHi )      // sequential call — reuses base 0
   ? "b lo=" + Str( nLo, 2 ) + " hi=" + Str( nHi, 2 )
RETURN
