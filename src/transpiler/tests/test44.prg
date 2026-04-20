// Test 44: --defines-map → per-source const class.
//
// Harbour headers don't flow into the transpiler parser (setNoInclude),
// so a bare reference to a header-origin #define would emit as a bare
// identifier and fail with CS0103. scripts/gen-defines.sh (driving
// tools/gendefines.py) harvests literal #defines into per-source C#
// const classes (defines/Test44Const.cs) and writes a defines_map.txt
// sibling that tells the emitter to qualify references as
// `Test44Const.NAME` instead of emitting them bare.
//
// This test asserts:
//   (a) gendefines.py produces defines/Test44Const.cs for test44.ch
//   (b) the transpiler loads --defines-map and qualifies the reference
//   (c) the resulting project compiles and runs with the header values

#include "test44.ch"

PROCEDURE Main()
   ? "limit=" + Str( TEST44_LIMIT, 4 )
   ? "greeting=" + TEST44_GREETING
   ? "offset=" + Str( TEST44_OFFSET, 4 )
RETURN
