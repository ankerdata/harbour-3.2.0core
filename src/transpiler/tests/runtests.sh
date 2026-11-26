#!/bin/bash
# Run the transpiler test suite on macOS and Linux: runtests.bat's
# counterpart. Harbour is the one this checkout builds (`make` at its
# root, which puts hbmk2 in bin/<platform>/<compiler>/), unless
# HB_INSTALL names an install prefix with bin/hbmk2.
#
#   runtests.sh            gen + prg + cs + run
#   runtests.sh gen        regenerate csout/ only
#   runtests.sh prg|cs|run a single stage
#
# The suite's C# targets net10.0. Where `dotnet` is a later version with
# no .NET 10 runtime of its own and Homebrew's dotnet@10 is installed,
# that one is used; set DOTNET_ROOT and PATH yourself for any other layout.
set -euo pipefail

HERE="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(cd "$HERE/../../.." && pwd)"

if [ -n "${HB_INSTALL:-}" ]; then
    HBBIN="$HB_INSTALL/bin"
else
    HBBIN="$(dirname "$(ls "$ROOT"/bin/*/*/hbmk2 2>/dev/null | head -1)")"
fi
if [ ! -x "$HBBIN/hbmk2" ]; then
    echo "runtests.sh: hbmk2 not found (run make in $ROOT, or set HB_INSTALL)" >&2
    exit 9
fi
export PATH="$HBBIN:$PATH"

if ! dotnet --list-runtimes 2>/dev/null | grep -q "^Microsoft.NETCore.App 10\."; then
    for D in /opt/homebrew/opt/dotnet@10 /usr/local/opt/dotnet@10; do
        if [ -x "$D/bin/dotnet" ]; then
            export PATH="$D/bin:$PATH" DOTNET_ROOT="$D/libexec"
            break
        fi
    done
fi

exec python3 "$HERE/runsuite.py" "$@"
