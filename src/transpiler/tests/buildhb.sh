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
   # test19a/test19b and test20a/test20b are multi-file pairs handled
   # as single test19 / test20 builds below.
   case "$name" in
      test19a|test19b|test20a|test20b|test22a|test22b) continue ;;
   esac
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

# test19: multi-file build from test19a.hb + test19b.hb.
if [ -f "$SCRIPTDIR/test19a.hb" ] && [ -f "$SCRIPTDIR/test19b.hb" ]; then
   cp "$SCRIPTDIR/test19a.hb" "$SCRIPTDIR/hbexe/test19a_hb.prg"
   cp "$SCRIPTDIR/test19b.hb" "$SCRIPTDIR/hbexe/test19b_hb.prg"
   hbmk2 "$SCRIPTDIR/hbexe/test19a_hb.prg" "$SCRIPTDIR/hbexe/test19b_hb.prg" \
         -o"$SCRIPTDIR/hbexe/test19" -i"$ROOTDIR/include" -gtcgi -q 2>&1
   rc=$?
   rm -f "$SCRIPTDIR/hbexe/test19a_hb.prg" "$SCRIPTDIR/hbexe/test19b_hb.prg"
   if [ $rc -eq 0 ]; then
      echo "PASS: test19"
      PASS=$((PASS + 1))
   else
      echo "FAIL: test19"
      FAIL=$((FAIL + 1))
   fi
fi

# test20: multi-file build from test20a.hb + test20b.hb.
if [ -f "$SCRIPTDIR/test20a.hb" ] && [ -f "$SCRIPTDIR/test20b.hb" ]; then
   cp "$SCRIPTDIR/test20a.hb" "$SCRIPTDIR/hbexe/test20a_hb.prg"
   cp "$SCRIPTDIR/test20b.hb" "$SCRIPTDIR/hbexe/test20b_hb.prg"
   hbmk2 "$SCRIPTDIR/hbexe/test20a_hb.prg" "$SCRIPTDIR/hbexe/test20b_hb.prg" \
         -o"$SCRIPTDIR/hbexe/test20" -i"$ROOTDIR/include" -gtcgi -q 2>&1
   rc=$?
   rm -f "$SCRIPTDIR/hbexe/test20a_hb.prg" "$SCRIPTDIR/hbexe/test20b_hb.prg"
   if [ $rc -eq 0 ]; then
      echo "PASS: test20"
      PASS=$((PASS + 1))
   else
      echo "FAIL: test20"
      FAIL=$((FAIL + 1))
   fi
fi

# test22: multi-file build from test22a.hb + test22b.hb.
if [ -f "$SCRIPTDIR/test22a.hb" ] && [ -f "$SCRIPTDIR/test22b.hb" ]; then
   cp "$SCRIPTDIR/test22a.hb" "$SCRIPTDIR/hbexe/test22a_hb.prg"
   cp "$SCRIPTDIR/test22b.hb" "$SCRIPTDIR/hbexe/test22b_hb.prg"
   hbmk2 "$SCRIPTDIR/hbexe/test22a_hb.prg" "$SCRIPTDIR/hbexe/test22b_hb.prg" \
         -o"$SCRIPTDIR/hbexe/test22" -i"$ROOTDIR/include" -gtcgi -q 2>&1
   rc=$?
   rm -f "$SCRIPTDIR/hbexe/test22a_hb.prg" "$SCRIPTDIR/hbexe/test22b_hb.prg"
   if [ $rc -eq 0 ]; then
      echo "PASS: test22"
      PASS=$((PASS + 1))
   else
      echo "FAIL: test22"
      FAIL=$((FAIL + 1))
   fi
fi

echo ""
echo "Results: $PASS compiled, $FAIL failed"
echo "Executables in: $SCRIPTDIR/hbexe/"
