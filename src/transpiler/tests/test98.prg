// Test 98: integral lvalues fed a decimal; an assignment as an operand.
//
// The transpiler types a LOCAL or a file STATIC seeded from an integer
// define as C# long, and an AS INTEGER member is long by declaration.
// A decimal reaching one of those — a NUMERIC parameter, a method's
// return, a quantity — is CS0266. The emitter already coerced a plain
// `:=` into an int local or member; it now also covers a file STATIC
// on the left, the compound `+=` family, and the assignment the
// ref-shim block emits for `n := o:M( @x )` (ten sites in the easipos
// corpus). Where the fraction is real — a division, a quantity — the
// corpus says `Int()` in source so Harbour truncates too, and the
// coercion then only restates it.
// Second: `FOR i := 1 TO ( nLen := Len( a ) )` — an assignment used as
// an operand keeps its parentheses in C#, where `=` binds looser than
// `<=` (CS0131).
#define LAMP_OFF 0
#define LAMP_LOW 1
#include "hbclass.ch"

STATIC snLamp := LAMP_OFF

CLASS Ledger
   VAR nCovers AS INTEGER INIT 0
   METHOD Add( nQty )
   METHOD Gauge( cWhat, cOut )
ENDCLASS

METHOD Add( nQty ) CLASS Ledger
   ::nCovers += Int( nQty )
RETURN ::nCovers

/* NUMERIC return with a by-ref parameter: the caller's assignment is
   emitted inside the shim block. */
METHOD Gauge( cWhat, cOut ) CLASS Ledger
   cOut := "read " + cWhat
RETURN Len( cWhat ) * 10

PROCEDURE SetLamp( nLamp )
   snLamp := nLamp
RETURN

PROCEDURE Main()
   LOCAL oLedger := Ledger():New()
   LOCAL nResult := LAMP_OFF
   LOCAL cOut := ""
   LOCAL aList := { "a", "b", "c" }
   LOCAL cAll := ""
   LOCAL nLen, i

   SetLamp( LAMP_LOW )
   ? "lamp=" + LTrim( Str( snLamp ) )
   oLedger:Add( 2.7 )
   ? "covers=" + LTrim( Str( oLedger:Add( 1 ) ) )
   nResult := oLedger:Gauge( "tea", @cOut )
   ? "result=" + LTrim( Str( nResult ) ) + " " + cOut
   FOR i := 1 TO ( nLen := Len( aList ) )
      cAll += aList[ i ]
   NEXT
   ? "all=" + cAll + " len=" + LTrim( Str( nLen ) )
RETURN
