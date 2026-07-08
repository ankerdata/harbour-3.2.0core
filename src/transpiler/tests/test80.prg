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

    LOCAL nRef   := 10
    LOCAL nBare  := 10
    LOCAL nInOut := 10
    LOCAL cRef   := "keep"
    LOCAL cBare  := "keep"

    // AddTo has an @-caller below, so nTarget emits as C# `ref`.
    AddTo( @nRef,  5 )      // by ref   -> nRef becomes 15
    AddTo(  nBare, 5 )      // by value -> nBare stays 10 (write dropped)

    // In-out: the body READS the parameter before writing it, so the
    // bare call must still see the input value (10) even while its
    // write-back is discarded.
    Grow(  nInOut )         // sees 10 in, returns via param, but stays 10
    ? "grow-returned=", Grow( nInOut )   // 10 * 3 = 30 as the RETURN

    // String out-param, same rules.
    SetTag( @cRef,  "new" ) // by ref   -> cRef becomes "new"
    SetTag(  cBare, "new" ) // by value -> cBare stays "keep"

    // Literal into the ref slot — no input to keep, pure discard.
    AddTo( 100, 5 )

    ? "nRef=",  nRef
    ? "nBare=", nBare
    ? "nInOut=", nInOut
    ? "cRef=",  cRef
    ? "cBare=", cBare

RETURN

PROCEDURE AddTo( /*@*/nTarget, nAmount )
    nTarget := nTarget + nAmount
RETURN

// Reads nVal (proving the seeded input survives), writes it, and also
// returns 3x so the caller can observe the computed value regardless.
FUNCTION Grow( /*@*/nVal )
    LOCAL nWas := nVal
    nVal := nVal * 3
RETURN nWas * 3

PROCEDURE SetTag( /*@*/cInto, cValue )
    cInto := cValue
RETURN
