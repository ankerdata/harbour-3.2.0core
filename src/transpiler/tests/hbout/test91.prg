#include "astype.ch"
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

   DATA nCount AS INTEGER INIT 0
   DATA cLabel AS STRING INIT ""
   METHOD Bump( xValue )
   METHOD Flip( lFlag )

ENDCLASS

/* USUAL slot, by-ref: a typed caller variable needs the shim. */

CLASS Doubler INHERIT Clicker

   METHOD Bump2( nValue )

ENDCLASS

METHOD Bump( xValue AS USUAL ) CLASS Clicker
   xValue := xValue + 1
RETURN NIL

   /* Logical slot, by-ref, returning the new value — used inside an IF so
   the send is hoisted out of the condition. */
METHOD Flip( lFlag AS LOGICAL ) AS LOGICAL CLASS Clicker
   lFlag := !lFlag
RETURN lFlag

   /* ::Super:Method(@param): a decimal parameter into the USUAL slot. */
METHOD Bump2( nValue AS NUMERIC ) AS NUMERIC CLASS Doubler
   ::Super:Bump(@nValue)
   ::Super:Bump(@nValue)
RETURN nValue

   /* By-ref NUMERIC slot fed from an INTEGER member: ref long vs ref decimal. */
FUNCTION NextSerial( /*@*/nSerial AS NUMERIC ) AS STRING
   nSerial++
RETURN "S" + LTrim(Str(nSerial))

PROCEDURE Main()
   LOCAL oClicker := Clicker():New() AS OBJECT
   LOCAL oDoubler := Doubler():New() AS OBJECT
   LOCAL nLocal := 10 AS NUMERIC
   LOCAL xFlag := .F. AS LOGICAL

   // decimal into ref dynamic
   oClicker:Bump(@nLocal)
   QOut("local=" + LTrim(Str(nLocal)))
   // INTEGER member into ref dynamic
   oClicker:Bump(@oClicker:nCount)
   QOut("member=" + LTrim(Str(oClicker:nCount)))
   // dynamic into ref bool, hoisted
   IF oClicker:Flip(@xFlag)
      QOut("flag=T")
   ELSE
      QOut("flag=F")
   ENDIF

   QOut("twice=" + LTrim(Str(oDoubler:Bump2(5))))
   // member target, member ref arg
   oClicker:cLabel := NextSerial(@oClicker:nCount)
   QOut("serial=" + oClicker:cLabel + " count=" + LTrim(Str(oClicker:nCount)))
RETURN
