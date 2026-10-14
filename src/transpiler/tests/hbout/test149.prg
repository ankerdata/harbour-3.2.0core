#include "astype.ch"
// Test 149: the short overload returns what the canonical returns.
//
// A function with a by-reference parameter gets a second, shorter C#
// overload for the callers that leave the `@` arguments out (test56,
// test65). It forwards to the canonical and answers its result, but it
// was declared `dynamic`, so every short call lost the type the
// canonical had: in EasiPOS 115 routines, `decimal GetQty( ref bool )`
// beside `dynamic GetQty()`. The short overload now carries the
// canonical's return type: long for the counter, string for the label,
// bool for the test; a PROCEDURE stays void.

STATIC siCalls149 := 0 AS NUMERIC

FUNCTION Calls149( /*@*/lFirst AS LOGICAL ) AS NUMERIC
   LOCAL iCalls AS NUMERIC
   siCalls149++
   lFirst := (siCalls149 == 1)
   iCalls := siCalls149
RETURN iCalls

FUNCTION Label149( cPrefix AS STRING, /*@*/cLabel AS STRING, nWidth AS NUMERIC ) AS STRING
   LOCAL cOut AS STRING
   hb_default(@nWidth, 8)
   cLabel := PadR(cPrefix + "-" + Str(Calls149(), 1), nWidth, ".")
   cOut := Upper(cLabel)
RETURN cOut

FUNCTION Even149( nValue AS NUMERIC, /*@*/lOdd AS LOGICAL ) AS LOGICAL
   lOdd := (nValue % 2 == 1)
RETURN !lOdd

PROCEDURE Note149( cText AS STRING, /*@*/nLength AS NUMERIC )
   nLength := Len(cText)
   QOut("note", cText)
RETURN

PROCEDURE Main()
   LOCAL lFirst := .F. AS LOGICAL
   LOCAL cLabel := "" AS STRING
   LOCAL nLength := 0 AS NUMERIC
   LOCAL lOdd := .F. AS LOGICAL
   LOCAL nHalf AS NUMERIC

   // the full call, by reference
   QOut(Calls149(@lFirst), lFirst)
   // the short call: a long
   QOut(Calls149())
   nHalf := Calls149() / 2
   // the whole-number return divides as Harbour's
   QOut(Str(nHalf, 5, 2), Str(Calls149() / 2, 5, 2))

   // the full call, the width defaulted
   QOut(Label149("item", @cLabel), cLabel)
   // the short call: a string
   QOut(Label149("sale"))
   QOut(Len(Label149("tax")))

   QOut(Even149(4, @lOdd), lOdd)
   // the short call: a logical
   QOut(Even149(7))
   IF Even149(10)
      QOut("ten is even")
   ENDIF

   Note149("full", @nLength)
   QOut(nLength)
   // the short call of a procedure: void
   Note149("short")
RETURN
