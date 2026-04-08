#!/bin/bash
# Run all compiled C# projects from csexe/, save output to csexe/*.txt
# Usage: bash runcs.sh

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
EXE_DIR="$SCRIPT_DIR/csexe"

pass=0; fail=0
for d in "$EXE_DIR"/test*; do
   [ -d "$d" ] || continue
   name=$(basename "$d")
   (cd "$d" && dotnet run --no-build) > "$EXE_DIR/${name}.txt" 2>&1
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
