#!/bin/bash
# Build all transpiled .cs files with dotnet, in parallel.
# Usage: cd src/transpiler/tests && bash buildcs.sh [JOBS]
# Per-test dotnet invocations run concurrently (default: $(sysctl cpu count) or 4).

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../../.." && pwd)"
TRANSPILER="$REPO_ROOT/bin/hbtranspiler"
CSEXE="$SCRIPT_DIR/csexe"

JOBS="${1:-$(getconf _NPROCESSORS_ONLN 2>/dev/null || echo 4)}"
RESULTS_DIR="$CSEXE/.results"
rm -rf "$RESULTS_DIR"
mkdir -p "$CSEXE" "$RESULTS_DIR"

# Ensure HbRuntime shared library exists
if [ ! -d "$CSEXE/HbRuntime" ]; then
   cd "$CSEXE"
   dotnet new classlib --name HbRuntime --force > /dev/null 2>&1
   rm -f HbRuntime/Class1.cs
   cd "$SCRIPT_DIR"
fi
cp "$REPO_ROOT/src/transpiler/HbRuntime.cs" "$CSEXE/HbRuntime/HbRuntime.cs"

# Pre-build HbRuntime sequentially. Test csprojs reference it via
# ProjectReference, and `dotnet build` with parallel workers will
# otherwise race to write HbRuntime.dll — manifesting as random
# CS2012 "file in use" failures scattered across tests. Building
# once here, then passing `--no-dependencies` to the per-test build
# below, serialises the shared dependency safely.
(cd "$CSEXE/HbRuntime" && dotnet build > /dev/null 2>&1)

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

# Build one test case. First arg is the dest project name; remaining
# args are the .cs source files (one for single-file tests, multiple
# for pair tests like test19). Writes PASS/FAIL + error lines to
# $RESULTS_DIR/$name, which the main process aggregates at the end.
# Factored out so the per-test work can run in parallel — each call
# is independent (own project dir, own dotnet workspace).
build_one() {
   local name="$1"; shift
   local projdir="$CSEXE/$name"
   local result="$RESULTS_DIR/$name"

   mkdir -p "$projdir"
   # Hand-written csproj: avoids `dotnet new` + `dotnet add reference`,
   # each of which takes ~0.5s per test. Keeps the per-test call list
   # down to a single `dotnet build` — the only thing that actually
   # compiles anything.
   cat > "$projdir/$name.csproj" <<EOF
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <RootNamespace>$name</RootNamespace>
    <AssemblyName>$name</AssemblyName>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="../HbRuntime/HbRuntime.csproj" />
  </ItemGroup>
</Project>
EOF
   # Clean prior sources (otherwise leftover .cs from a previous run
   # with different inputs would still compile).
   find "$projdir" -maxdepth 1 -name '*.cs' -delete
   local src
   for src in "$@"; do
      cp "$src" "$projdir/$(basename "$src")"
   done

   local out
   # --no-dependencies: skip the HbRuntime rebuild that's already
   # been done once, up front, in the main process.
   out=$(cd "$projdir" && dotnet build --no-dependencies 2>&1)
   if echo "$out" | grep -q "Build succeeded" && \
      ! echo "$out" | grep -q "error CS"; then
      echo "PASS" > "$result"
   else
      { echo "FAIL"; echo "$out" | grep "error CS"; } > "$result"
   fi
}

# Pure-bash job throttling: fire build_one as a background job for
# each test, waiting when the running count hits $JOBS. Avoids
# xargs quoting pain when paths have tabs/spaces or the line gets long.
running=0
schedule() {
   if [ "$running" -ge "$JOBS" ]; then
      wait -n 2>/dev/null || wait
      running=$((running - 1))
   fi
   build_one "$@" &
   running=$((running + 1))
}

# Single-file tests — skip multi-file pair members (handled below).
for f in "$SCRIPT_DIR"/test*.cs; do
   name=$(basename "$f" .cs)
   case "$name" in
      test19a|test19b|test20a|test20b|test22a|test22b|test41a|test41b) continue ;;
   esac
   schedule "$name" "$f"
done

# Multi-file pair tests: both .cs files in one project named without
# the a/b suffix (test19a + test19b → test19).
for pair in 19 20 22 41; do
   a="$SCRIPT_DIR/test${pair}a.cs"
   b="$SCRIPT_DIR/test${pair}b.cs"
   [ -f "$a" ] && [ -f "$b" ] && schedule "test${pair}" "$a" "$b"
done

wait

# Aggregate: one file per test, first line is PASS or FAIL. Keep the
# counting loop out of a pipeline so `pass`/`fail` survive to the
# final report (subshells would otherwise lose them).
pass=0; fail=0
lines=""
for f in "$RESULTS_DIR"/*; do
   [ -f "$f" ] || continue
   name=$(basename "$f")
   if head -1 "$f" | grep -q PASS; then
      lines+="PASS: $name
"
      pass=$((pass+1))
   else
      lines+="FAIL: $name
$(tail -n +2 "$f")
"
      fail=$((fail+1))
   fi
done
printf '%s' "$lines" | sort

echo ""
echo "Results: $pass passed, $fail failed"
[ $fail -eq 0 ]
