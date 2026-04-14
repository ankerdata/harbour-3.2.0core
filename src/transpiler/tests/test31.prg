// Test 31: `...` varargs forwarding inside a short codeblock.
//
// `{ |...| Foo(...) }` — Clipper-style short codeblock that forwards
// all its varargs to a callee — used to trip E0066 "Function not
// declared with variable number of parameters" under the transpiler.
//
// Short codeblocks don't get their own PHB_HFUNC context; the body
// expressions are parsed in the enclosing function's scope with
// `bBlock` set as a flag. So the fVParams check at HB_EA_PUSH_PCODE
// in include/hbexprb.c looks at the enclosing function's fVParams,
// which doesn't reflect the block's `|...|` marker. The check fires
// spuriously whenever a short codeblock forwards varargs from inside
// a non-varargs function.
//
// End-to-end verification of the varargs pipeline:
//
//   - Parser: include/hbexprb.c suppresses the spurious fVParams check
//     under HB_TRANSPILER.
//   - Scanner: hbreftab.c marks fCalledVarargs on any function reached
//     via a `...` call argument; the persisted reftab carries the flag
//     across scan/codegen runs.
//   - .hb emitter (gentr.c): round-trips `|...|` and `...` literally.
//   - C# emitter (gencsharp.c):
//       * `|...|` codeblock → `((Func<dynamic[], dynamic>)((dynamic[]
//         hbva) => { ...; return null; }))`
//       * `...` call arg or `{ ... }` array spread → `hbva`
//       * Function flagged fCalledVarargs → signature widens to
//         `params dynamic[] hbva` (with a per-param unpack preamble
//         for any named leading params — none in this test)
//       * LOCAL of a `|...|` codeblock → `Func<dynamic[], dynamic>`
//       * hb_AParams() → `hbva` (short-circuited, not routed via
//         HbRuntime)
//   - HbRuntime.EVAL(block, ...args) invokes the block by packing
//     trailing args into a dynamic[].
//
// Echo iterates its varargs with hb_AParams() + FOR EACH in both
// pipelines so the runtime output matches byte-for-byte between the
// .prg, .hb, and .cs outputs.
//
// Hit in easiposx: easicmremote, easiglory, easiwt, easizvt, easiwig,
// pptio — 6 sites across 6 files.

PROCEDURE Main()
   LOCAL bHandler := { |...| Echo(...) }
   Eval( bHandler, "hello", "world", "from harbour" )
RETURN

PROCEDURE Echo(...)
   LOCAL xArg
   LOCAL nIdx := 1
   FOR EACH xArg IN hb_AParams()
      ? "arg " + LTrim(Str(nIdx)) + ": " + xArg
      nIdx++
   NEXT
RETURN
