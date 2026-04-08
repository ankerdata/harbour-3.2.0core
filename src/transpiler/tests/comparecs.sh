#!/bin/bash
# Compare output of Harbour (.prg) vs C# (.cs) using saved .txt files
# Run runprg.sh and runcs.sh first to generate the .txt files
# Usage: bash compare.sh

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PRG_DIR="$SCRIPT_DIR/prgexe"
CS_DIR="$SCRIPT_DIR/csexe"

pass=0; fail=0
for prg_txt in "$PRG_DIR"/test*.txt; do
   [ -f "$prg_txt" ] || continue
   name=$(basename "$prg_txt" .txt)
   cs_txt="$CS_DIR/${name}.txt"

   if [ ! -f "$cs_txt" ]; then
      echo "SKIP: $name (no C# output)"
      continue
   fi

   if diff -q "$prg_txt" "$cs_txt" > /dev/null 2>&1; then
      echo "MATCH: $name"
      pass=$((pass+1))
   else
      echo "DIFFER: $name"
      diff "$prg_txt" "$cs_txt" | head -6
      fail=$((fail+1))
   fi
done

echo ""
echo "Results: $pass match, $fail differ"
