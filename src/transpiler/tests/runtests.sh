#!/bin/bash
# Run all transpiler tests
# Usage: ./runtests.sh [path-to-hbtranspiler]

SCRIPTDIR="$(cd "$(dirname "$0")" && pwd)"
ROOTDIR="$(cd "$SCRIPTDIR/../../.." && pwd)"
TRANSPILER="${1:-$ROOTDIR/bin/hbtranspiler}"
PASS=0
FAIL=0

for f in "$SCRIPTDIR"/test*.prg; do
   name=$(basename "$f" .prg)
   "$TRANSPILER" -I"$ROOTDIR/include" "$f" -GT 2>/dev/null
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
