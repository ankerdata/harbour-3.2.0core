# harbour-core — working notes for Claude (transpiler branch)

This checkout carries Alex's Harbour→C# transpiler in `src/transpiler/`
on top of upstream Harbour. Upstream files elsewhere are not ours;
`doc/todo.txt`, `NIX.md` etc. are upstream. The pipeline that drives
the transpiler over EasiPOS lives in the sibling `easipos-transpiled`
repo (see its CLAUDE.md and README.md).

## Build and test

```
cp src/transpiler/harbour.yyc src/transpiler/harboury.c   # ALWAYS after editing harbour.yyc
bash src/transpiler/build.sh                              # → bin/hbtranspiler (macOS/Linux)
src\transpiler\build.bat                                  # → bin/hbtranspiler.exe (Windows/MSVC)
bash src/transpiler/tests/verify.sh                       # full suite (~60 s)
```

- The build compiles `harboury.c`, not `harbour.yyc`. Forgetting the
  copy makes grammar edits silently no-ops.
- `build.sh`'s source list is the truth, and `build.bat` carries the
  same list — a new `.c` must be added to BOTH or the Windows build
  breaks at link time. The transpiler is AST-only:
  `pcodestubs.c` replaces `hbpcode.c`; base expression constructors
  live in the prebuilt `libhbcommon.a`, so hooks must go in compiled
  TUs (`complex.c`, `harboury.c`, `gencsharp.c`), not `src/common/`.
- `gencsharp.c` compiles with `-w` — a duplicate tentative definition
  once merged two registries silently (SIGSEGV). Keep an eye on it.
- Windows build works (`build.bat`, MSVC/x86, links the `hbcommon` +
  `hbnortl` that `call-win-make.bat` produces). Verified by
  regenerating `tests/hbout/` + `tests/csout/` — 186 tracked reference
  files, byte-identical to the macOS output.
- On an ARM64 Windows host, prefer the native build: run
  `call-win-make-arm64.bat`, then `build.bat` with `HB_ARCH=arm64`,
  `HB_LIBDIR=lib\win\msvcarm64` and
  `HB_OUT=bin\hbtranspiler-arm64.exe`. The x86 binary runs under
  emulation and is ~2x slower (easipos scan 21m47s vs 10m52s); output
  is byte-identical. `build.bat` defaults to x86 deliberately —
  `PROCESSOR_ARCHITECTURE` reads `AMD64` inside an emulated shell, so
  auto-detection would silently pick the wrong target.

Run modes: `-GS` C# emit, `-GT` Harbour round-trip (emits `.prg` into
`-o<dir>/` — without `-o` it overwrites the input), `-GF -q`
scan-only (populate `--reftab=`). Flags the pipeline always passes:
`--reftab --preload-list --var-types --defines-map --fieldtypes
--filename-casing -DECR -DMULTITHREAD` plus the include paths — see
`easipos-transpiled/scripts/transpile_common.py` for the single
canonical invocation.

## Rules Alex has set

- **Never disable a test to get green.** Fix the code, fix the test,
  or delete it with a commit message saying why. No skip lists — a
  visible `FAIL:` line forces the conversation.
- **Check every stage of verify.sh**, not the last line. Grep the log
  for `FAIL:|differ|Results:|exited non-zero` and confirm the exit
  code.
- **Don't re-run verify.sh to look at the same failure twice.**
  Diagnose from the output already on disk (`tests/*.cs`, `*.hb`, the
  dotnet output); re-run only the narrow test.
- **Transpile, don't infer.** The class parser recognises the keywords
  the source wrote (CLASS, VAR, METHOD, INIT, ACCESS…) and never
  supplies semantics the source didn't state (e.g. binding a bare
  `METHOD` to the last CLASS the way hbclass.ch would). If a file needs
  preprocessor magic to make sense, it fails the scan and gets fixed in
  source.
- **Preserve AST signals before re-deriving syntax.** Parens
  (single-element `HB_ET_LIST`), declaration-site casing (`pParams`),
  by-ref/nilable/conflict bits (reftab slots), `fArrayDim` — the parser
  already captured them. Over-paren / over-cast bugs come from ignoring
  an existing node; grep `include/hbcompdf.h`, `include/hbexprb.h`,
  `hbreftab.c` first.
- **Never commit on your own**; report what's ready and wait. Routine
  build/verify commands run without asking.
- **HbRuntime.cs** (`src/transpiler/HbRuntime.cs`, linked into the
  easipos csproj — single source of truth) is Harbour builtins only,
  UPPERCASE names. Application types are fixed in the .prg, not
  stubbed here. Overloads, when justified, are generic `<T>`, never
  `dynamic`.

## Architecture pointers

- `hbreftab.c` — cross-file reftab: function signatures, param slots
  (type, ref/nilable/conflict flags), class member registry
  (`Class::member`), def-class parents. Converges over scan passes.
- `hbtypes.c` — inference: Hungarian seeding, call-site propagation,
  INTEGER (Int64) candidacy (Pass 2.5: index/sink shaped locals;
  fractional or compound-division writes disqualify), ORM def-class
  member typing via `hbfieldtypes.c`, depth-capped nested receiver
  resolution (NOT recursive `hb_astInferExprType` on receivers — that
  was a 0.08 s → 60 s blowup), W0022/W0024/W0028–W0032 checks.
- `gencsharp.c` — emitter. Numerics are `decimal`, INTEGER is `long`,
  `/` on two integral operands emits `(decimal)(a) / b` (Harbour
  division is always float), subscripts `[(long)(expr) - 1]`.
- `hbdefinemap.c` / `tools/gendefines.py` — `#define` → typed C# consts
  (int-valued → long, fractional → decimal) + `defines_map.txt`.
- `hbfieldtypes.c` — ORM def contracts from `fieldtypes.tsv`
  (class, accessor, type, inherits).
- Warnings: W0016 unsupported construct, W0018 arity (emit-phase only),
  W0020 by-ref passed by value (`/*@*/` marker = deliberate, both
  declaration and call site), W0021 missing Hungarian, W0022 param
  type conflict → USUAL, W0024 Hungarian contradicted, W0025 name-
  seeded class contradicted, W0026 int candidate blocked, W0028–W0032
  ORM contract violations / scalar-named receiver.
- Tests: `src/transpiler/tests/testNN.prg` (positive, compared as .hb
  and .cs and run) + `tests/errors/*.prg` (must fail with a specific
  error). Add a test for every emitter/inference change.
