// Test 82: numeric #define type tiering.
//
// A numeric #define is typed by its literal value: ANY integer ->
// `long` (the single integral tier — Harbour integers are 64-bit, no
// Int32 sub-tier), a fractional literal -> `decimal`. gendefines emits
// the const at that type AND records it in defines_map.txt; the
// transpiler (hbtypes.c) reads the map to type a reference the same
// way (both integral tokens infer as INTEGER, which emits C# long).
//
// Guarded two ways at once. Mistyping is a COMPILE error: a fractional
// declared integral will not convert (CS0266); BIGVAL exceeds Int32 so
// any regression back to an int tier overflows the literal (CS0031).
// And the RUNTIME values below must still match the Harbour (.prg)
// run, which catches wrong-but-compilable typing.

#define SMALLFLAG   7            // integral -> long
#define BIGVAL      4294967296   // 2^32 — pins that the tier is Int64
#define RATE        1.5          // fractional -> decimal

PROCEDURE Main()
   ? "small=" + LTrim( Str( SMALLFLAG ) )          // 7
   ? "big="   + LTrim( Str( BIGVAL + 1 ) )         // 4294967297 (long)
   ? "rate="  + LTrim( Str( Int( RATE * 2 ) ) )    // 3  (decimal 1.5*2)
RETURN
