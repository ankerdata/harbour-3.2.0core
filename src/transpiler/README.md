# Harbour Transpiler

A whole-program source-to-source transpiler that turns Harbour `.prg`
code into either canonical Harbour `.hb` (round-trip) or strongly-typed
C# `.cs`.

It is built **on top of** the Harbour 3.2 compiler front-end: it reuses
the lexer, preprocessor, parser, and symbol/type machinery, but
replaces the PCODE back-end with an AST-driven code emitter. The
codegen replacement is feature-flagged behind `-DHB_TRANSPILER`, so
the transpiler is built as a separate binary (`bin/hbtranspiler`)
without disturbing the stock Harbour compiler.

The transpiler's headline feature is **whole-program static analysis**
driven by a persistent per-function signature table
([`hbreftab.tab`](hbreftab.tab)). Cross-file information — by-ref
argument passing, parameter types inferred from call sites, return
types, `NIL` comparisons — all flow through this table to produce
strongly-typed C# output from dynamically-typed Harbour source.

---

## Quick start

```bash
# Build the transpiler.
bash src/transpiler/build.sh

# Round-trip a single .prg through the transpiler back to .hb.
bin/hbtranspiler -Iinclude src/transpiler/tests/test1.prg -GT

# Generate C# from a single .prg.
bin/hbtranspiler -Iinclude src/transpiler/tests/test1.prg -GS
```

For multi-file projects, see [Whole-codebase workflow](#whole-codebase-workflow)
below — you need a one-time scan pass first.

---

## Output modes

The transpiler is invoked the same way as the Harbour compiler. The
`-G*` family of flags chooses what to emit:

| Flag  | Output                  | Description                                          |
|-------|--------------------------|------------------------------------------------------|
| `-GT` | `<file>.hb`              | Round-trip to canonical Harbour source              |
| `-GS` | `<file>.cs`              | Strongly-typed C# source                             |
| `-GF` | *(none — updates table)* | Function-table scan only; required before `-GS`/`-GT` on multi-file projects |

`-GT` is useful as a regression test: re-parsing the generated `.hb`
under stock Harbour should produce a binary that runs identically to
the binary built from the original `.prg`. The test suite enforces
this on every test (see [tests/](tests/)).

---

## Architecture

```
                      .prg source
                          │
                          ▼
              ┌──────────────────────┐
              │  preprocessor (pp)   │
              └──────────┬───────────┘
                         ▼
              ┌──────────────────────┐
              │  lexer (harbour.y)   │
              └──────────┬───────────┘
                         ▼
              ┌──────────────────────┐
              │   parser (yacc)      │  ← modified to emit AST
              └──────────┬───────────┘     when HB_TRANSPILER is set
                         ▼
                ┌──────────────────┐
                │      AST         │   hbast.c
                └────────┬─────────┘
                         │
        ┌────────────────┼────────────────┐
        ▼                ▼                ▼
   hb_compGenScan   hb_compGenTranspile  hb_compGenCSharp
   (genscan.c)      (gentr.c)            (gencsharp.c)
        │                │                │
        ▼                ▼                ▼
  hbreftab.tab         .hb              .cs
```

The AST is built once during parsing and is then walked by whichever
back-end the `-G*` flag selected. All three back-ends share the same
helper machinery in [`hbast.c`](hbast.c) and the type-inference engine
in [`hbtypes.c`](hbtypes.c).

The `-GS` and `-GT` back-ends **load** `hbreftab.tab` at startup and
consult it for every function declaration and call site. The `-GF`
scanner **writes** the table by walking the AST and running the same
inference engine that `-GS` uses.

---

## Source layout

| File                              | Purpose                                                                 |
|-----------------------------------|-------------------------------------------------------------------------|
| **Build / driver**                |                                                                          |
| [`build.sh`](build.sh)            | Single-shot clang command — produces `bin/hbtranspiler`                 |
| [`hbmain.c`](hbmain.c)            | Top-level driver: dispatches to the chosen `-G*` back-end               |
| [`cmdcheck.c`](cmdcheck.c)        | Command-line option parser (`-G*` flag handling lives here)             |
| **Front-end (largely unchanged)** |                                                                          |
| [`harbour.y`](harbour.y), [`harbour.yyc`](harbour.yyc) | Yacc grammar with AST-emitting actions guarded by `HB_TRANSPILER` |
| [`ppcomp.c`](ppcomp.c)            | Preprocessor glue                                                       |
| [`complex.c`](complex.c), [`expropta.c`](expropta.c) | Lexer / expression helpers                       |
| [`pcodestubs.c`](pcodestubs.c)    | No-op stubs for the PCODE back-end (the transpiler skips PCODE entirely)|
| [`genstubs.c`](genstubs.c)        | Stubs for the C/HRB back-ends (also unused in the transpiler)           |
| **AST + analysis**                |                                                                          |
| [`hbast.c`](hbast.c)              | AST node types, builder API (`hb_astAddX`, `hb_astBeginY`, …)           |
| [`hbtypes.c`](hbtypes.c)          | Five-pass type inference engine (see Type system below)                 |
| [`hbclsparse.c`](hbclsparse.c)    | CLASS / METHOD pre-parser                                               |
| **Function tables**               |                                                                          |
| [`hbfunctab.c`](hbfunctab.c) + [`hbfuncs.tab`](hbfuncs.tab) | Standard-library function knowledge — return types and `HbRuntime` prefix mapping. Generated by [`tools/genfunctab.py`](tools/genfunctab.py). |
| [`hbreftab.c`](hbreftab.c) + `hbreftab.tab` | User-defined function signature table. Generated by `-GF`. |
| **Back-ends**                     |                                                                          |
| [`gentr.c`](gentr.c)              | `-GT` Harbour round-trip emitter                                        |
| [`gencsharp.c`](gencsharp.c)      | `-GS` C# emitter                                                         |
| [`genscan.c`](genscan.c)          | `-GF` scan-only entry point                                             |
| **Runtime + tooling**             |                                                                          |
| [`HbRuntime.cs`](HbRuntime.cs)    | C# implementations of Harbour builtins (`QOut`, `Str`, `Len`, …)        |
| [`tools/genfunctab.py`](tools/genfunctab.py) | Generates `hbfuncs.tab` from `HbRuntime.cs` + Harbour doc blocks |
| **Tests**                         |                                                                          |
| [`tests/`](tests/)                | Numbered `.prg` test cases + drivers (`runtests.sh`, `buildprg.sh`, `buildhb.sh`, `buildcs.sh`) |

---

## The two function tables

These are the heart of how the transpiler generates *strongly-typed*
C# from a dynamically-typed source language. They live in flat text
files so they're cheap to read, easy to inspect, and trivially
overridable.

### `hbfuncs.tab` — standard library

Records two pieces of information about each known Harbour standard
library function:

```
NAME<TAB>RETTYPE<TAB>PREFIX
STR     STRING       HbRuntime
LEN     NUMERIC      HbRuntime
ABS     NUMERIC      HbRuntime
…
```

- **RETTYPE** — declared return type, used by the type-propagation
  pass in [`hbtypes.c`](hbtypes.c) so callers of e.g. `Str()` know
  the result is a `STRING`. One of `STRING`, `NUMERIC`, `LOGICAL`,
  `DATE`, `ARRAY`, `HASH`, `OBJECT`, `BLOCK`, or `-` if unknown.
- **PREFIX** — namespace to remap to when emitting C#. A row with
  `HbRuntime` produces `HbRuntime.NAME(...)` in the output. `-` means
  "leave the name alone".

The file is **generated** by [`tools/genfunctab.py`](tools/genfunctab.py)
which combines two sources of truth:

1. Every `public static` method in [`HbRuntime.cs`](HbRuntime.cs) — these
   become rows with `PREFIX=HbRuntime`.
2. Every `$SYNTAX$` block under [`doc/en/`](../../doc/en/) and
   [`contrib/.../doc/`](../../contrib/) — the token after the `-->`
   arrow is mapped through Hungarian-prefix conventions to a return
   type (`cFoo` → STRING, `nFoo` → NUMERIC, …).

Re-run after editing `HbRuntime.cs` or after pulling new doc blocks:

```bash
python3 src/transpiler/tools/genfunctab.py
```

The path the loader reads is hardcoded in [`include/hbfunctab.h`](../../include/hbfunctab.h)
as `HB_FUNCTAB_PATH`. Change it if the source tree moves.

### `hbreftab.tab` — user-defined functions

Records the **whole-program signature** of every `FUNCTION` and
`PROCEDURE` in the codebase being transpiled. This is where all the
cross-file static analysis results live.

```
NAME<TAB>FLAGS<TAB>RETTYPE<TAB>NPARAMS<TAB>PARAM_1<TAB>...<TAB>PARAM_N

  FLAGS    = "V" if variadic (uses PCount/HB_PValue/HB_Parameter/HB_AParams),
             "-" otherwise
  RETTYPE  = inferred return type (NUMERIC/STRING/...) or "-" if unknown
  PARAM    = name:type:pflags
  pflags   = a string of flag letters, or "-" for none:
               R = by-ref (some call site uses @var)
               N = nilable (body compares/assigns to NIL)
               C = conflict (two call sites disagreed on the type)
             Letters can combine in any order, e.g. "RN" or "NR".
```

Example after a scan:

```
swap         -  -        2  nX:NUMERIC:R    nY:NUMERIC:R
fred         -  -        3  nA:NUMERIC:-    nB:NUMERIC:N   nC:NUMERIC:N
doubleit     -  NUMERIC  1  n:NUMERIC:-
quadrupleit  -  NUMERIC  1  n:NUMERIC:-
```

Six pieces of cross-file information feed the emitters:

1. **By-ref slots** (`R`) — marked when *any* call site uses `@var`.
   The declaration emits `ref decimal nX`; every call site emits
   `Swap(ref a, ref b)`. See [test19a.prg](tests/test19a.prg).
2. **Declared parameter count** — lets the emitter add `= default` (or
   `= null` for nilable) to trailing params so `Fred(x)` calling
   `PROCEDURE Fred(a, b, c)` works.
3. **Parameter names** — lets the emitter switch to **C# named-argument
   syntax** when a call site has a middle gap, e.g. `Fred(x, , z)` →
   `Fred(x, nC: z)`.
4. **Nilable slots** (`N`) — detected when the function body contains
   `param == NIL`, `param != NIL`, or `param := NIL`. The slot is
   emitted as `T?` in C# so the Harbour NIL semantics is preserved.
   See [test18.prg](tests/test18.prg).
5. **Return types** — inferred from the function body and from calls
   to other functions whose return types are already known. Lets
   cross-file call chains stay strongly typed.
   See [test20a.prg](tests/test20a.prg) / [test20b.prg](tests/test20b.prg).
6. **Call-site parameter-type refinement** — when `Main` calls
   `QuadrupleIt(5)`, `QuadrupleIt`'s parameter `n` is refined from
   `USUAL` to `NUMERIC` in the table, and subsequent emitters render
   it as `decimal`. Conflicts between call sites (e.g. `Foo(42)` then
   `Foo("hello")`) produce a warning and freeze the slot as `USUAL`
   with the `C` flag set to prevent subsequent refinement attempts.

The path is hardcoded as `HB_REFTAB_PATH` in
[`include/hbreftab.h`](../../include/hbreftab.h).

#### Whole-codebase workflow

For a multi-file project, none of the cross-file information above
can be discovered by parsing each file in isolation — `module_a.prg`
might define `Swap` while the `@a` argument proving its first
parameter is by-ref lives in `module_b.prg`. The solution is a
one-time scan pass:

```bash
# 1. Build / refresh the table from the WHOLE codebase. -GF parses
#    each file but writes nothing except hbreftab.tab. Re-run any
#    time signatures or call-site usage changes.
for f in src/**/*.prg; do
   bin/hbtranspiler -GF -Iinclude "$f"
done

# 2. Now generate code. The C# emitter automatically loads
#    hbreftab.tab at startup; transpiles can run in parallel.
for f in src/**/*.prg; do
   bin/hbtranspiler -GS -Iinclude "$f"
done
```

**Convergence caveat for mutual recursion.** A single `-GF` pass over
each file walks call sites in source order. If `A` calls `B` and `B`
calls `A`, the type of whichever parameter is touched *first* might
not be fully refined until a second pass runs. For a pathological
cycle you may need to run the whole `-GF` loop twice for everything
to stabilise. For the normal case (acyclic definitions and
definition-before-use within each file) one pass suffices, and the
file-order independence is guaranteed by the three-pass structure
of [`hb_refTabCollect`](hbreftab.c) (see [Scan pipeline](#scan-pipeline)).

Working multi-file demos:

- [`test19a.prg`](tests/test19a.prg) + [`test19b.prg`](tests/test19b.prg)
  — cross-file by-ref detection (the `Swap` pair).
- [`test20a.prg`](tests/test20a.prg) + [`test20b.prg`](tests/test20b.prg)
  — cross-file return-type propagation + call-site parameter-type
  refinement (the `DoubleIt` / `QuadrupleIt` pair).

The test build scripts special-case both pairs to build a single
`test19` / `test20` executable from each.

#### Scan pipeline

`-GF` invokes [`hb_refTabCollect`](hbreftab.c) which runs three
ordered passes over the file's function list:

1. **Register** — every `FUNCTION`/`PROCEDURE` is added to the table
   with its name and parameter list. This must happen *before* any
   body walks so that source-order doesn't matter: `Main` calling `Foo`
   defined later in the same file works exactly like `Main` calling
   `Foo` from a separate file.
2. **Scan** — each body is walked for by-ref call sites, nilable
   `param != NIL` / `param := NIL` patterns, and `PCount` /
   `HB_PValue` variadic detection. Produces the per-param `R` / `N`
   flags and the function-level `V` flag.
3. **Propagate + refine** — each body is fed to `hb_astPropagate`
   with the populated table as context. This does return-type
   inference for *this* function and call-site parameter-type
   refinement for functions it *calls*. When a call site's inferred
   argument type disagrees with a previously recorded specific type
   for a slot, the slot is frozen as `USUAL` with the `C` flag and a
   warning is printed to stderr.

#### Conflict warnings

When two call sites refine the same parameter slot to incompatible
specific types, `-GF` emits one warning per conflict to stderr:

```
hbtranspiler: warning: line 7: call site passes STRING for parameter
  'Foo:x' but earlier sites had a different type — downgrading to USUAL
```

The `C` flag is set so the slot is frozen — later refinements are
silently ignored, and no repeat warnings are emitted. In the persisted
table the slot will show as e.g. `x:USUAL:C`.

Most conflicts indicate either genuinely polymorphic parameters
(`Transform(x, cPic)` where `x` can be anything) or a bug in the
source. Review each warning and, if the slot really is polymorphic,
the `C` flag already does the right thing by leaving it as `dynamic`
in the output.

---

## Type system

Harbour is dynamically typed. C# is statically typed. The transpiler
bridges this with **five passes** of inference, all driven by
[`hb_astPropagate`](hbtypes.c):

1. **Seed from declarations** — `LOCAL x := 5` infers `NUMERIC` from
   the literal; `LOCAL cFoo` infers `STRING` from the Hungarian prefix.
2. **Intra-function propagation** — walk the body looking at
   assignments. `cFoo := SomeFunc(x)` propagates `SomeFunc`'s return
   type back into `cFoo`. Iterates until fixed point.
3. **Update LOCAL/STATIC nodes** — refined types are written back into
   the AST so the emitter sees them.
4. **Return type inference** — walks `RETURN` statements and computes
   the function's return type. Arithmetic operators (`*`, `/`, `%`,
   `^`, `-`) are treated as unconditionally NUMERIC since they have
   no non-arithmetic meaning in Harbour. `+` remains polymorphic
   (string / numeric / date).
5. **Call-site parameter refinement** — walks every call site and
   refines callee parameter types in the `hbreftab.tab`-backed table.
   This is where cross-file type flow happens: typing `Main`'s
   `QuadrupleIt(5)` refines `QuadrupleIt:n` to `NUMERIC`, which in
   turn refines `DoubleIt:n` when the type inferencer later sees
   `DoubleIt(n)` inside `QuadrupleIt`'s body.

Calls to functions in `hbfuncs.tab` (standard library) or
`hbreftab.tab` (user-defined) contribute their declared return type
back into the caller's inference.

Anything still unknown after all five passes ends up as `dynamic` in
the C# output.

### Type mapping

| Harbour          | C#                                  |
|------------------|--------------------------------------|
| `NUMERIC`        | `decimal`                            |
| `STRING`         | `string`                             |
| `LOGICAL`        | `bool`                               |
| `DATE`           | `DateTime`                           |
| `ARRAY`          | `dynamic[]`                          |
| `HASH`           | `Dictionary<dynamic, dynamic>`       |
| `OBJECT`         | `object`  *(or class name if known)* |
| `BLOCK`          | `dynamic`  *(typed Func<> if args known)* |
| `USUAL` / unknown| `dynamic`                            |

Nilable parameters (those the body compares to or assigns `NIL`) are
emitted with `?` appended: `decimal?`, `string?`, etc. Non-nilable
slots stay fully strongly typed.

### `NIL` semantics

Harbour's `NIL` is a value any variable can hold. C#'s `decimal` (and
other value types) cannot hold `null` without the `?` modifier.

The scanner detects three idioms inside a function body:
- `param == NIL` / `param = NIL`
- `param != NIL`
- `param := NIL`

and marks the affected parameter as nilable in the table (`N` flag).
The C# emitter then renders that parameter as `T?` and defaults it to
`null` instead of `default`, preserving the Harbour NIL semantics.

Example: a parameter `nB` that the body compares to NIL:

```harbour
PROCEDURE Fred( nA, nB, nC )
   IF nB != NIL
      ? "got nB"
   ENDIF
RETURN
```

becomes

```csharp
public static void Fred(decimal nA = default, decimal? nB = null, decimal? nC = null)
{
    if (nB != null) { HbRuntime.QOut("got nB"); }
    ...
}
```

`nA` (never compared to NIL) stays strongly typed. `nB` and `nC` are
nullable. The standard idiom `IF param != NIL` now translates correctly.

The `HbRuntime.Str()` overload accepts `decimal?` explicitly so
nilable NUMERIC parameters can be passed without an explicit cast.
Other `HbRuntime` helpers will need similar widening as real-world
code exercises them.

See [test18.prg](tests/test18.prg) for the full demo.

---

## C# emission features

| Harbour                          | C#                                                       |
|----------------------------------|------------------------------------------------------------|
| `LOCAL x AS NUMERIC`             | `decimal x`                                               |
| `LOCAL x := 5`                   | `decimal x = 5;`                                         |
| `:=` / `==` / `=`                | `=` / `==` / `==` (assignment vs equality)               |
| `.T.` / `.F.` / `NIL`            | `true` / `false` / `null`                                |
| `.AND.` / `.OR.` / `.NOT.`       | `&&` / `\|\|` / `!`                                      |
| `^` / `$`                        | `Math.Pow()` / `.Contains()`                             |
| `IIF(c, a, b)`                   | `(c ? a : b)`                                            |
| `{\|a, b\| expr}`                | `Func<dynamic, dynamic, dynamic> = ((a, b) => expr)`     |
| `Self` / `::`                    | `this` / `this.`                                          |
| `ClassName():New()`              | `new ClassName()`                                        |
| `ClassName():New(args)`          | `(ClassName) new ClassName().New(args)`                  |
| `ACCESS` / `ASSIGN`              | C# property `{ get; set; }`                               |
| `Foo(@x)` + `PROCEDURE Foo(x)`   | `Foo(ref x)` + `Foo(ref decimal x)` (type from refTab)   |
| `Fred(x)` short call             | `Fred(x)` (relies on `= default` on declaration)         |
| `Fred(x, , z)` middle gap        | `Fred(x, nC: z)` (named argument syntax)                 |
| `IF nB != NIL`                   | `if (nB != null)` with `nB` emitted as `decimal?`        |
| Array index `a[i]`               | `a[(int)(i) - 1]` (1-based → 0-based)                    |
| Hash subscript `h["k"]`          | `h["k"]` (string keys passed through)                    |
| `? expr`                         | `HbRuntime.QOut(expr)`                                   |
| `Str(n)`, `Len(s)`, `Upper(s)`…  | `HbRuntime.Str(n)`, etc.  *(via `hbfuncs.tab`)*          |
| Comments (`*`, `&&`, `NOTE`)     | `// …`                                                    |
| `#define NAME val`               | `const T NAME = val;` inside `Program` class             |
| `#include`                       | `// #include …` (preserved as comment)                   |

### Strong typing strategy

Parameter types come from two sources, with the table winning when
available:

1. **`hbreftab.tab`** — refined type from call-site analysis. This
   is what you get for any parameter that's called with a
   strongly-typed argument somewhere in the codebase.
2. **Hungarian inference** — fallback for parameters the table has
   nothing useful for (yet).

Function declarations get `= default` on every parameter slot after
the last by-reference parameter so callers can omit trailing args.
Reference parameters can't carry defaults (C# rule), which is why the
"after the last ref" position is the cutoff. Nilable parameters get
`= null` instead of `= default`.

Standalone functions emit into a `public static partial class Program`
so multi-file projects (e.g. test19, test20) can have their separate
`Program` definitions merged into one class at the C# build step.
Single-file projects are unaffected.

---

## `.hb` round-trip emitter (`-GT`)

The `-GT` mode regenerates idiomatic Harbour from the AST. Its job is
to be the test oracle: a `.prg` → `.hb` → compile-with-stock-Harbour
loop should produce a working binary, semantically equivalent to the
direct `.prg` → compile path.

A few non-obvious things it preserves:

- **`AS TYPE` annotations** on locals, parameters, and return values
  (inferred when not in the source). Stripped at parse time by
  [`include/astype.ch`](../../include/astype.ch) which is auto-included
  by every emitted `.hb` so stock Harbour ignores the annotations.
  Parameter types come from the same `hbreftab.tab` the C# emitter
  uses, so the two outputs stay in lockstep.
- **`@` at call sites** — `Foo(@x, @y)` round-trips intact.
- **`/*@*/` markers** on parameters that the by-ref scan determined
  must be by-ref. Harbour itself has no syntax for declaring a by-ref
  parameter in a `FUNCTION`/`PROCEDURE` signature (by-ref is purely
  call-site in the language), so these are emitted as inline comments
  that stock Harbour treats as whitespace.
- **Empty argument slots** — `Fred(x, , z)` round-trips intact.

---

## Tests

```bash
cd src/transpiler/tests

# Round-trip every .prg through -GT. Exercises the .hb emitter and
# implicitly runs the scanner on every test.
bash runtests.sh

# Compile each .prg directly with stock Harbour (baseline binary).
bash buildprg.sh

# Compile each generated .hb with stock Harbour.
bash buildhb.sh

# Compile each generated .cs with `dotnet`.
bash buildcs.sh
```

Current counts (as of this README):
- `runtests.sh`: **23 passed, 0 failed**
- `buildprg.sh`: **21 compiled, 0 failed**
- `buildhb.sh`: **21 compiled, 0 failed**
- `buildcs.sh`: **21 passed, 0 failed**

The discrepancy (23 vs 21) is the test19 and test20 multi-file pairs:
each counts as two files in the round-trip pipeline (because each
`.prg` produces its own `.hb`), but only one executable in the build
pipelines (because the build scripts link the pair into a single
binary).

The test suite is intentionally small and incremental — each numbered
test exercises one feature in isolation. New tests usually expose new
limitations rather than just adding more coverage. Notable test IDs:

| Test      | Feature                                              |
|-----------|------------------------------------------------------|
| 1–9       | Core language: types, control flow, arithmetic      |
| 10–11     | Classes, constructors, `ACCESS`/`ASSIGN`, METHOD     |
| 12–16     | Assorted language features: codeblocks, IIF, etc.   |
| 17        | By-reference parameters (single-file)                |
| 18        | Default arguments, middle gaps, nilable detection    |
| 19a + 19b | **Cross-file by-reference** (multi-file pair)        |
| 20a + 20b | **Cross-file return types + param refinement**      |

---

## Limitations and future work

- **Multi-class files** — a second `CLASS` in the same `.prg`
  currently drops out of the C# output (AST nesting bug).
- **`SUPER` reference** — not handled in C# output yet.
- **Multiple inheritance** — Harbour doesn't really support it
  either; the transpiler doesn't try.
- **Macro expressions** (`&var`, `&(...)`) — emitted as comments.
  These can't be statically resolved and would need a runtime macro
  evaluator on the C# side.
- **Class-scoped method signatures** — `hbreftab.tab` currently keys
  method entries by bare method name, ignoring the owning class.
  Functions and methods called `Close` will collide. Fix when it
  actually bites: switch to `ClassName::Method` keys.
- **Two-pass `-GF` for mutual recursion** — a pathological cycle
  between files may need `-GF` run twice over all files to converge.
  Acyclic codebases don't.
- **HbRuntime widening for nilable types** — only `Str()` currently
  accepts `decimal?`. Other helpers (`Len`, `Val`, etc.) will need
  similar widening as real-world code hits them.
- **PCODE generator** — still allocated alongside the AST. Removing
  it is mostly a cleanup of `harbour.yyc` grammar actions, no
  functional change.

---

## Files NOT to edit by hand

| File                      | Source of truth                                |
|---------------------------|------------------------------------------------|
| `harboury.c`, `harboury.h`| `harbour.yyc` (run `cp harbour.yyc harboury.c` after editing) |
| `hbfuncs.tab`             | `tools/genfunctab.py`                           |
| `hbreftab.tab`            | `bin/hbtranspiler -GF`                          |

---

## Build requirements

- `clang` (the build script is hardcoded to it; gcc would also work
  with minor edits)
- macOS / Linux
- Optional: `dotnet` SDK 6.0+ for running the C# test pipeline
- Optional: Python 3.8+ for regenerating `hbfuncs.tab`
- Optional: stock `harbour` binary on the `PATH` for `buildhb.sh` and
  `buildprg.sh`
