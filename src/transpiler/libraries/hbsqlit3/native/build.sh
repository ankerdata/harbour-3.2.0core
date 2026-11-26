#!/bin/bash
# Build SQLite3 Multiple Ciphers for HbSqlit3 on macOS and Linux: the
# counterpart of build.bat, from the same amalgamation
# (contrib/3rd/sqlite3/sqlite3.c) with the same flags, so the C# build
# opens EasiPOS's encrypted databases as 9.0 does.
#
# One library per architecture, which HbSqlit3 loads by the process's:
#   <arch>/libsqlite3mc.dylib   macOS (arm64, x64)
#   <arch>/libsqlite3mc.so      Linux (the machine's own architecture)
#
#   build.sh [arm64] [x64]      (default: the machine's architecture)
set -euo pipefail

HERE="$(cd "$(dirname "$0")" && pwd)"
SRC="$HERE/../../../../../contrib/3rd/sqlite3/sqlite3.c"
if [ ! -f "$SRC" ]; then
    echo "ERROR: $SRC not found" >&2
    exit 1
fi

case "$(uname -m)" in
    arm64|aarch64) HOST=arm64 ;;
    x86_64|amd64)  HOST=x64 ;;
    *) echo "ERROR: no build for $(uname -m)" >&2; exit 1 ;;
esac
ARCHS="${*:-$HOST}"

FLAGS="-O2 -w -fPIC
    -DSQLITE_ENABLE_FTS3 -DSQLITE_ENABLE_FTS3_PARENTHESIS
    -DSQLITE_OMIT_DEPRECATED -DSQLITE_ENABLE_COLUMN_METADATA
    -DSQLITE_HAS_CODEC -DSQLITE_TEMP_STORE=2
    -DHAVE_CIPHER_AES_128_CBC=1 -DHAVE_CIPHER_AES_256_CBC=1
    -DHAVE_CIPHER_CHACHA20=1 -DHAVE_CIPHER_SQLCIPHER=1 -DHAVE_CIPHER_RC4=1"

for ARCH in $ARCHS; do
    mkdir -p "$HERE/$ARCH"
    if [ "$(uname -s)" = "Darwin" ]; then
        case "$ARCH" in
            arm64) CLANG_ARCH=arm64 ;;
            x64)   CLANG_ARCH=x86_64 ;;
            *) echo "ERROR: unknown architecture $ARCH" >&2; exit 1 ;;
        esac
        OUT="$HERE/$ARCH/libsqlite3mc.dylib"
        echo "Building $ARCH/libsqlite3mc.dylib ..."
        # shellcheck disable=SC2086
        clang $FLAGS -arch "$CLANG_ARCH" -dynamiclib \
            -install_name @rpath/libsqlite3mc.dylib \
            -o "$OUT" "$SRC" -framework Security
    else
        if [ "$ARCH" != "$HOST" ]; then
            echo "ERROR: on Linux only $HOST is built here" >&2
            exit 1
        fi
        OUT="$HERE/$ARCH/libsqlite3mc.so"
        echo "Building $ARCH/libsqlite3mc.so ..."
        # shellcheck disable=SC2086
        ${CC:-cc} $FLAGS -shared -o "$OUT" "$SRC" -lpthread -ldl -lm
    fi
    echo "  $OUT"
done
