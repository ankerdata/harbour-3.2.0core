// Test 56: a function whose FIRST parameter is by-reference, called
// both bare (for its return value) and with `@arg`.
//
// The Harbour idiom `FUNCTION GetQty(lDecimalQty)` where the param
// is treated by-ref — most callers want only the return value and
// call `GetQty()`, a few pass `@lDecimalQty` to receive the side
// output. In C# a `ref` param is mandatory, so the emit needs a
// parameterless short overload forwarding to the canonical
// `GetQty(ref bool)`.
//
// The short-overload machinery was gated `iFirstRef > 0`, which
// excluded first-param-ref functions like this one — bare calls
// hit CS7036. Relaxing the gate to `iFirstRef >= 0` emits the
// parameterless overload. This test pins that path.

PROCEDURE Main()
   LOCAL lDec := .F.

   ? "bare="    + LTrim( Str( GetQty() ) )
   ? "ref_ret=" + LTrim( Str( GetQty( @lDec ) ) )
   ? "ref_out=" + IIF( lDec, "Y", "N" )
RETURN

/* First (and only) param is by-ref: the `@lDec` call site above
   marks slot 0 as ref in the reftab. */
FUNCTION GetQty( lDecimalQty )
   lDecimalQty := .T.
RETURN 42
