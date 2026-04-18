#!/bin/bash
# Negative regression tests: each .prg in this directory must surface
# a specific unsupported-construct warning on stderr during -GS. These
# cover Harbour language constructs that can't be transpiled to C#.
# The transpiler emits a `default` placeholder and a `warning W0016`
# so the .cs still compiles, keeping the rest of the file's functions
# available to downstream callers — historically we hard-failed these
# files, which dropped 10 real files from the easipos build and made
# every downstream caller CS0103. Tests verify that the warning is
# still surfaced, not that codegen hard-fails.
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
   local prg="$1" expect_pattern="$2"
   local name=$(basename "$prg" .prg)
   local out
   out=$("$TRANS" -I"$INC" -o"$SCRIPT_DIR/" "$prg" -GS 2>&1)
   local rc=$?

   if echo "$out" | grep -qE "warning W0016.*$expect_pattern"; then
      echo "PASS: $name (warning surfaced)"
      pass=$((pass+1))
   else
      echo "FAIL: $name (expected warning W0016 /$expect_pattern/, got rc=$rc)"
      echo "$out" | sed 's|^|  |'
      fail=$((fail+1))
   fi

   # Clean up the .cs file the transpiler may have written before the
   # error check — keeps the tree tidy for git.
   rm -f "$SCRIPT_DIR/$name.cs"
}

run_one "$SCRIPT_DIR/alias_stmt.prg" "ALIAS (expression|reference)"
run_one "$SCRIPT_DIR/alias_expr.prg" "ALIAS (expression|reference)"
run_one "$SCRIPT_DIR/macro_expr.prg" "macro &"
run_one "$SCRIPT_DIR/comma_op.prg"   "comma-operator"

echo ""
echo "Results: $pass passed, $fail failed"
[ $fail -eq 0 ]
