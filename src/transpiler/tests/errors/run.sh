#!/bin/bash
# Negative regression tests: each .prg in this directory must FAIL
# -GS codegen with a specific transpiler error. These cover Harbour
# language constructs that are not supported in the C# target and
# must be surfaced as errors rather than emitted as broken C#.
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

   if [ $rc -ne 0 ] && echo "$out" | grep -qE "$expect_pattern"; then
      echo "PASS: $name (expected failure surfaced)"
      pass=$((pass+1))
   else
      echo "FAIL: $name (rc=$rc, expected nonzero + /$expect_pattern/)"
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
