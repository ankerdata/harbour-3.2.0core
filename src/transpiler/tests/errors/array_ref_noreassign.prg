// W0023: an array passed by-ref (`@aArr`) to a callee that only mutates
// elements and never reassigns the whole variable. C# arrays are
// reference types, so element mutation already propagates without `ref`
// — the `@` is redundant. The transpiler emits the parameter as a plain
// `dynamic[]` (codegen continues, the .cs still compiles) and surfaces
// warning W0023 to flag the source.

PROCEDURE Main()
   LOCAL aArr := { 1, 2, 3 }
   ScaleArr( @aArr, 2 )       // redundant @ — ScaleArr never reassigns aData
   ? aArr[ 1 ]
RETURN

PROCEDURE ScaleArr( aData, nFactor )
   LOCAL i
   FOR i := 1 TO Len( aData )
      aData[ i ] := aData[ i ] * nFactor   // element mutation only
   NEXT
RETURN
