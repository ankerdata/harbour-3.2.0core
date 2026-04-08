#!/bin/bash
# Run all compiled .prg executables from prgexe/, save output to prgexe/*.txt
# Usage: bash runprg.sh

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
EXE_DIR="$SCRIPT_DIR/prgexe"

pass=0; fail=0
for f in "$EXE_DIR"/test*; do
   [ -x "$f" ] || continue
   [[ "$f" == *.txt ]] && continue
   name=$(basename "$f")
   "$f" > "$EXE_DIR/${name}.txt" 2>&1
   rc=$?
   if [ $rc -eq 0 ]; then
      echo "PASS: $name"
      pass=$((pass+1))
   else
      echo "FAIL: $name (exit $rc)"
      tail -3 "$EXE_DIR/${name}.txt"
      fail=$((fail+1))
   fi
done

echo ""
echo "Results: $pass passed, $fail failed"
