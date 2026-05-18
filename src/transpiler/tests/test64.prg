// Test 64: a plain variable passed to a by-ref parameter without
// Harbour's `@`.
//
// Once a parameter is reached by-ref anywhere, the C# parameter
// emits `ref` — and C# then requires `ref` at EVERY call site
// (CS1620), where Harbour happily takes a bare argument (by-value
// at that site). The transpiler now emits `ref` for a plain-
// variable argument to a by-ref parameter.
//
// NB: that gives the callee write-back the bare Harbour call did
// not have. For a parameter designed to be passed `@` that is the
// intent — but to keep the .prg / .cs outputs identical this test
// reads Inc()'s RETURN value, not the post-call variable.

PROCEDURE Main()
   LOCAL n := 5

   Inc( @n )                              // @ form marks the param by-ref
   ? "viaref=" + LTrim( Str( n ) )        // 15

   ? "bare=" + LTrim( Str( Inc( n ) ) )   // bare arg — emitted as `ref n`
RETURN

FUNCTION Inc( nVal )
   nVal := nVal + 10
RETURN nVal
