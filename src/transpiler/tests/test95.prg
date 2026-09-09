// Test 95: three by-ref call shapes at method sends and in conditions.
//
// (1) `GetBrewer():Measure( "tea", @nOut )` — the receiver is a
//     function returning the object, so the method row comes from the
//     function's return type; with the row known, the omitted trailing
//     by-ref slot (aLog) is padded with the shared discard instead of
//     being dropped (CS7036: `GetsoEasiCdS():GetVoucher(...)` in the
//     easipos corpus).
// (2) `WHILE Tick( @n, .F. )` / `ELSEIF Peel( oBrewer:cName ) == "t"` —
//     a literal or a member access at a by-ref slot inside a WHILE or
//     ELSEIF condition, where no shim block can be placed and hoisting
//     would evaluate the call once: the inline discard form
//     (`ref HbDiscard<T>.Seed( x )`, plain variables since test80) now
//     covers every non-@ shape (CS1620).
// (3) `Nudge( @i )` on a FOR counter the transpiler types INTEGER: the
//     shim temp is decimal and its write-back into the long takes the
//     (long) an AS INTEGER member already got (CS0266).
#include "hbclass.ch"

CLASS Brewer
   VAR cName INIT "tea"
   METHOD Measure( cWhat, nOut, aLog )
ENDCLASS

METHOD Measure( cWhat, nOut, aLog ) CLASS Brewer
   nOut := Len( cWhat ) * 10
   aLog := { cWhat }
RETURN nOut

STATIC FUNCTION GetBrewer()
   STATIC soBrewer
   IF soBrewer == NIL
      soBrewer := Brewer():New()
   ENDIF
RETURN soBrewer

FUNCTION Tick( nCount, lReset )
   IF lReset
      nCount := 0
      lReset := .F.
   ENDIF
   nCount++
RETURN nCount < 3

FUNCTION Peel( cText )
   cText := Left( cText, 1 )
RETURN cText

FUNCTION Nudge( nValue )
   nValue := nValue + 10
RETURN NIL

PROCEDURE Main()
   LOCAL nOut := 0
   LOCAL n := 5
   LOCAL i
   LOCAL lReset := .T.
   LOCAL cWord := "tea"
   LOCAL aLog := {}
   LOCAL oBrewer := GetBrewer()

   oBrewer:Measure( "coffee", @nOut, @aLog )     // @aLog: the slot is by-ref
   ? "coffee=" + LTrim( Str( nOut ) ) + " log=" + aLog[ 1 ]
   GetBrewer():Measure( "tea", @nOut )            // aLog omitted
   ? "tea=" + LTrim( Str( nOut ) )

   Tick( @n, @lReset )                            // @lReset: the slot is by-ref
   ? "reset=" + LTrim( Str( n ) ) + " " + IIF( lReset, "T", "F" )
   WHILE Tick( @n, .F. )
      ? "tick=" + LTrim( Str( n ) )
   END

   Peel( @cWord )                                 // @cWord: the slot is by-ref
   IF oBrewer:cName == "coffee"
      ? "coffee"
   ELSEIF Peel( oBrewer:cName ) == "t"
      ? "tea " + cWord
   ENDIF

   FOR i := 1 TO 2
      Nudge( @i )
   NEXT
   ? "i=" + LTrim( Str( i ) )
RETURN
