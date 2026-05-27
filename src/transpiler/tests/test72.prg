// Test 72: HB_SYMBOL_UNUSED(x) is a no-op at C# emit.
//
// std.ch defines:
//
//   #define HB_SYMBOL_UNUSED( symbol )  ( ( symbol ) )
//
// so the canonical "I'm intentionally not using this parameter, don't
// warn me" pragma expands to a bare parenthesised expression. As a
// statement that's CS0201 ("Only assignment, call, increment...") —
// `lFlag;` is not a legal C# statement. The transpiler now detects
// expression-statements whose expression collapses to a valueless
// shape (HB_ET_VARIABLE, literal, bare obj:member access, etc.) and
// skips the emit entirely.
//
// The .prg / .hb / .cs pipelines must all produce the same output:
// the procedure's `?` lines print, the HB_SYMBOL_UNUSED line has no
// runtime effect. (Harbour silently no-ops it through `((x))`; C#
// silently no-ops it through omitted emission.)

PROCEDURE Probe72( lFlag, nCount, cName )
   HB_SYMBOL_UNUSED( lFlag )
   HB_SYMBOL_UNUSED( nCount )
   HB_SYMBOL_UNUSED( cName )
   ? "Probe72 ran"
RETURN

PROCEDURE Main()
   Probe72( .T., 7, "alpha" )
   ? "after Probe72"
RETURN
