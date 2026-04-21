#include "astype.ch"
// Test 45a: short-overload emission — typed parameters and caller-
// observed arity gating.
//
// test45a defines a function with a ref param, called from test45b with
// BOTH the full arity (@-ref form) and a short arity (just the first
// param). C# `ref` params are required, so the canonical
//
//    LoadFlags(string cFile, ref dynamic[] aFlag, decimal nCount = default)
//
// can't accept the 1-arg call. The emitter generates a short overload
// that covers arities 0..iFirstRef (= 0..1 here):
//
//    LoadFlags(string cFile = default)
//
// Two things this pair guards:
//   1. The short overload is emitted (else the 1-arg call CS1501s).
//   2. Its pre-ref parameter is typed `string` — Hungarian-inferred from
//      the `c` prefix — NOT `dynamic`. A dynamic short overload would
//      hide the typing the canonical exposes and collapse every pre-ref
//      slot back to untyped Harbour idiom.
//
// Companion test46 covers the other side: a function whose callers
// never use a short form must NOT emit the overload.

PROCEDURE LoadFlags( cFile AS STRING, /*@*/aFlag AS ARRAY, nCount AS NUMERIC )

   LOCAL i AS NUMERIC

   ASize(aFlag, nCount)
   FOR i := 1 TO nCount
      aFlag[i] := cFile + ":" + Str(i, 1)
   NEXT

RETURN
