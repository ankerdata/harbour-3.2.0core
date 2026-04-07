#!/bin/bash
# Compile all .prg test files to executables in prgexe/
# Usage: ./buildprg.sh

SCRIPTDIR="$(cd "$(dirname "$0")" && pwd)"
export PATH=$PATH:/opt/harbour/bin
mkdir -p "$SCRIPTDIR/prgexe"

PASS=0
FAIL=0

for f in "$SCRIPTDIR"/test*.prg; do
   name=$(basename "$f" .prg)
   hbmk2 "$f" -o"$SCRIPTDIR/prgexe/$name" -gtcgi -q 2>/dev/null
   if [ $? -eq 0 ]; then
      echo "PASS: $name"
      PASS=$((PASS + 1))
   else
      echo "FAIL: $name"
      FAIL=$((FAIL + 1))
   fi
done

echo ""
echo "Results: $PASS compiled, $FAIL failed"
echo "Executables in: $SCRIPTDIR/prgexe/"
