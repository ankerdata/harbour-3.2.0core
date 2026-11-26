#!/bin/bash
# Build the Harbour transpiler
# Usage: bash build.sh [install]

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
OUTPUT="$REPO_ROOT/bin/hbtranspiler"
INSTALL_DIR="/opt/harbour/bin"

mkdir -p "$(dirname "$OUTPUT")"

cd "$REPO_ROOT"

# hbcommon and hbnortl, wherever this system's Harbour build put them
# (lib/darwin/clang, lib/linux/gcc ...): `HB_BUILD_PARTS=compiler make`
# at the root builds them. HB_LIBDIR names another place.
if [ -z "$HB_LIBDIR" ]; then
   HB_LIBDIR="$(dirname "$(ls lib/*/*/libhbnortl.a 2>/dev/null | head -1)")"
fi
if [ ! -f "$HB_LIBDIR/libhbnortl.a" ] || [ ! -f "$HB_LIBDIR/libhbcommon.a" ]; then
   echo "Build FAILED: libhbnortl.a and libhbcommon.a not found (run: HB_BUILD_PARTS=compiler make)"
   exit 1
fi

# clang on macOS, as Harbour's own build there; the system's compiler elsewhere
if [ -z "$CC" ]; then
   if [ "$(uname -s)" = "Darwin" ]; then CC=clang; else CC=cc; fi
fi

$CC -Isrc/transpiler/include -I./include -Isrc/transpiler -DHB_TRANSPILER -fno-common -w -O3 \
  -o "$OUTPUT" \
  src/transpiler/cmdcheck.c src/transpiler/complex.c \
  src/transpiler/expropta.c \
  src/transpiler/gencsharp.c src/transpiler/genscan.c src/transpiler/genstubs.c \
  src/transpiler/hbast.c src/transpiler/hbclsparse.c \
  src/transpiler/hbcomp.c src/transpiler/hbmain.c \
  src/transpiler/hbtypes.c src/transpiler/hbreftab.c \
  src/transpiler/hbdefinemap.c src/transpiler/hbfieldtypes.c \
  src/transpiler/hbhbxcanon.c \
  src/transpiler/hbfilecase.c \
  src/transpiler/hbfunctab.c src/transpiler/hbvartypes.c \
  src/transpiler/ppcomp.c \
  src/transpiler/pcodestubs.c src/transpiler/harboury.c \
  src/transpiler/src/common/expropt2.c \
  src/compiler/compi18n.c src/compiler/exproptb.c \
  src/compiler/hbdbginf.c \
  src/compiler/hbfunchk.c \
  src/compiler/hbgenerr.c src/compiler/hbident.c \
  src/transpiler/src/compiler/hbusage.c \
  src/main/harbour.c src/transpiler/src/pp/ppcore.c \
  -L"$HB_LIBDIR" -lhbnortl -lhbcommon -lm

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
