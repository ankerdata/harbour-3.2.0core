#!/bin/bash
# Compile all transpiled Harbour outputs in hbout/ to executables in
# hbexe/. runtests.sh writes the transpiled .prg files into hbout/ via
# -GT; they share the .prg extension with original sources but live in
# a separate directory so the two don't collide.
# Usage: ./buildhb.sh

SCRIPTDIR="$(cd "$(dirname "$0")" && pwd)"
ROOTDIR="$(cd "$SCRIPTDIR/../../.." && pwd)"
HBOUT="$SCRIPTDIR/hbout"
export PATH=$PATH:/opt/harbour/bin
mkdir -p "$SCRIPTDIR/hbexe"

PASS=0
FAIL=0

for f in "$HBOUT"/test*.prg; do
   [ -f "$f" ] || continue
   name=$(basename "$f" .prg)
   # test19a/test19b and test20a/test20b are multi-file pairs handled
   # as single test19 / test20 builds below.
   case "$name" in
      test19a|test19b|test20a|test20b|test22a|test22b|test41a|test41b|test45a|test45b|test46a|test46b) continue ;;
   esac
   hbmk2 "$f" -o"$SCRIPTDIR/hbexe/$name" -i"$ROOTDIR/include" -i"$SCRIPTDIR" -gtcgi -q 2>&1
   if [ $? -eq 0 ]; then
      echo "PASS: $name"
      PASS=$((PASS + 1))
   else
      echo "FAIL: $name"
      FAIL=$((FAIL + 1))
   fi
done

# Multi-file pair builds — hbmk2 accepts multiple .prg inputs
# directly; no copy-rename hop required now that the transpiler emits
# .prg into hbout/.
for pair in 19 20 22 41 45 46; do
   a="$HBOUT/test${pair}a.prg"
   b="$HBOUT/test${pair}b.prg"
   if [ -f "$a" ] && [ -f "$b" ]; then
      hbmk2 "$a" "$b" -o"$SCRIPTDIR/hbexe/test${pair}" \
            -i"$ROOTDIR/include" -gtcgi -q 2>&1
      if [ $? -eq 0 ]; then
         echo "PASS: test${pair}"
         PASS=$((PASS + 1))
      else
         echo "FAIL: test${pair}"
         FAIL=$((FAIL + 1))
      fi
   fi
done

echo ""
echo "Results: $PASS compiled, $FAIL failed"
echo "Executables in: $SCRIPTDIR/hbexe/"
