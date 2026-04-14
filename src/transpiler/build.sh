#!/bin/bash
# Build the Harbour transpiler
# Usage: bash build.sh [install]

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
OUTPUT="$REPO_ROOT/bin/hbtranspiler"
INSTALL_DIR="/opt/harbour/bin"

mkdir -p "$(dirname "$OUTPUT")"

cd "$REPO_ROOT"

clang -I./include -Isrc/transpiler -DHB_TRANSPILER -fno-common -w -O3 \
  -o "$OUTPUT" \
  src/transpiler/cmdcheck.c src/transpiler/complex.c \
  src/transpiler/expropta.c src/transpiler/genhb.c \
  src/transpiler/gencsharp.c src/transpiler/genscan.c src/transpiler/genstubs.c \
  src/transpiler/hbast.c src/transpiler/hbclsparse.c \
  src/transpiler/hbcomp.c src/transpiler/hbmain.c \
  src/transpiler/hbtypes.c src/transpiler/hbreftab.c \
  src/transpiler/hbfunctab.c src/transpiler/ppcomp.c \
  src/transpiler/pcodestubs.c src/transpiler/harboury.c \
  src/common/expropt2.c \
  src/compiler/compi18n.c src/compiler/exproptb.c \
  src/compiler/hbdbginf.c \
  src/compiler/hbfunchk.c \
  src/compiler/hbgenerr.c src/compiler/hbident.c \
  src/compiler/hbusage.c \
  src/main/harbour.c src/pp/ppcore.c \
  -L./lib/darwin/clang -lhbnortl -lhbcommon -lm

if [ $? -ne 0 ]; then
   echo "Build FAILED"
   exit 1
fi

echo "Build successful: $OUTPUT"
"$OUTPUT" -v 2>&1 | head -2

if [ "$1" = "install" ]; then
   echo ""
   echo "Installing to $INSTALL_DIR..."
   sudo cp "$OUTPUT" "$INSTALL_DIR/hbtranspiler"
   echo "Installed: $INSTALL_DIR/hbtranspiler"
fi
