// Test 66: ref-invariance shim in both directions and in expression
// context.
//
// C# `ref` is invariant: a `ref dynamic` argument can't bind a typed
// `ref string` parameter, nor can a typed `ref decimal` argument bind a
// `ref dynamic` parameter. The emitter copies each by-ref lvalue through
// a temp of the parameter's own type and copies it back. When the call
// sits inside an `if` condition or a RETURN it must first be hoisted into
// a preceding statement so the temp shuffle has somewhere to live.
//
//   FillBuf : typed by-ref param (cText -> string); caller passes a
//             dynamic local              -> dynamic -> ref string
//   Bump66    : USUAL by-ref param (xVal); caller passes a decimal local
//             -> ref decimal -> ref dynamic
//
// Each is exercised both as a bare statement and inside an if / return.

FUNCTION FillBuf( cText, nLen )
   cText := Replicate( "x", nLen )      // writes cText back -> by-ref
   RETURN Len( cText ) == nLen          // logical result for the if/return

FUNCTION Bump66( xVal )
   xVal := xVal + 1                     // writes xVal back -> by-ref
   RETURN xVal

FUNCTION Grab( nLen )
   LOCAL buf                            // untyped -> dynamic local
   IF FillBuf( @buf, nLen )             // call hoisted out of the if
      RETURN buf
   ENDIF
   RETURN "?"

PROCEDURE Main()
   LOCAL buf                            // untyped -> dynamic
   LOCAL nCount := 10                   // decimal
   LOCAL lOK
   lOK := FillBuf( @buf, 3 )            // statement-context reverse shim
   ? "buf=" + buf + " ok=" + iif( lOK, "Y", "N" )
   ? "grab=" + Grab( 5 )               // if-hoist, returns the buffer
   Bump66( @nCount )                      // statement-context original shim
   ? "count=" + Str( nCount, 3 )
   IF Bump66( @nCount ) > 0               // expression-context original shim
      ? "bumped=" + Str( nCount, 3 )
   ENDIF
RETURN
