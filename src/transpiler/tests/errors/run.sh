#!/bin/bash
# Negative regression tests: each .prg in this directory must surface a
# specific warning on stderr during -GS (the code is passed per-file).
# Most are W0016 unsupported-construct cases — Harbour language features
# that can't be transpiled to C#, where the transpiler emits a `default`
# placeholder so the .cs still compiles, keeping the rest of the file's
# functions available to downstream callers (historically we hard-failed
# these, which dropped 10 real files from the easipos build and CS0103'd
# every downstream caller). Others flag a source smell that codegen
# still handles, e.g. W0023 (`@` on a never-reassigned array param).
# Tests verify the warning is surfaced, not that codegen hard-fails.
# E0100 (a single `=`) and E0101 (an error block in BEGIN SEQUENCE WITH
# other than the break idiom) are errors: the file fails, as on a syntax
# error, and the test checks the error line instead.
#
# Usage: bash tests/errors/run.sh     (HBTRANSPILER overrides the binary)

set -u
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../../../.." && pwd)"
TRANS="${HBTRANSPILER:-$REPO_ROOT/bin/hbtranspiler}"
INC="$REPO_ROOT/include"

pass=0
fail=0

run_one() {
   local prg="$1" code="$2" expect_pattern="$3"
   local name=$(basename "$prg" .prg)
   local out
   out=$("$TRANS" -I"$INC" -o"$SCRIPT_DIR/" "$prg" -GS 2>&1)
   local rc=$?

   if echo "$out" | grep -qE "(warning|Error) $code.*$expect_pattern"; then
      echo "PASS: $name ($code surfaced)"
      pass=$((pass+1))
   else
      echo "FAIL: $name (expected $code /$expect_pattern/, got rc=$rc)"
      echo "$out" | sed 's|^|  |'
      fail=$((fail+1))
   fi

   # Clean up the .cs file the transpiler may have written before the
   # error check — keeps the tree tidy for git.
   rm -f "$SCRIPT_DIR/$name.cs"
}

run_one "$SCRIPT_DIR/alias_stmt.prg" "W0016" "ALIAS (expression|reference)"
run_one "$SCRIPT_DIR/alias_expr.prg" "W0016" "ALIAS (expression|reference)"
run_one "$SCRIPT_DIR/macro_expr.prg" "W0016" "macro &"
run_one "$SCRIPT_DIR/comma_op.prg"   "W0016" "comma-operator"
run_one "$SCRIPT_DIR/array_ref_noreassign.prg" "W0023" "redundant"
run_one "$SCRIPT_DIR/hungarian_mismatch.prg"    "W0024" "contradicts its Hungarian-prefix"
run_one "$SCRIPT_DIR/equal_assign.prg"  "E0100" "as an assignment"
run_one "$SCRIPT_DIR/equal_compare.prg" "E0100" "as a comparison"
run_one "$SCRIPT_DIR/equal_for.prg"     "E0100" "in FOR"
run_one "$SCRIPT_DIR/seq_with_block.prg" "E0101" "takes only"
run_one "$SCRIPT_DIR/switch_break.prg"  "W0033" "ends a SWITCH CASE"
run_one "$SCRIPT_DIR/thread_static_init.prg" "W0034" "is initialised"
run_one "$SCRIPT_DIR/integer_fraction.prg" "W0024" "assigning a value that may hold a fraction to 'iHalf'"
run_one "$SCRIPT_DIR/integer_fraction.prg" "W0024" "'/=' may leave a fraction in 'iCount'"
run_one "$SCRIPT_DIR/integer_fraction.prg" "W0024" "'[*]=' may leave a fraction in 'iCount'"
run_one "$SCRIPT_DIR/integer_fraction.prg" "W0024" "FOR STEP that may hold a fraction for 'iPos'"
run_one "$SCRIPT_DIR/integer_fraction.prg" "W0024" "passing a value that may hold a fraction as 'iValue'"

# ...and nothing else: the file's quiet lines (Int(), a whole literal) stay quiet
count=$("$TRANS" -I"$INC" -o"$SCRIPT_DIR/" "$SCRIPT_DIR/integer_fraction.prg" -GS 2>&1 | grep -c "warning W")
rm -f "$SCRIPT_DIR/integer_fraction.cs"
if [ "$count" -eq 5 ]; then
   echo "PASS: integer_fraction (5 warnings, no more)"
   pass=$((pass+1))
else
   echo "FAIL: integer_fraction (expected 5 warnings, got $count)"
   fail=$((fail+1))
fi

echo ""
echo "Results: $pass passed, $fail failed"
[ $fail -eq 0 ]
