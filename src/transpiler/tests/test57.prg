// Test 57: ref-shim for a typed lvalue passed by-ref to a USUAL
// (`ref dynamic`) parameter.
//
// Harbour's polymorphic by-ref idiom: a function takes `@xVal` and
// fills it with a number, a string, or a logical depending on
// context. The `x`-prefix keeps the parameter USUAL, so the C#
// emit is `ref dynamic xVal`.
//
// C# `ref` is invariant — a caller's `ref decimal` / `ref string`
// lvalue cannot bind to `ref dynamic`. For a call STATEMENT the
// transpiler wraps the call in a brace block that copies each such
// lvalue through a `dynamic` temp and back:
//
//     {
//         dynamic _hbref1 = nGot;
//         FetchValue(1, ref _hbref1);
//         nGot = _hbref1;
//     }
//
// Covered here: a plain variable lvalue (@nGot / @cGot), a class
// DATA field lvalue (@oH:nField, the HB_ET_REFERENCE arg form), and
// the `var := Foo(@x)` assignment form. Expression-context calls
// (e.g. `IF Foo(@x)`) are out of scope — they need temp hoisting.

#include "hbclass.ch"

CLASS Holder
   DATA nField INIT 0
   METHOD New()
ENDCLASS

METHOD New() CLASS Holder
RETURN Self

PROCEDURE Main()
   LOCAL nGot := 0
   LOCAL cGot := ""
   LOCAL lOk
   LOCAL oH := Holder():New()

   // bare call statement — typed numeric variable by-ref
   FetchValue( 1, @nGot )
   ? "num=" + LTrim( Str( nGot ) )

   // bare call statement — typed string variable by-ref
   FetchValue( 2, @cGot )
   ? "str=" + RTrim( cGot )

   // bare call statement — typed class DATA field by-ref
   FetchValue( 1, @oH:nField )
   ? "field=" + LTrim( Str( oH:nField ) )

   // `var := Foo(@x)` form — the assignment-case shim
   lOk := FetchValue( 1, @nGot )
   ? "into=" + LTrim( Str( nGot ) ) + " ok=" + IIF( lOk, "Y", "N" )
RETURN

/* `xVal` is x-prefixed -> USUAL -> emits `ref dynamic`. It receives
   both a number and a string across call sites, so it is genuinely
   polymorphic and stays USUAL. */
FUNCTION FetchValue( nKind, xVal )
   IF nKind == 1
      xVal := 42
   ELSE
      xVal := "hello"
   ENDIF
RETURN .T.
