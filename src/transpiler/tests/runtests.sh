#!/bin/bash
# Run all transpiler tests
# Usage: ./runtests.sh [path-to-hbtranspiler]

SCRIPTDIR="$(cd "$(dirname "$0")" && pwd)"
ROOTDIR="$(cd "$SCRIPTDIR/../../.." && pwd)"
TRANSPILER="${1:-$ROOTDIR/bin/hbtranspiler}"
PASS=0
FAIL=0

# Whole-codebase pre-scan: populate hbreftab.tab with signature
# information for every test*.prg before any -GT runs. This is what
# makes cross-file by-ref data available to single-file transpiles
# (see test19a.prg for the canonical example).
HBX="$ROOTDIR/include/harbour.hbx"
for f in "$SCRIPTDIR"/test*.prg; do
   "$TRANSPILER" --hbx="$HBX" -I"$ROOTDIR/include" "$f" -GF -q > /dev/null 2>&1
done

for f in "$SCRIPTDIR"/test*.prg; do
   name=$(basename "$f" .prg)
   "$TRANSPILER" --hbx="$HBX" -I"$ROOTDIR/include" "$f" -GT 2>/dev/null
   if [ $? -eq 0 ]; then
      echo "PASS: $name"
      PASS=$((PASS + 1))
   else
      echo "FAIL: $name"
      FAIL=$((FAIL + 1))
   fi
done

echo ""
echo "Results: $PASS passed, $FAIL failed"
