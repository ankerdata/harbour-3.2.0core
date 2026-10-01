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

run_one "$SCRIPT_DIR/hash_key_name.prg" "W0035" "'hWrong' is given a hash keyed by numbers"
run_one "$SCRIPT_DIR/hash_key_name.prg" "W0035" "'hByName' takes a number as a key"
run_one "$SCRIPT_DIR/hash_key_name.prg" "W0035" "'hnById' takes a string as a key"
run_one "$SCRIPT_DIR/x_subscript.prg"   "W0036" "'xValue' is subscripted"
run_one "$SCRIPT_DIR/foreach_after.prg" "W0038" "'cName' is read after its FOR EACH"
run_one "$SCRIPT_DIR/foreach_after.prg" "W0038" "'cCell' is read after its FOR EACH"
run_one "$SCRIPT_DIR/foreach_integer.prg" "W0039" "FOR EACH 'nId' is an integer only by inference"

# ...and nothing else: the quiet lines stay quiet
for spec in "hash_key_name:3" "x_subscript:1" "foreach_after:2" "foreach_integer:1"; do
   name=${spec%%:*}
   want=${spec##*:}
   count=$("$TRANS" -I"$INC" -o"$SCRIPT_DIR/" "$SCRIPT_DIR/$name.prg" -GS 2>&1 | grep -c "warning W003[5-9]")
   rm -f "$SCRIPT_DIR/$name.cs"
   if [ "$count" -eq "$want" ]; then
      echo "PASS: $name ($want warnings, no more)"
      pass=$((pass+1))
   else
      echo "FAIL: $name (expected $want warnings, got $count)"
      fail=$((fail+1))
   fi
done

# W0037 reads the class rows a scan writes: -GF with a private reftab, so
# the suite's hbreftab.tab is left alone. One warning, and the quiet lines
# (HBObject's Init, a declared and an inherited member, a dynamic class)
# stay quiet.
TAB="$SCRIPT_DIR/self_undeclared.tab"
rm -f "$TAB"
out=$("$TRANS" -I"$INC" --reftab="$TAB" "$SCRIPT_DIR/self_undeclared.prg" -GF 2>&1)
rm -f "$TAB"
if echo "$out" | grep -q "warning W0037  'Device37:oDevice37'" &&
   [ "$(echo "$out" | grep -c "warning W0037")" -eq 1 ]; then
   echo "PASS: self_undeclared (W0037 surfaced, 1 warning, no more)"
   pass=$((pass+1))
else
   echo "FAIL: self_undeclared (expected one W0037, for Device37:oDevice37)"
   echo "$out" | sed 's|^|  |'
   fail=$((fail+1))
fi

# W0040 / W0041 read the class rows too (a class's parents, its members):
# two -GF passes on a private reftab, the second converged, as the scan's
# last pass decides them (scan.py's LAST_PASS_CODES).
TAB="$SCRIPT_DIR/declared_types.tab"
rm -f "$TAB"
"$TRANS" -I"$INC" --reftab="$TAB" "$SCRIPT_DIR/declared_types.prg" -GF > /dev/null 2>&1
out=$("$TRANS" -I"$INC" --reftab="$TAB" "$SCRIPT_DIR/declared_types.prg" -GF 2>&1)
rm -f "$TAB"
for expect in "W0040  'oPart' is declared Missing40" \
              "W0040  'oGhost' is declared Ghost40" \
              "W0041  assigning Other40 to 'oShape', declared Shape40" \
              "W0041  assigning STRING to 'oShape', declared Shape40" \
              "W0041  putting NUMERIC into 'aShapes', declared Shape40" \
              "W0041  putting Other40 into 'aShapes', declared Shape40" \
              "W0041  passing STRING as 'oShape' to 'Take40', declared Shape40" \
              "W0041  returning STRING from 'Make40', declared Shape40"; do
   if echo "$out" | grep -qF "$expect"; then
      echo "PASS: declared_types ($expect)"
      pass=$((pass+1))
   else
      echo "FAIL: declared_types (expected: $expect)"
      echo "$out" | sed 's|^|  |'
      fail=$((fail+1))
   fi
done
count=$(echo "$out" | grep -c "warning W004[01]")
if [ "$count" -eq 8 ]; then
   echo "PASS: declared_types (8 warnings, no more)"
   pass=$((pass+1))
else
   echo "FAIL: declared_types (expected 8 warnings, got $count)"
   echo "$out" | grep "warning" | sed 's|^|  |'
   fail=$((fail+1))
fi

echo ""
echo "Results: $pass passed, $fail failed"
[ $fail -eq 0 ]
