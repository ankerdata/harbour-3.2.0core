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

        // Inline assignment inside comparison. The Fetch() below returns 5;
        // nVal captures it, and the `> 3` branch reads the captured value.
        if ((nVal = Fetch()) > 3)
        {
            HbRuntime.QOUT("fetched=" + HbRuntime.STR(nVal, 3) + " branch=big");
        }
        else
        {
            HbRuntime.QOUT("fetched=" + HbRuntime.STR(nVal, 3) + " branch=small");
        }

        // Compound assignment in a comparison. nA += 2 returns 2; > 1 is TRUE.
        if ((nA += 2) > 1)
        {
            HbRuntime.QOUT("nA=" + HbRuntime.STR(nA, 3) + " branch=big");
        }
        else
        {
            HbRuntime.QOUT("nA=" + HbRuntime.STR(nA, 3) + " branch=small");
        }

        // Nested: assignment inside a boolean combined expression.
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
