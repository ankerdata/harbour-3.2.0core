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
   LOCAL nC AS NUMERIC
   LOCAL nD AS NUMERIC
   LOCAL nE AS NUMERIC

   // --- Plain assignment statements (the common case by far) ---
   //
   // These are what :=  is actually used for 99% of the time —
   // not the assignment-in-comparison edge case further down.
   // Guards against a paren-wrapping regression going too far and
   // inserting unwanted parens around statement-position assigns.

   nA := 10
   QOut("plain: nA=" + Str(nA, 3))

   nA := Fetch()
   QOut("from-call: nA=" + Str(nA, 3))

   // Chained assignment — `nC := nD := nE := 7` assigns 7 to all
   // three, right-to-left. Emitted C# needs the nested assign to
   // survive as an expression, which is a separate code path from
   // the top-level statement.
   nC := (nD := (nE := 7))
   QOut("chain: " + Str(nC, 3) + Str(nD, 3) + Str(nE, 3))

   // Compound assignment as a statement (no surrounding comparison).
   nA += 5
   nA *= 2
   QOut("compound: nA=" + Str(nA, 3))

   // --- Assignment-inside-expression (the original paren case) ---

   // The Fetch() below returns 5; nVal captures it, and the `> 3`
   // branch reads the captured value.
   IF (nVal := Fetch()) > 3
      QOut("fetched=" + Str(nVal, 3) + " branch=big")
   ELSE
      QOut("fetched=" + Str(nVal, 3) + " branch=small")
   ENDIF

   // Reset nA then compound-assign in a comparison.
   nA := 0
   IF (nA += 2) > 1
      QOut("nA=" + Str(nA, 3) + " branch=big")
   ELSE
      QOut("nA=" + Str(nA, 3) + " branch=small")
   ENDIF

   // Assignment inside a boolean combined expression.
   IF (nB := Fetch()) > 3 .AND. nB < 10
      QOut("nB=" + Str(nB, 3) + " in-range")
   ELSE
      QOut("nB=" + Str(nB, 3) + " out-of-range")
   ENDIF

RETURN

FUNCTION Fetch() AS NUMERIC
RETURN 5
