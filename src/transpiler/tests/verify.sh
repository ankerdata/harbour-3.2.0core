#!/bin/bash
# End-to-end verification: transpile, build, run, and compare outputs
# for every testNN.prg in this directory.
#
# The individual buildcs / buildhb / buildprg scripts only check that
# a target compiles; runcs / runhb / runprg only check that it exits
# cleanly; comparecs / comparehb check that the recorded stdout from
# each target matches the canonical .prg output. None of the earlier
# scripts alone catches a runtime crash whose stderr goes to the void
# (Harbour BASE/1003 on an undeclared "memvar", null-ref in C#, etc.)
# — only the compare step does.
#
# Run this after any change that could affect codegen or runtime
# behaviour. CI-style single entry point.

set -u
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
cd "$SCRIPT_DIR"

fail=0
run_step() {
   local label="$1"; shift
   echo "=== $label ==="
   if ! bash "$@"; then
      echo "FAIL: $label exited non-zero"
      fail=1
   fi
}

# Build: one target per source.
run_step "buildprg"  buildprg.sh
run_step "buildhb"   buildhb.sh
run_step "buildcs"   buildcs.sh

# Run: exercise each built target, capture stdout.
run_step "runprg"    runprg.sh
run_step "runhb"     runhb.sh
run_step "runcs"     runcs.sh

# Compare: .hb and .cs stdout must match the .prg baseline. This is
# the step that actually catches silent runtime divergences.
echo "=== comparehb ==="
bash comparehb.sh
hb_rc=$?
echo "=== comparecs ==="
bash comparecs.sh
cs_rc=$?

# Negative tests: every .prg under errors/ must FAIL -GS codegen with
# the expected transpiler error. Covers unsupported constructs (e.g.
# workarea ALIAS) that must surface rather than silently transpile.
echo "=== errors ==="
bash errors/run.sh
err_rc=$?

if [ $hb_rc -ne 0 ] || [ $cs_rc -ne 0 ] || [ $err_rc -ne 0 ]; then
   fail=1
fi

if [ $fail -ne 0 ]; then
   echo
   echo "VERIFY: one or more stages failed"
   exit 1
fi

echo
echo "VERIFY: all stages passed"
