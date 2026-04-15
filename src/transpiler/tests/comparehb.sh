#!/bin/bash
# Compare output of Harbour (.prg) vs Harbour (.hb) using saved .txt files
# Run runprg.sh and runhb.sh first to generate the .txt files
# Usage: bash comparehb.sh

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PRG_DIR="$SCRIPT_DIR/prgexe"
HB_DIR="$SCRIPT_DIR/hbexe"

pass=0; fail=0
for prg_txt in "$PRG_DIR"/test*.txt; do
   [ -f "$prg_txt" ] || continue
   name=$(basename "$prg_txt" .txt)
   hb_txt="$HB_DIR/${name}.txt"

   if [ ! -f "$hb_txt" ]; then
      echo "SKIP: $name (no .hb output)"
      continue
   fi

   if diff -q "$prg_txt" "$hb_txt" > /dev/null 2>&1; then
      echo "MATCH: $name"
      pass=$((pass+1))
   else
      echo "DIFFER: $name"
      diff "$prg_txt" "$hb_txt" | head -6
      fail=$((fail+1))
   fi
done

echo ""
echo "Results: $pass match, $fail differ"
[ $fail -eq 0 ]
