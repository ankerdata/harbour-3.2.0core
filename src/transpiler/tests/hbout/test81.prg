#include "astype.ch"
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
   LOCAL n := 5 AS NUMERIC

   // by-ref
   Fill(@n)
   // 6 — write-back kept
   QOut("afteref=" + LTrim(Str(n)))

   n := 5
   // deliberate by-value
   Fill(/*@*/n)
   // 5 — write-back discarded
   QOut("afterbyval=" + LTrim(Str(n)))

   n := 5
   // 6 — RETURN value
   QOut("ret=" + LTrim(Str(Fill(n))))
   // 5 — n untouched
   QOut("kept=" + LTrim(Str(n)))
RETURN

FUNCTION Fill( /*@*/nOut AS NUMERIC ) AS NUMERIC
   nOut := nOut + 1
RETURN nOut
