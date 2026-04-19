#!/bin/bash
# Determinism check for hb_refTabSave.
#
# hbreftab.tab is fed to md5-based convergence loops in the easipos
# scan pipeline — if two scans over identical input produce different
# byte output (hash-bucket iteration order, etc.) the loop never
# reports "converged" and always runs MAX_PASSES. hb_refTabSave sorts
# entries by name to keep output deterministic; this test guards that
# property by running the whole-codebase pre-scan twice into two
# throwaway reftabs and diffing them.

set -u
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../../.." && pwd)"
TRANSPILER="${1:-$REPO_ROOT/bin/hbtranspiler}"

TAB_A="$(mktemp)"
TAB_B="$(mktemp)"
trap 'rm -f "$TAB_A" "$TAB_B"' EXIT

for tab in "$TAB_A" "$TAB_B"; do
   : > "$tab"
   for f in "$SCRIPT_DIR"/test*.prg; do
      "$TRANSPILER" -I"$REPO_ROOT/include" "--reftab=$tab" "$f" -GF -q \
         > /dev/null 2>&1
   done
done

if diff -q "$TAB_A" "$TAB_B" > /dev/null; then
   echo "PASS: reftab output is deterministic"
   exit 0
else
   echo "FAIL: reftab output differs between two identical scans"
   diff "$TAB_A" "$TAB_B" | head -20
   exit 1
fi
