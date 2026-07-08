#include "astype.ch"
// Test 80: faithful by-value semantics for un-@'d arguments.
//
// Harbour passes parameters BY VALUE; `@` opts into by-reference. C#
// `ref` is per-signature, so a parameter that ANY caller @-passes
// emits `ref` for every caller — including the ones who wrote a bare
// argument and, in Harbour, expect NO write-back.
//
// The transpiler reconciles those un-@'d callers with HbDiscard:
//   - a bare variable  -> ref HbDiscard<T>.Seed(x): the callee still
//     sees x's value as input, but the write-back is dropped and the
//     caller's x is untouched (in-out and out-only both faithful);
//   - a literal / omitted slot -> ref HbDiscard<T>.Value (no input to
//     preserve).
// Emitting `ref x` for the bare case would write back — the silent
// divergence from Harbour that W0020 warns about; this test pins the
// correct behaviour.

PROCEDURE Main()

   LOCAL nRef := 10 AS NUMERIC
   LOCAL nBare := 10 AS NUMERIC
   LOCAL nInOut := 10 AS NUMERIC
   LOCAL cRef := "keep" AS STRING
   LOCAL cBare := "keep" AS STRING

   // AddTo has an @-caller below, so nTarget emits as C# `ref`.
   // by ref   -> nRef becomes 15
   AddTo(@nRef, 5)
   // by value -> nBare stays 10 (write dropped)
   AddTo(nBare, 5)

   // In-out: the body READS the parameter before writing it, so the
   // bare call must still see the input value (10) even while its
   // write-back is discarded.
   // sees 10 in, returns via param, but stays 10
   Grow(nInOut)
   // 10 * 3 = 30 as the RETURN
   QOut("grow-returned=", Grow(nInOut))

   // String out-param, same rules.
   // by ref   -> cRef becomes "new"
   SetTag(@cRef, "new")
   // by value -> cBare stays "keep"
   SetTag(cBare, "new")

   // Literal into the ref slot — no input to keep, pure discard.
   AddTo(100, 5)

   QOut("nRef=", nRef)
   QOut("nBare=", nBare)
   QOut("nInOut=", nInOut)
   QOut("cRef=", cRef)
   QOut("cBare=", cBare)

RETURN

PROCEDURE AddTo( /*@*/nTarget AS NUMERIC, nAmount AS NUMERIC )
   /*@*/
   nTarget := nTarget + nAmount
RETURN

   // Reads nVal (proving the seeded input survives), writes it, and also
   // returns 3x so the caller can observe the computed value regardless.
FUNCTION Grow( nVal AS NUMERIC ) AS NUMERIC
   /*@*/
   LOCAL nWas := nVal AS NUMERIC
   nVal := nVal * 3
RETURN nWas * 3

PROCEDURE SetTag( /*@*/cInto AS STRING, cValue AS STRING )
   /*@*/
   cInto := cValue
RETURN
