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
   # test19a/test19b and test20a/test20b are multi-file pairs handled
   # as single test19 / test20 builds below.
   case "$name" in
      test19a|test19b|test20a|test20b|test22a|test22b|test41a|test41b) continue ;;
   esac
   hbmk2 "$f" -o"$SCRIPTDIR/prgexe/$name" -gtcgi -q 2>/dev/null
   if [ $? -eq 0 ]; then
      echo "PASS: $name"
      PASS=$((PASS + 1))
   else
      echo "FAIL: $name"
      FAIL=$((FAIL + 1))
   fi
done

# test19: multi-file build (test19a defines Swap, test19b calls it).
if [ -f "$SCRIPTDIR/test19a.prg" ] && [ -f "$SCRIPTDIR/test19b.prg" ]; then
   hbmk2 "$SCRIPTDIR/test19a.prg" "$SCRIPTDIR/test19b.prg" \
         -o"$SCRIPTDIR/prgexe/test19" -gtcgi -q 2>/dev/null
   if [ $? -eq 0 ]; then
      echo "PASS: test19"
      PASS=$((PASS + 1))
   else
      echo "FAIL: test19"
      FAIL=$((FAIL + 1))
   fi
fi

# test20: multi-file build (test20a defines DoubleIt, test20b uses it).
if [ -f "$SCRIPTDIR/test20a.prg" ] && [ -f "$SCRIPTDIR/test20b.prg" ]; then
   hbmk2 "$SCRIPTDIR/test20a.prg" "$SCRIPTDIR/test20b.prg" \
         -o"$SCRIPTDIR/prgexe/test20" -gtcgi -q 2>/dev/null
   if [ $? -eq 0 ]; then
      echo "PASS: test20"
      PASS=$((PASS + 1))
   else
      echo "FAIL: test20"
      FAIL=$((FAIL + 1))
   fi
fi

# test22: multi-file build (two classes with the same method name in
# separate files because the Harbour parser doesn't allow same-named
# methods in a single compilation unit).
if [ -f "$SCRIPTDIR/test22a.prg" ] && [ -f "$SCRIPTDIR/test22b.prg" ]; then
   hbmk2 "$SCRIPTDIR/test22a.prg" "$SCRIPTDIR/test22b.prg" \
         -o"$SCRIPTDIR/prgexe/test22" -gtcgi -q 2>/dev/null
   if [ $? -eq 0 ]; then
      echo "PASS: test22"
      PASS=$((PASS + 1))
   else
      echo "FAIL: test22"
      FAIL=$((FAIL + 1))
   fi
fi

# test41: multi-file build, two files each declaring `STATIC FUNCTION
# Helper()` / `HelperNum()` with different bodies. Proves the C#
# emitter's file-scope mangling of STATIC functions.
if [ -f "$SCRIPTDIR/test41a.prg" ] && [ -f "$SCRIPTDIR/test41b.prg" ]; then
   hbmk2 "$SCRIPTDIR/test41a.prg" "$SCRIPTDIR/test41b.prg" \
         -o"$SCRIPTDIR/prgexe/test41" -gtcgi -q 2>/dev/null
   if [ $? -eq 0 ]; then
      echo "PASS: test41"
      PASS=$((PASS + 1))
   else
      echo "FAIL: test41"
      FAIL=$((FAIL + 1))
   fi
fi

echo ""
echo "Results: $PASS compiled, $FAIL failed"
echo "Executables in: $SCRIPTDIR/prgexe/"
