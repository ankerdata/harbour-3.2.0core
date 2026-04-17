using System;
using static HbRuntime;

// Test 25: four-bug regression test for the C# emitter.
//
// Each section exercises one of the fixes landed in gencsharp.c /
// HbRuntime.cs. A regression on any of them shows up as a runtests
// failure (parse/transpile) or a buildcs failure (dotnet build).
//
//   A. Case normalization in hb_csFuncMap. Harbour is case-insensitive
//      for identifiers, so real code uses every variation (INT/Int/int).
//      The emitter uppercases remapped runtime calls so they all
//      resolve to the UPPERCASE methods in HbRuntime.cs. Before:
//      `int(x)` → `HbRuntime.int(x)` (reserved-word + missing method).
//
//   B. Numeric #define with trailing `//` comment. Before: the emitter
//      pasted `m;` onto the raw remainder of the define, so both the
//      `m` suffix and the terminating `;` got swallowed by the line
//      comment. After: the numeric literal is parsed out separately
//      and any trailing comment is emitted after the terminator.
//
//   C. String #define with characters that need C# escaping — notably
//      backslash. Before: the emitter passed the Harbour string through
//      verbatim, producing a broken C# literal. After: emitted as a
//      verbatim string (`@"..."`), a perfect semantic match for
//      Harbour's no-escape string literals.
//
//   D. HB_ET_ALIASVAR with implicit HB_ET_ALIAS on the alias side. The
//      Harbour parser auto-wraps identifiers it can't resolve against
//      the current scope as implicit memvar aliases. A case-typo'd
//      local reference lands there. Before: the emitter dropped
//      `/* ALIAS: /* unknown expr type 26 */->name */` which both
//      nested comments and leaked `->` into live code. After: the
//      emitter does a case-insensitive lookup against the current
//      function's locals and emits the canonical name.

// Bug B — numeric #define with `m` suffix path (has decimal point)

// Bug B — numeric #define without `m` suffix path (no decimal point)

// Bug C — string #define with an embedded backslash
public static partial class Program
{
    const decimal PI = 3.14m; // pi-ish, inline comment exercises the split
    const decimal MAGIC = 42; // the answer, plus a trailing comment
    const string SEPARATOR = @"\";
    public static void Main(string[] args)
    {
        string cName = "harbour";

        // Bug A — every source-level casing variation of the same builtins.
        // Post-fix all four variants emit as `HbRuntime.STR` / `.UPPER` /
        // `.LEN` / `.INT`, matching the UPPERCASE HbRuntime.cs methods.
        HbRuntime.QOUT("pi:     " + HbRuntime.STR(PI, 8, 2));
        HbRuntime.QOUT("magic:  " + HbRuntime.STR(MAGIC, 8));
        HbRuntime.QOUT("upper:  " + HbRuntime.UPPER(cName));
        HbRuntime.QOUT("upper2: " + HbRuntime.UPPER(cName));
        HbRuntime.QOUT("str:    " + HbRuntime.STR(MAGIC, 8));
        HbRuntime.QOUT("len:    " + HbRuntime.STR(HbRuntime.LEN(cName), 4));
        HbRuntime.QOUT("int:    " + HbRuntime.STR(HbRuntime.INT(PI + 0.7m), 4));
        HbRuntime.QOUT("INT:    " + HbRuntime.STR(HbRuntime.INT(PI + 0.7m), 4));

        // Bug C — round-trip the backslash #define through string concat.
        HbRuntime.QOUT("sep:    " + "a" + SEPARATOR + "b");

        return;

        // Bug D — exercised at compile time only. This function is defined but
        // never called, so the runtime behavior of the case-typo'd reference
        // cannot diverge between pipelines. What matters is that the parser
        // accepts it and the C# emitter resolves `alocal` to the canonical
        // `aLocal` via hb_csResolveLocal, instead of leaking an unknown-expr
        // comment into live code.
    }
    public static dynamic test25_BugDCheck()
    {
        dynamic[] aLocal = new dynamic[] { 1, 2, 3 };
        return aLocal[0];
    }
}
