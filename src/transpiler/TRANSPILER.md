# Harbour Transpiler — Remaining Steps

## Current State (Phase 1 Complete)

The transpiler pipeline works end-to-end. Harbour source is parsed through the full compiler frontend, an AST is built from expression statements, and valid Harbour code is emitted. The transpiled output compiles and runs.

**Working:** function/procedure declarations, LOCAL variable declarations (without initializers), expression statements (assignments, function calls, compound assignments), string/numeric/logical literals, binary and unary operators, method calls, array access, macros, codeblocks.

**Architecture (current):** Source → Preprocessor → Lexer → Parser → AST → gentr.c → Output

**Invocation:** `hbtranspiler input.prg -GT` produces `input.hb`

---

## Phase 2: Remove Preprocessor

**Goal:** The transpiler reads source code without any preprocessing. The output should look like the input, not like expanded macro output. `#include` directives, `#command`/`#translate` rules, `#define` macros — none are executed. The source is taken as-is.

**Why:** The preprocessor obscures the original code structure:
- `? x` becomes `QOUT(x)` — loses the `?` syntax
- `SET COLOR TO "W/B"` becomes `SETCOLOR("W/B")` — loses the command form
- `CLASS MyClass` / `DATA` / `METHOD` / `ENDCLASS` becomes `__clsNew()`, `__clsAddMsg()` — completely unrecognisable
- `#include "hbclass.ch"` pulls in hundreds of rules and disappears from the output

For transpilation to C#, the original source structure is what matters. The preprocessor directives used in the target codebase are not expected to be a problem.

**What to do:**
- Bypass the preprocessor entirely in the transpiler pipeline
- The lexer (`complex.c`) currently receives tokens from the preprocessor. Change it to tokenize source directly using a simpler tokenizer, or use the PP tokenizer in a pass-through mode that tokenizes but does not preprocess
- The PP token layer (`hb_pp_getLine()` in `ppcore.c`) handles tokenization including string literals, operators, keywords, and comments. This tokenization is valuable and should be kept. Only the translation/command/define/include processing should be skipped.

**Implementation approach:**
- In the transpiler's copy of `ppcomp.c`, skip `hb_pp_setStdRules()` and all `hb_pp_readRules()` calls — this means no `#command`/`#translate` rules are loaded
- Skip `#include` file expansion — when the PP encounters `#include`, pass the directive through as a token instead of opening the file
- Skip `#define` macro expansion — `hb_pp_processDefine()` is not called
- The tokenizer still runs, so tokens are properly classified (keywords, strings, operators, numbers)

**New pipeline:** Source → Tokenizer (PP tokenization only, no expansion) → Lexer → Parser → AST → Output

**Consequence:** The parser will encounter syntax it doesn't understand (`SET...TO`, `?`, `CLASS...ENDCLASS`, `@...SAY`, etc.). This is addressed in Phase 2a.

**Files:** `src/transpiler/ppcomp.c`, `src/transpiler/ppcore.c` (if needed for #include passthrough)

---

## Phase 2a: Token-Level Pattern Recognition

**Goal:** Handle Harbour constructs that the yacc parser doesn't understand because they were previously handled by preprocessor `#command`/`#translate` rules.

**What to do:**
- Build a pre-parse pass that operates on the token stream before the yacc parser
- Recognise known patterns and build AST nodes directly:

| Pattern | AST Node |
|---------|----------|
| `? expr, expr, ...` | `HB_AST_QOUT` |
| `?? expr, expr, ...` | `HB_AST_QQOUT` |
| `SET <keyword> TO <expr>` | `HB_AST_SET` (new) |
| `@ row, col SAY expr` | `HB_AST_SAY` (new) |
| `@ row, col GET var` | `HB_AST_GET` (new) |
| `CLASS name [INHERIT parent]` | `HB_AST_CLASS` (new) |
| `DATA name [AS type] [INIT expr]` | `HB_AST_CLASSDATA` (new) |
| `METHOD name(params) [CLASS name]` | `HB_AST_METHOD` (new) |
| `ENDCLASS` | closes `HB_AST_CLASS` |
| `#include "file"` | `HB_AST_INCLUDE` (new, passthrough) |
| `#define name value` | `HB_AST_PPDEFINE` (new, passthrough) |

- Tokens that don't match any command pattern are passed through to the yacc parser as before (assignments, expressions, function calls, control flow)
- This can be implemented as a function that consumes tokens and either builds AST nodes directly or feeds them to `hb_comp_yyparse()`

**Incremental approach:** Start with the patterns that appear in the target codebase. Add more as needed. Unknown commands can initially be emitted as comments or as-is.

**Files:** new `src/transpiler/hbpreparse.c`, `hbast.h` (add new node types), `gentr.c` (add emitters)

---

## Phase 2b: Preserve Name Case

**Goal:** Variable and function/procedure names retain their original case from source code. Currently all identifiers are emitted as uppercase (`X` instead of `x`, `HELLO` instead of `Hello`).

**Root cause:** The lexer in `complex.c` line 852 calls `hb_pp_tokenUpper(pToken)` on every keyword token before it enters the parser. This uppercases all identifiers before they are stored.

**What to do:**
- In the transpiler's copy of `complex.c`, skip the `hb_pp_tokenUpper()` call when `HB_TRANSPILER` is defined
- Guard with `#ifndef HB_TRANSPILER` around line 852 so identifiers flow through with original case
- The keyword lookup table (`s_keytable`) uses uppercase, so keyword matching needs a case-insensitive comparison — uppercase a temporary copy for lookup only while preserving the original
- Verify that the identifier cache (`hb_compIdentifierNew`) stores the original-case name

**Impact:** After this change, `LOCAL nTotal := 0` emits as `LOCAL nTotal := 0` instead of `LOCAL NTOTAL := 0`. Function names like `CalcTotal()` are preserved. This is essential for Hungarian notation prefix detection (Phase 2c) and readable C# output.

**Files:** `src/transpiler/complex.c`

**Test:** `LOCAL nValue := 5` → emitted as `LOCAL nValue := 5`

---

## Phase 2c: Type Inference from Hungarian Notation and Initializers

**Goal:** Emit `AS <type>` declarations on all variables, inferred from three sources in priority order:

1. **Initializer expression type** (most reliable)
2. **Hungarian notation prefix** (developer convention)
3. **Fallback:** `AS USUAL`

**Hungarian notation prefixes:**

| Prefix | Type | Harbour AS | C# Type |
|--------|------|-----------|---------|
| `n` | Numeric | `AS INTEGER` or `AS DECIMAL` (see below) | `int` or `decimal` |
| `c` | Character/String | `AS STRING` | `string` |
| `l` | Logical | `AS LOGICAL` | `bool` |
| `a` | Array | `AS ARRAY` | `object[]` |
| `o` | Object | `AS OBJECT` | `object` (or class name) |
| `d` | Date | `AS DATE` | `DateTime` |
| `h` | Hash | `AS HASH` | `Dictionary<object,object>` |
| `b` | Block/Codeblock | `AS BLOCK` | `Func<>` / `Action` |
| `t` | Timestamp | `AS TIMESTAMP` | `DateTime` |
| `x` | Any/variant | `AS USUAL` | `dynamic` |

**Integer vs Decimal distinction:**

When type is numeric (from prefix `n` or initializer), further refine:
- Initializer is `HB_ET_NUMERIC` with `NumType == HB_ET_LONG` → `AS INTEGER`
- Initializer is `HB_ET_NUMERIC` with `NumType == HB_ET_DOUBLE` → `AS DECIMAL`
- Hungarian `n` prefix with no initializer → `AS NUMERIC` (resolved later from usage)
- If assigned from a function call or expression → `AS NUMERIC` (can't distinguish)

**Implementation:**

Create a type inference module `hbtypes.c` with:
```c
const char * hb_astInferType( const char * szName, PHB_EXPR pInit );
```

**Emitter output:**
```harbour
LOCAL nTotal := 0 AS INTEGER
LOCAL cName := "hello" AS STRING
LOCAL lFound AS LOGICAL
LOCAL xValue AS USUAL
```

**Files:** new `src/transpiler/hbtypes.c`, modify `gentr.c` (LOCAL emitter), modify `hbast.h` (declare inference function)

**Test:** `LOCAL nCount := 10` → `LOCAL nCount := 10 AS INTEGER`

---

## Phase 3: Variable Declarations with Initializers

**Goal:** `LOCAL x := 5, y := "hello"` emits with initializers intact.

**What to do:**
- Hook into the grammar's `VarDef` / `VarList` rules in `harbour.yyc` to capture initializer expressions and associate them with variable names
- Add `hb_astAddLocal()` calls when LOCAL variables are declared with `:=` initializers
- Extend emitter to output `LOCAL x := 5` instead of separate `LOCAL X` and `X := 5`
- Handle STATIC, FIELD, MEMVAR, PRIVATE, PUBLIC declarations similarly
- Integrate type inference (Phase 2c) — every declaration gets an `AS <type>` suffix

**Files:** `harbour.yyc` (VarDef rules around line 5300+), `hbast.c`, `gentr.c`

**Test:** `LOCAL nCount := 5, cName := "hello"` → `LOCAL nCount := 5 AS INTEGER, cName := "hello" AS STRING`

---

## Phase 4: IF / ELSEIF / ELSE / ENDIF

**Goal:** Control flow structures are captured in the AST and emitted.

**What to do:**
- Hook into `IfBegin`, `IfElse`, `IfElseIf`, `IfEndif` rules in `harbour.yyc`
- Use the AST block stack: push on `IfBegin`, pop/swap on `IfElse`/`IfElseIf`, pop/assemble on `IfEndif`
- Build `HB_AST_IF` nodes with condition, then-block, elseif-chain, else-block
- The condition expressions are already `PHB_EXPR` trees — store them in the AST node

**Key grammar cases in harbour.yyc:**
- Case ~103 (`IfBegin`): save condition, push block for then-body
- Case ~105 (`IfElse`): pop then-body, push block for else-body
- Case ~107 (`IfElseIf`): pop current body, save new condition, push new block
- Case ~108 (`IfEndif`): pop, assemble complete IF node, append to parent

**Files:** `harbour.yyc`, `hbast.c` (add `hb_astBeginIf`, `hb_astElse`, `hb_astElseIf`, `hb_astEndIf`), `gentr.c` (already has IF emitter)

**Test:** `IF x > 0` / `ELSEIF x < 0` / `ELSE` / `ENDIF`

---

## Phase 5: DO WHILE / ENDDO

**What to do:**
- Hook into `WhileBegin` / `EndWhile` rules in `harbour.yyc`
- Push block on begin, pop and build `HB_AST_DOWHILE` on end
- Capture the condition expression

**Files:** `harbour.yyc`, `hbast.c`, `gentr.c` (already has DOWHILE emitter)

**Test:** `DO WHILE x > 0` / `x--` / `ENDDO`

---

## Phase 6: FOR / NEXT

**What to do:**
- Hook into `ForBegin` / `ForNext` rules in `harbour.yyc`
- Capture loop variable name, start expression, TO expression, optional STEP expression
- Build `HB_AST_FOR` node with body

**Files:** `harbour.yyc`, `hbast.c`, `gentr.c` (already has FOR emitter)

**Test:** `FOR i := 1 TO 10 STEP 2` / `? i` / `NEXT`

---

## Phase 7: FOR EACH / NEXT

**What to do:**
- Hook into `ForEach` / `ForEachNext` rules in `harbour.yyc`
- Capture iterator variable(s), enumeration expression(s), direction
- Build `HB_AST_FOREACH` node

**Files:** `harbour.yyc`, `hbast.c`, `gentr.c`

**Test:** `FOR EACH x IN {1, 2, 3}` / `? x` / `NEXT`

---

## Phase 8: DO CASE / CASE / OTHERWISE / ENDCASE

**What to do:**
- Hook into `CaseBegin`, `CaseStatement`, `OtherwiseStatement`, `EndCase` rules
- Build chain of `HB_AST_CASE` nodes linked together
- Wrap in `HB_AST_DOCASE` with optional OTHERWISE block

**Files:** `harbour.yyc`, `hbast.c`, `gentr.c`

**Test:** `DO CASE` / `CASE x == 1` / `CASE x == 2` / `OTHERWISE` / `ENDCASE`

---

## Phase 9: SWITCH / CASE / DEFAULT / ENDSWITCH

**What to do:**
- Hook into `SwitchBegin`, `SwitchCase`, `SwitchDefault`, `SwitchEnd` rules
- Build `HB_AST_SWITCH` node with case list and default block

**Files:** `harbour.yyc`, `hbast.c`, `gentr.c`

---

## Phase 10: EXIT, LOOP, BREAK

**What to do:**
- Hook into the EXIT, LOOP, BREAK rules in `harbour.yyc` (cases ~71, ~72, ~79)
- Build simple `HB_AST_EXIT`, `HB_AST_LOOP`, `HB_AST_BREAK` nodes
- BREAK optionally carries an expression

**Files:** `harbour.yyc`, `hbast.c`, `gentr.c` (already has emitters for these)

---

## Phase 11: BEGIN SEQUENCE / RECOVER / ALWAYS / END

**What to do:**
- Hook into `SeqBegin`, `SeqRecover`, `SeqAlways`, `SeqEnd` rules
- Build `HB_AST_BEGINSEQ` node with body, recover block (optional USING variable), always block

**Files:** `harbour.yyc`, `hbast.c`, `gentr.c`

---

## Phase 12: WITH OBJECT / END WITH

**What to do:**
- Hook into `WithObject` / `EndWith` rules
- Build `HB_AST_WITHOBJECT` node with object expression and body

**Files:** `harbour.yyc`, `hbast.c`, `gentr.c`

---

## Phase 13: RETURN with Value

**Status:** Hooks are in `harbour.yyc` (cases 73 and 75) but the captured expression pointer may be stale if PCODE generation modifies it. Verify that RETURN expressions are emitted correctly for both `RETURN` (no value) and `RETURN expr`.

**Test:** `FUNCTION Add(a, b)` / `RETURN a + b`

---

## Phase 14: Full Harbour Identity Verification

**Goal:** Run the transpiler on real-world Harbour programs and verify the output looks like the input.

**What to do:**
- Create a test suite in `src/transpiler/tests/` with `.prg` files of increasing complexity
- Write a shell script that: transpiles each file, diffs input vs output
- Since the preprocessor is no longer invoked, the output should closely match the input (modulo formatting and type annotations)
- Fix any discrepancies found

---

## Phase 15: Remove PCODE Dependency

**Goal:** Once all grammar rules have AST hooks, remove PCODE generation entirely.

**What to do:**
- Replace all PCODE-generating grammar actions in `harbour.y` with AST-only actions
- Regenerate `harbour.yyc` from the modified `harbour.y`
- Remove PCODE files from Makefile: `hbpcode.c`, `hbopt.c`, `hbdead.c`, `hbfix.c`, `hbstripl.c`, `hblbl.c`, `genstubs.c`
- Remove the deferred expression freeing hack in `hbcomp.c`
- Remove `expropta.c`/`exproptb.c` — expressions are no longer consumed by PCODE generation
- Clean up `hbmain.c` — remove optimization passes and PCODE finalization

---

## Phase 16: C# Output

**Goal:** Add a C# code emitter alongside the Harbour emitter.

**What to do:**
- Create `gencsharp.c` (or add a mode flag to `gentr.c`)
- Map Harbour constructs to C# equivalents, using type inference from Phase 2c:

| Harbour | C# |
|---------|-----|
| `FUNCTION name(params)` | `static dynamic name(params)` (or typed return if inferable) |
| `PROCEDURE name(params)` | `static void name(params)` |
| `LOCAL nCount := 5 AS INTEGER` | `int nCount = 5;` |
| `LOCAL cName := "hi" AS STRING` | `string cName = "hi";` |
| `LOCAL lDone AS LOGICAL` | `bool lDone = false;` |
| `LOCAL xVal AS USUAL` | `dynamic xVal = null;` |
| `LOCAL nPrice := 9.99 AS DECIMAL` | `decimal nPrice = 9.99m;` |
| `x := expr` | `x = expr;` |
| `IF / ELSEIF / ELSE / ENDIF` | `if / else if / else` |
| `DO WHILE / ENDDO` | `while (cond) { }` |
| `FOR i := 1 TO 10` | `for (int i = 1; i <= 10; i++)` |
| `FOR EACH x IN arr` | `foreach (dynamic x in arr)` |
| `DO CASE / CASE / ENDCASE` | `if / else if / else` |
| `SWITCH / ENDSWITCH` | `switch { }` |
| `? expr` | `Console.WriteLine(expr);` |
| `BEGIN SEQUENCE / RECOVER / END` | `try / catch / finally` |
| `.T.` / `.F.` | `true` / `false` |
| `NIL` | `null` |
| `.AND.` / `.OR.` / `.NOT.` | `&&` / `\|\|` / `!` |
| `:=` | `=` |
| `==` | `==` |
| `!=` / `<>` | `!=` |
| `{1, 2, 3}` | `new object[] {1, 2, 3}` |
| `{key => val}` | `new Dictionary<object,object> {{key, val}}` |
| `obj:Method(args)` | `obj.Method(args)` |
| `@func()` | `new Action(func)` / delegate |
| `{&vert;x&vert; expr}` | `(x) => expr` |
| `CLASS name INHERIT parent` | `class name : parent` |
| `DATA name AS type` | field/property declaration |
| `METHOD name(params)` | method declaration |
| `SET COLOR TO x` | `Harbour.SetColor(x);` |
| `@ row, col SAY expr` | `Harbour.DevPos(row, col); Harbour.DevOut(expr);` |

- Add a Harbour runtime support library for C# that provides `QOut`, `SetColor`, `SubStr`, `Len`, etc.
- Handle Harbour's dynamic typing: use inferred types where possible, `dynamic` for `AS USUAL`
- Add `-GS` flag for C# output (or reuse `-GT` with a sub-option)

---

## Build Command Reference

```bash
# Build the transpiler
clang -I./include -Isrc/transpiler -DHB_TRANSPILER -fno-common -w -O3 \
  -o /tmp/hbtranspiler \
  src/transpiler/cmdcheck.c src/transpiler/complex.c \
  src/transpiler/expropta.c src/transpiler/gentr.c \
  src/transpiler/genstubs.c src/transpiler/hbast.c \
  src/transpiler/hbclsparse.c src/transpiler/hbcomp.c \
  src/transpiler/hbmain.c src/transpiler/hbtypes.c \
  src/transpiler/ppcomp.c src/transpiler/harboury.c \
  src/compiler/compi18n.c src/compiler/exproptb.c \
  src/compiler/hbdbginf.c src/compiler/hbdead.c \
  src/compiler/hbfix.c src/compiler/hbfunchk.c \
  src/compiler/hbgenerr.c src/compiler/hbident.c \
  src/compiler/hblbl.c src/compiler/hbopt.c \
  src/compiler/hbpcode.c src/compiler/hbstripl.c \
  src/compiler/hbusage.c \
  src/main/harbour.c src/pp/ppcore.c \
  -L./lib/darwin/clang -lhbnortl -lhbcommon -lm

# Run the transpiler
/tmp/hbtranspiler input.prg -GT

# Compile and run the transpiled output
hbmk2 input.hb -run
```

## Important Notes

- **Always sync `harboury.c` from `harbour.yyc`** after editing grammar actions:
  ```bash
  cp src/transpiler/harbour.yyc src/transpiler/harboury.c
  ```
  The build compiles `harboury.c`, not `harbour.yyc`. If you forget this step, your grammar changes will have no effect.

- **`-DHB_TRANSPILER`** must be passed to the compiler. This enables the AST state in `HB_COMP`, the `HB_LANG_TRANSPILE` enum value, the expression statement hook in `hbexpra.c`, and the case-insensitive keyword matching in `complex.c`.

- **`src/pp/ppcore.c`** is compiled directly into the transpiler binary (not linked from `libhbpp.a`) because the passthrough flag was added there. If `ppcore.c` is updated in `src/pp/`, the transpiler gets the changes automatically.
