// Test 82: numeric #define type tiering.
//
// A numeric #define is typed by its literal value: an integer that fits
// Int32 -> `int`, an integer beyond Int32 -> `long` (bit-flag masks /
// large ids), a fractional literal -> `decimal`. gendefines emits the
// const at that type AND records it in defines_map.txt; the transpiler
// (hbtypes.c) reads the map to type a reference the same way.
//
// This is guarded two ways at once. Mistyping is a COMPILE error: a
// >Int32 value declared `int` overflows the literal (CS0031); a
// fractional declared `int` will not convert (CS0266) — so if the long
// or decimal tier regressed, buildcs.sh would fail here. And the
// RUNTIME values below must still match the Harbour (.prg) run, which
// catches a wrong-but-compilable typing.

#define SMALLFLAG   7            // fits Int32  -> int
#define BIGVAL      4294967296   // 2^32, > Int32 -> long
#define RATE        1.5          // fractional  -> decimal

PROCEDURE Main()
   ? "small=" + LTrim( Str( SMALLFLAG ) )          // 7
   ? "big="   + LTrim( Str( BIGVAL + 1 ) )         // 4294967297 (long)
   ? "rate="  + LTrim( Str( Int( RATE * 2 ) ) )    // 3  (decimal 1.5*2)
RETURN
