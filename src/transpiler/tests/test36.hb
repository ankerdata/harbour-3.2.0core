#include "astype.ch"
// Test 36: source-level parens around ASSIGN survive to C#.
//
// Harbour allows `(var := expr) <op> other` as a compact way to
// capture a call's return value and immediately test it. The outer
// parens matter because `:=` is an expression returning the assigned
// value — without them, comparison parses as `var := (expr <op> other)`
// and the var ends up bool-typed.
//
// Before commit a7a675e012, the C# emitter dropped the parens. C#'s
// `=` has lower precedence than `<`, so `(h := Call()) < 0` turned
// into `h = Call() < 0`, which C# reads as `h = (Call() < 0)` — the
// bool result flows into h (fails `bool → decimal` at compile time)
// and the `< 0` branch evaluates against the call's raw return, not
// the comparison. Three cooperating fixes in gencsharp.c:
//
//   1. HB_ET_LIST single-child (`(expr)`) propagates the caller's
//      fParen hint to that child.
//   2. HB_ET_SETGET propagates fParen into the wrapped expression.
//   3. The generic binop emitter self-parenthesizes ASSIGN / compound
//      assigns when the parent signals disambiguation is needed.

PROCEDURE Main()
   LOCAL nVal AS NUMERIC
   LOCAL nA := 0 AS NUMERIC
   LOCAL nB := 0 AS NUMERIC

   // Inline assignment inside comparison. The Fetch() below returns 5;
   // nVal captures it, and the `> 3` branch reads the captured value.
   IF (nVal := Fetch()) > 3
      QOut("fetched=" + Str(nVal, 3) + " branch=big")
   ELSE
      QOut("fetched=" + Str(nVal, 3) + " branch=small")
   ENDIF

   // Compound assignment in a comparison. nA += 2 returns 2; > 1 is TRUE.
   IF (nA += 2) > 1
      QOut("nA=" + Str(nA, 3) + " branch=big")
   ELSE
      QOut("nA=" + Str(nA, 3) + " branch=small")
   ENDIF

   // Nested: assignment inside a boolean combined expression.
   IF (nB := Fetch()) > 3 .AND. nB < 10
      QOut("nB=" + Str(nB, 3) + " in-range")
   ELSE
      QOut("nB=" + Str(nB, 3) + " out-of-range")
   ENDIF

RETURN

FUNCTION Fetch() AS NUMERIC
RETURN 5
