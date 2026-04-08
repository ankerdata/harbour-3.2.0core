#!/bin/bash
# Build all transpiled .cs files with dotnet
# Usage: cd src/transpiler/tests && bash buildcs.sh

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../../.." && pwd)"
TRANSPILER="$REPO_ROOT/bin/hbtranspiler"
CSEXE="$SCRIPT_DIR/csexe"

mkdir -p "$CSEXE"

# Ensure HbRuntime shared library exists
if [ ! -d "$CSEXE/HbRuntime" ]; then
   cd "$CSEXE"
   dotnet new classlib --name HbRuntime --force > /dev/null 2>&1
   rm -f HbRuntime/Class1.cs
fi
cp "$REPO_ROOT/src/transpiler/HbRuntime.cs" "$CSEXE/HbRuntime/HbRuntime.cs"

# Regenerate all .cs files
for f in "$SCRIPT_DIR"/test*.prg; do
   "$TRANSPILER" -I"$REPO_ROOT/include" "$f" -GS -q 2>/dev/null
done

# Build each with dotnet
pass=0; fail=0
for f in "$SCRIPT_DIR"/test*.cs; do
   name=$(basename "$f" .cs)
   cd "$CSEXE"
   dotnet new console --name "$name" --force > /dev/null 2>&1
   cp "$f" "$name/Program.cs"
   rm -f "$name/HbRuntime.cs"
   # Add project reference to shared HbRuntime library
   dotnet add "$name/$name.csproj" reference "$CSEXE/HbRuntime/HbRuntime.csproj" > /dev/null 2>&1
   cd "$name"
   output=$(dotnet build 2>&1)
   if echo "$output" | grep -q "Build succeeded" && ! echo "$output" | grep -q "error CS"; then
      echo "PASS: $name"
      pass=$((pass+1))
   else
      echo "FAIL: $name"
      echo "$output" | grep "error CS" || true
      fail=$((fail+1))
   fi
done

echo ""
echo "Results: $pass passed, $fail failed"
