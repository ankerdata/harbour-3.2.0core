// Test 91: ref-shims at method sends.
//
// C# `ref` is invariant: a typed lvalue cannot bind a `ref dynamic`
// parameter, a `dynamic` one cannot bind `ref bool`, and a member access
// is not a ref-able location at all. Function calls have had the shim —
// a temp of the parameter's type passed by ref and copied back — since
// test57/66/68. Method sends did not: the shim collector only knew
// FUNCALL nodes, so `oObj:Method(@x)` and `::Super:Method(@x)` emitted
// a bare `ref x` (CS1503 in the easipos corpus: EasiZVT's
// @lPrtMerchantReceipt, BrowseDialog's @cSearchFor), and
// `oObj:cField := Func(@oObj:nField)` fell through because the
// assignment target was a member, not a variable (fnb's
// GetProFormaSerialNo). The write-back into an AS INTEGER member takes
// the same (long) coercion an ordinary assignment gets.
#include "hbclass.ch"

CLASS Clicker
   VAR nCount AS INTEGER INIT 0
   VAR cLabel INIT ""
   METHOD Bump( xValue )
   METHOD Flip( lFlag )
ENDCLASS

/* USUAL slot, by-ref: a typed caller variable needs the shim. */
METHOD Bump( xValue ) CLASS Clicker
   xValue := xValue + 1
RETURN NIL

/* Logical slot, by-ref, returning the new value — used inside an IF so
   the send is hoisted out of the condition. */
METHOD Flip( lFlag ) CLASS Clicker
   lFlag := !lFlag
RETURN lFlag

CLASS Doubler INHERIT Clicker
   METHOD Bump2( nValue )
ENDCLASS

/* ::Super:Method(@param): a decimal parameter into the USUAL slot. */
METHOD Bump2( nValue ) CLASS Doubler
   ::Super:Bump( @nValue )
   ::Super:Bump( @nValue )
RETURN nValue

/* By-ref NUMERIC slot fed from an INTEGER member: ref long vs ref decimal. */
FUNCTION NextSerial( nSerial )
   nSerial++
RETURN "S" + LTrim( Str( nSerial ) )

PROCEDURE Main()
   LOCAL oClicker := Clicker():New()
   LOCAL oDoubler := Doubler():New()
   LOCAL nLocal := 10
   LOCAL xFlag := .F.

   oClicker:Bump( @nLocal )                   // decimal into ref dynamic
   ? "local=" + LTrim( Str( nLocal ) )
   oClicker:Bump( @oClicker:nCount )          // INTEGER member into ref dynamic
   ? "member=" + LTrim( Str( oClicker:nCount ) )
   IF oClicker:Flip( @xFlag )                 // dynamic into ref bool, hoisted
      ? "flag=T"
   ELSE
      ? "flag=F"
   ENDIF
   ? "twice=" + LTrim( Str( oDoubler:Bump2( 5 ) ) )
   oClicker:cLabel := NextSerial( @oClicker:nCount )   // member target, member ref arg
   ? "serial=" + oClicker:cLabel + " count=" + LTrim( Str( oClicker:nCount ) )
RETURN
