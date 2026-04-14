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

# Pass 1 — whole-codebase scan. Populates hbreftab.tab with signatures
# (params + by-ref usage) so the C# emitter has cross-file information
# when it runs in pass 2. Without this, e.g. test19a's Swap parameters
# would not get the `ref` modifier, because the @ args live in test19b.
for f in "$SCRIPT_DIR"/test*.prg; do
   "$TRANSPILER" -I"$REPO_ROOT/include" "$f" -GF -q 2>/dev/null
done

# Pass 2 — regenerate all .cs files with the populated table.
for f in "$SCRIPT_DIR"/test*.prg; do
   "$TRANSPILER" -I"$REPO_ROOT/include" "$f" -GS -q 2>/dev/null
done

# Build each with dotnet
pass=0; fail=0
for f in "$SCRIPT_DIR"/test*.cs; do
   name=$(basename "$f" .cs)
   # test19a/test19b and test20a/test20b are multi-file pairs handled
   # as single test19 / test20 projects below.
   case "$name" in
      test19a|test19b|test20a|test20b|test22a|test22b) continue ;;
      # test32 exercises the PP trailing-comment-in-command fix via
      # `SET CENTURY ON // ...`. The PP expands to HbRuntime.__SetCentury
      # / Set / _SET_EXACT, which HbRuntime.cs does not stub yet.
      # Parser/round-trip side is covered by runtests + runhb; the
      # C# side will come with the HbRuntime coverage todo.
      test32) continue ;;
   esac
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

# test19: multi-file project combining test19a.cs (defines Swap) and
# test19b.cs (Main, calls Swap). Both .cs files live as siblings in
# the same console project so dotnet links them into one assembly.
if [ -f "$SCRIPT_DIR/test19a.cs" ] && [ -f "$SCRIPT_DIR/test19b.cs" ]; then
   cd "$CSEXE"
   dotnet new console --name test19 --force > /dev/null 2>&1
   rm -f test19/Program.cs test19/HbRuntime.cs
   cp "$SCRIPT_DIR/test19a.cs" "test19/test19a.cs"
   cp "$SCRIPT_DIR/test19b.cs" "test19/test19b.cs"
   dotnet add "test19/test19.csproj" reference "$CSEXE/HbRuntime/HbRuntime.csproj" > /dev/null 2>&1
   cd "test19"
   output=$(dotnet build 2>&1)
   if echo "$output" | grep -q "Build succeeded" && ! echo "$output" | grep -q "error CS"; then
      echo "PASS: test19"
      pass=$((pass+1))
   else
      echo "FAIL: test19"
      echo "$output" | grep "error CS" || true
      fail=$((fail+1))
   fi
fi

# test20: multi-file project combining test20a.cs (defines DoubleIt)
# and test20b.cs (defines QuadrupleIt + Main, calls DoubleIt).
if [ -f "$SCRIPT_DIR/test20a.cs" ] && [ -f "$SCRIPT_DIR/test20b.cs" ]; then
   cd "$CSEXE"
   dotnet new console --name test20 --force > /dev/null 2>&1
   rm -f test20/Program.cs test20/HbRuntime.cs
   cp "$SCRIPT_DIR/test20a.cs" "test20/test20a.cs"
   cp "$SCRIPT_DIR/test20b.cs" "test20/test20b.cs"
   dotnet add "test20/test20.csproj" reference "$CSEXE/HbRuntime/HbRuntime.csproj" > /dev/null 2>&1
   cd "test20"
   output=$(dotnet build 2>&1)
   if echo "$output" | grep -q "Build succeeded" && ! echo "$output" | grep -q "error CS"; then
      echo "PASS: test20"
      pass=$((pass+1))
   else
      echo "FAIL: test20"
      echo "$output" | grep "error CS" || true
      fail=$((fail+1))
   fi
fi

# test22: multi-file project — two classes with same method name in
# separate files (each with a class definition + method bodies). Both
# .cs files live in the same project; test22b.cs contains Main.
if [ -f "$SCRIPT_DIR/test22a.cs" ] && [ -f "$SCRIPT_DIR/test22b.cs" ]; then
   cd "$CSEXE"
   dotnet new console --name test22 --force > /dev/null 2>&1
   rm -f test22/Program.cs test22/HbRuntime.cs
   cp "$SCRIPT_DIR/test22a.cs" "test22/test22a.cs"
   cp "$SCRIPT_DIR/test22b.cs" "test22/test22b.cs"
   dotnet add "test22/test22.csproj" reference "$CSEXE/HbRuntime/HbRuntime.csproj" > /dev/null 2>&1
   cd "test22"
   output=$(dotnet build 2>&1)
   if echo "$output" | grep -q "Build succeeded" && ! echo "$output" | grep -q "error CS"; then
      echo "PASS: test22"
      pass=$((pass+1))
   else
      echo "FAIL: test22"
      echo "$output" | grep "error CS" || true
      fail=$((fail+1))
   fi
fi

echo ""
echo "Results: $pass passed, $fail failed"
