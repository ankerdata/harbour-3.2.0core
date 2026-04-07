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
   # hbmk2 treats .hb as scripts — copy to .prg for compilation
   cp "$f" "$SCRIPTDIR/hbexe/${name}_hb.prg"
   hbmk2 "$SCRIPTDIR/hbexe/${name}_hb.prg" -o"$SCRIPTDIR/hbexe/$name" -i"$ROOTDIR/include" -gtcgi -q 2>&1
   rm -f "$SCRIPTDIR/hbexe/${name}_hb.prg"
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
