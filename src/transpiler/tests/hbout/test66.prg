#include "astype.ch"
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

FUNCTION FillBuf( /*@*/cText AS STRING, nLen AS NUMERIC ) AS LOGICAL
   // writes cText back -> by-ref
   cText := Replicate("x", nLen)
   // logical result for the if/return
RETURN Len(cText) == nLen

FUNCTION Bump66( /*@*/xVal AS USUAL ) AS USUAL
   // writes xVal back -> by-ref
   xVal := xVal + 1
RETURN xVal

FUNCTION Grab( nLen AS NUMERIC ) AS USUAL
   // untyped -> dynamic local
   LOCAL buf AS USUAL
   // call hoisted out of the if
   IF FillBuf(@buf, nLen)
   RETURN buf
   ENDIF

RETURN "?"

PROCEDURE Main()
   // untyped -> dynamic
   LOCAL buf AS USUAL
   // decimal
   LOCAL nCount := 10 AS NUMERIC
   LOCAL lOK AS LOGICAL
   // statement-context reverse shim
   lOK := FillBuf(@buf, 3)
   QOut("buf=" + buf + " ok=" + IIF(lOK, "Y", "N"))
   // if-hoist, returns the buffer
   QOut("grab=" + Grab(5))
   // statement-context original shim
   Bump66(@nCount)
   QOut("count=" + Str(nCount, 3))
   // expression-context original shim
   IF Bump66(@nCount) > 0
      QOut("bumped=" + Str(nCount, 3))
   ENDIF

RETURN
