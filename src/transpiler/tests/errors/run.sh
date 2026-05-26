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
#
# Usage: bash tests/errors/run.sh

set -u
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../../../.." && pwd)"
TRANS="$REPO_ROOT/bin/hbtranspiler"
INC="$REPO_ROOT/include"

pass=0
fail=0

run_one() {
   local prg="$1" code="$2" expect_pattern="$3"
   local name=$(basename "$prg" .prg)
   local out
   out=$("$TRANS" -I"$INC" -o"$SCRIPT_DIR/" "$prg" -GS 2>&1)
   local rc=$?

   if echo "$out" | grep -qE "warning $code.*$expect_pattern"; then
      echo "PASS: $name (warning surfaced)"
      pass=$((pass+1))
   else
      echo "FAIL: $name (expected warning $code /$expect_pattern/, got rc=$rc)"
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

echo ""
echo "Results: $pass passed, $fail failed"
[ $fail -eq 0 ]
