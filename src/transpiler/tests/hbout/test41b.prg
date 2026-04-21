#include "astype.ch"
// Test 41 (multi-file pair): see test41a.prg for rationale.
//
// Declares STATIC Helper() / HelperNum() with different bodies so
// the merged partial class Program has two file-private copies of
// each, mangled test41a_Helper / test41b_Helper etc.

FUNCTION HelperCallerB() AS STRING
RETURN "41b: " + Helper() + ", " + Str(HelperNum(), 4)

STATIC FUNCTION Helper() AS STRING
RETURN "from-b"

STATIC FUNCTION HelperNum() AS NUMERIC
RETURN 99
