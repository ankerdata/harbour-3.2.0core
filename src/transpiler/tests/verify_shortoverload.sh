#!/bin/bash
# Verify the C# emitter's short-overload gating and typing.
#
# Short overloads let callers pass fewer args than a declaration with a
# ref param takes (C# `ref` has no default). The emitter should:
#
#   1. Emit the overload when at least one caller uses a shorter arity.
#      test45 does; its emitted .cs must contain a second LoadFlags.
#   2. Skip it when every caller passes the full arity.
#      test46 does; its emitted .cs must have exactly one SaveFlags.
#   3. Type the pre-ref parameters from the canonical's per-slot type
#      (Hungarian inference for LoadFlags: cFile → string) rather than
#      collapsing them to `dynamic`. The old `dynamic cFile = null`
#      emission would regress here.
#
# Run after buildcs.sh's pass-2 -GS has regenerated the .cs files.
# Exits non-zero on the first failed assertion and prints a diff-like
# summary so the regression is obvious in CI logs.
set -u

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
CSOUT="$SCRIPT_DIR/csout"
CS45="$CSOUT/test45a.cs"
CS46="$CSOUT/test46a.cs"

fail=0

fail_msg() {
   echo "FAIL: $1" >&2
   fail=$((fail + 1))
}

if [ ! -f "$CS45" ]; then
   fail_msg "test45a.cs missing — run buildcs.sh first"
elif [ ! -f "$CS46" ]; then
   fail_msg "test46a.cs missing — run buildcs.sh first"
else
   n_load=$(grep -c 'public static void LoadFlags' "$CS45" || true)
   if [ "$n_load" -ne 2 ]; then
      fail_msg "test45a.cs: expected 2 LoadFlags signatures (canonical + short), got $n_load"
      grep -n 'public static void LoadFlags' "$CS45" >&2 || true
   fi

   if ! grep -q 'public static void LoadFlags(string cFile = default)' "$CS45"; then
      fail_msg "test45a.cs: short overload not typed 'string cFile = default'"
      grep -n 'public static void LoadFlags' "$CS45" >&2 || true
   fi

   if grep -q 'public static void LoadFlags(dynamic cFile = null)' "$CS45"; then
      fail_msg "test45a.cs: short overload regressed to 'dynamic cFile = null'"
   fi

   n_save=$(grep -c 'public static void SaveFlags' "$CS46" || true)
   if [ "$n_save" -ne 1 ]; then
      fail_msg "test46a.cs: expected 1 SaveFlags signature (no short overload), got $n_save"
      grep -n 'public static void SaveFlags' "$CS46" >&2 || true
   fi
fi

if [ "$fail" -eq 0 ]; then
   echo "PASS: short-overload gating + typing"
   exit 0
fi
echo "short-overload verification: $fail check(s) failed" >&2
exit 1
