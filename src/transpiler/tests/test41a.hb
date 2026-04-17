#include "astype.ch"
// Test 41 (multi-file pair): STATIC function file-scope mangling.
//
// Harbour `STATIC FUNCTION` / `STATIC PROCEDURE` is file-private —
// callable only from the declaring .prg. The transpiler emits every
// standalone function into one merged `public static partial class
// Program`, so two files each declaring `STATIC FUNCTION Helper()`
// previously collided (CS0111). The emitter now mangles STATIC
// function names to `<FileBase>_<FuncName>` at both the declaration
// and every intra-file call site. Cross-file callers can't reach
// these (that's the language rule), so no reftab wiring needed.
//
// This file and test41b.prg each declare `STATIC FUNCTION Helper()`
// returning different values. Main() exercises both its own Helper()
// and test41b's public HelperCallerB() (which internally calls its
// own Helper()), so any mangling mistake either fails to compile or
// returns the wrong string.

PROCEDURE Main()
   QOut("41a: " + Helper() + ", " + Str(HelperNum(), 4))
   QOut(HelperCallerB())
RETURN

STATIC FUNCTION Helper() AS STRING
RETURN "from-a"

STATIC FUNCTION HelperNum() AS NUMERIC
RETURN 41
