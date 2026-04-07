#!/bin/bash
# Compile all .hb transpiled files to executables in hbexe/
# Usage: ./buildhb.sh

SCRIPTDIR="$(cd "$(dirname "$0")" && pwd)"
ROOTDIR="$(cd "$SCRIPTDIR/../../.." && pwd)"
export PATH=$PATH:/opt/harbour/bin
mkdir -p "$SCRIPTDIR/hbexe"

PASS=0
FAIL=0

for f in "$SCRIPTDIR"/test*.hb; do
   name=$(basename "$f" .hb)
   hbmk2 "$f" -o"$SCRIPTDIR/hbexe/$name" -i"$ROOTDIR/include" -q 2>&1
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
echo "Executables in: $SCRIPTDIR/hbexe/"
