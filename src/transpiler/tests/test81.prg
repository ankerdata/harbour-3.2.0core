// Test 81: the call-site by-value marker.
//
// A block comment whose content is `@`, placed immediately before a
// call argument, marks a DELIBERATE by-value pass to a by-ref
// parameter. At transpile time it suppresses W0020 for that argument
// (verified manually — verify.sh checks runtime output, not warnings).
// At run time it changes nothing: the argument passes by value either
// way, so the callee's write-back is discarded. This test pins that
// runtime semantics, which must be identical across .prg, round-tripped
// .hb, and generated .cs.
//
// The marker is a special token, not an ordinary comment: genhb
// re-emits it inline (attached to its argument) on -GT round-trip,
// mirroring how the declaration-site marker is synthesized.

PROCEDURE Main()
   LOCAL n := 5

   Fill( @n )                              // by-ref
   ? "afteref=" + LTrim( Str( n ) )        // 6 — write-back kept

   n := 5
   Fill( /*@*/n )                          // deliberate by-value
   ? "afterbyval=" + LTrim( Str( n ) )     // 5 — write-back discarded

   n := 5
   ? "ret=" + LTrim( Str( Fill( n ) ) )    // 6 — RETURN value
   ? "kept=" + LTrim( Str( n ) )           // 5 — n untouched
RETURN

FUNCTION Fill( nOut )
   nOut := nOut + 1
RETURN nOut
