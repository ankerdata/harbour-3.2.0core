#include "astype.ch"
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

   DATA cName AS STRING INIT "tea"
   METHOD Measure( cWhat, nOut, aLog )

ENDCLASS

METHOD Measure( cWhat AS STRING, nOut AS NUMERIC, aLog AS ARRAY ) AS NUMERIC CLASS Brewer
   nOut := Len(cWhat) * 10
   aLog := {cWhat}
RETURN nOut

STATIC FUNCTION GetBrewer() AS OBJECT
   STATIC soBrewer AS OBJECT
   IF soBrewer == NIL
      soBrewer := Brewer():New()
   ENDIF

RETURN soBrewer

FUNCTION Tick( /*@*/nCount AS NUMERIC, /*@*/lReset AS LOGICAL ) AS LOGICAL
   IF lReset
      nCount := 0
      lReset := .F.
   ENDIF

   nCount++
RETURN nCount < 3

FUNCTION Peel( /*@*/cText AS STRING ) AS STRING
   cText := Left(cText, 1)
RETURN cText

FUNCTION Nudge( /*@*/nValue AS NUMERIC )
   nValue := nValue + 10
RETURN NIL

PROCEDURE Main()
   LOCAL nOut := 0 AS NUMERIC
   LOCAL n := 5 AS NUMERIC
   LOCAL i AS NUMERIC
   LOCAL lReset := .T. AS LOGICAL
   LOCAL cWord := "tea" AS STRING
   LOCAL aLog := {} AS ARRAY
   LOCAL oBrewer := GetBrewer() AS OBJECT

   // @aLog: the slot is by-ref
   oBrewer:Measure("coffee", @nOut, @aLog)
   QOut("coffee=" + LTrim(Str(nOut)) + " log=" + aLog[1])
   // aLog omitted
   GetBrewer():Measure("tea", @nOut)
   QOut("tea=" + LTrim(Str(nOut)))

   // @lReset: the slot is by-ref
   Tick(@n, @lReset)
   QOut("reset=" + LTrim(Str(n)) + " " + IIF(lReset, "T", "F"))
   DO WHILE Tick(@n, .F.)
      QOut("tick=" + LTrim(Str(n)))
   ENDDO

   // @cWord: the slot is by-ref
   Peel(@cWord)
   IF oBrewer:cName == "coffee"
      QOut("coffee")
   ELSEIF Peel(oBrewer:cName) == "t"
      QOut("tea " + cWord)
   ENDIF

   FOR i := 1 TO 2
      Nudge(@i)
   NEXT

   QOut("i=" + LTrim(Str(i)))
RETURN
