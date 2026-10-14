#include "astype.ch"
// Test 150: a call binds the short overload, and a short overload is
// emitted only where a call binds it.
//
// A function with a by-reference parameter gets a short overload for the
// callers that leave the `@` arguments out (test56, test65). The call
// emitter, though, padded every omitted slot up to the last `ref` with
// `ref HbDiscard<T>.Value` as soon as a call had one argument, so only a
// call with none ever bound the short overload: in EasiPOS 64 calls did,
// 368 short-arity calls went padded to the full overload, and 138 of its
// 147 short overloads were bound by nothing. Now a call whose arguments
// stop before the first `ref` is emitted as written and binds the short
// overload, an inner gap as `default(T?)`; a call that reaches the `ref`
// keeps the padding. And the short overload is emitted only where the
// scan recorded such a call: a file-static routine's calls are credited
// to its own row (they were recorded under the bare name, which no row
// holds, so every file-static routine with a `ref` parameter got one),
// and an empty record no longer means "never scanned, emit anyway".

FUNCTION Label150( cPrefix AS STRING, /*@*/cLabel AS STRING, nWidth AS NUMERIC ) AS STRING
   hb_default(@nWidth, 8)
   cLabel := PadR(cPrefix, nWidth, ".")
RETURN Upper(cLabel)

FUNCTION Pair150( cA AS STRING, cB AS STRING, /*@*/lFlag AS LOGICAL ) AS STRING
   IF cA == NIL
      cA := "-"
   ENDIF

   lFlag := (cA == cB)
RETURN cA + "/" + cB

STATIC FUNCTION Scale150( nValue AS NUMERIC, /*@*/nFactor AS NUMERIC ) AS NUMERIC
   // a short caller: emitted and bound
   nFactor := 10
RETURN nValue * nFactor

STATIC FUNCTION Stamp150( cText AS STRING, /*@*/lDone AS LOGICAL ) AS STRING
   // full callers only: no short overload
   lDone := .T.
RETURN "[" + cText + "]"

PROCEDURE Main()
   LOCAL cLabel := "" AS STRING
   LOCAL lFlag := .F. AS LOGICAL
   LOCAL nFactor := 0 AS NUMERIC
   LOCAL lDone := .F. AS LOGICAL

   // stops before the ref: the short overload
   QOut(Label150("sale"))
   QOut(Label150("item", @cLabel), cLabel)
   // reaches past the ref: padded
   QOut(Label150("tax", , 4))
   // an inner gap before the ref
   QOut(Pair150(, "b"))
   QOut(Pair150("a", "a", @lFlag), lFlag)
   QOut(Scale150(3))
   QOut(Scale150(2, @nFactor), nFactor)
   QOut(Stamp150("x", @lDone), lDone)
   QOut(Stamp150("y", @lDone))
RETURN
