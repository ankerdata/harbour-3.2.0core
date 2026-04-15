using System;
using static HbRuntime;

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
public static partial class Program
{
    public static void Main(string[] args)
    {
        decimal nVal = default;
        decimal nA = 0;
        decimal nB = 0;
        decimal nC = default;
        decimal nD = default;
        decimal nE = default;

        // --- Plain assignment statements (the common case by far) ---
        //
        // These are what :=  is actually used for 99% of the time —
        // not the assignment-in-comparison edge case further down.
        // Guards against a paren-wrapping regression going too far and
        // inserting unwanted parens around statement-position assigns.

        nA = 10;
        HbRuntime.QOUT("plain: nA=" + HbRuntime.STR(nA, 3));

        nA = Fetch();
        HbRuntime.QOUT("from-call: nA=" + HbRuntime.STR(nA, 3));

        // Chained assignment — `nC := nD := nE := 7` assigns 7 to all
        // three, right-to-left. Emitted C# needs the nested assign to
        // survive as an expression, which is a separate code path from
        // the top-level statement.
        nC = (nD = (nE = 7));
        HbRuntime.QOUT("chain: " + HbRuntime.STR(nC, 3) + HbRuntime.STR(nD, 3) + HbRuntime.STR(nE, 3));

        // Compound assignment as a statement (no surrounding comparison).
        nA += 5;
        nA *= 2;
        HbRuntime.QOUT("compound: nA=" + HbRuntime.STR(nA, 3));

        // --- Assignment-inside-expression (the original paren case) ---

        // The Fetch() below returns 5; nVal captures it, and the `> 3`
        // branch reads the captured value.
        if ((nVal = Fetch()) > 3)
        {
            HbRuntime.QOUT("fetched=" + HbRuntime.STR(nVal, 3) + " branch=big");
        }
        else
        {
            HbRuntime.QOUT("fetched=" + HbRuntime.STR(nVal, 3) + " branch=small");
        }

        // Reset nA then compound-assign in a comparison.
        nA = 0;
        if ((nA += 2) > 1)
        {
            HbRuntime.QOUT("nA=" + HbRuntime.STR(nA, 3) + " branch=big");
        }
        else
        {
            HbRuntime.QOUT("nA=" + HbRuntime.STR(nA, 3) + " branch=small");
        }

        // Assignment inside a boolean combined expression.
        if ((nB = Fetch()) > 3 && nB < 10)
        {
            HbRuntime.QOUT("nB=" + HbRuntime.STR(nB, 3) + " in-range");
        }
        else
        {
            HbRuntime.QOUT("nB=" + HbRuntime.STR(nB, 3) + " out-of-range");
        }

        return;
    }

    public static decimal Fetch()
    {
        return 5;
    }
}
