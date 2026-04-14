// Test 28: non-constant STATIC initializers.
//
// Standard Harbour rejects STATIC initializers that aren't compile-time
// constants (E0009 "Illegal variable initializer"). Production Harbour
// code routinely uses non-constant inits like
//     STATIC scCRLF := Chr(13) + Chr(10)
// which work in vanilla mode because the expression is pre-reduced to
// a string literal before the check runs.
//
// The transpiler preserves expressions verbatim so they can round-trip
// into .hb and into C# field initializers, so the check sees them
// unreduced. C# static field initializers accept any expression (only
// `const` requires a literal), so the check is skipped under
// HB_TRANSPILER in include/hbexpra.c.
//
// Hit in easiposx: syspro.prg (scCRLF), fmse.prg (array literal with
// Chr()+string), and 7 others.

PROCEDURE Main()
   STATIC scCRLF := Chr(13) + Chr(10)
   STATIC snBase := 10 * 2 + 2
   ? "crlf:  " + LTrim(Str(Len(scCRLF)))
   ? "base:  " + LTrim(Str(snBase))
RETURN
