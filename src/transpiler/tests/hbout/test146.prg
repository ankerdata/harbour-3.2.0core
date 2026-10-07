#include "astype.ch"
// Test 146: an argument left out of a by-reference slot is NIL every time.
//
// A parameter some caller passes with @ is `ref` in C# for every caller, so a
// caller that leaves it out before an argument it does pass gives
// `ref HbDiscard<T>.Value`. That was one shared field, and a callee that reads
// its parameter before writing it read whatever the last call through the
// slot had written back: EasiPOS's Plu(), whose `DEFAULT nRFOrVoidIndex TO 0`
// is NIL's reading, took a line index an earlier call had left and stopped on
// a bound error.

STATIC FUNCTION Bump146( /*@*/nCount AS NUMERIC, nStep AS NUMERIC ) AS NUMERIC
   hb_default(@nCount, 0)
   nCount += nStep
RETURN nCount

STATIC FUNCTION Tag146( /*@*/cTag AS STRING, cAdd AS STRING ) AS STRING
   hb_default(@cTag, "")
   cTag += cAdd
RETURN cTag

PROCEDURE Main()

   LOCAL nMine146 := 5 AS NUMERIC
   LOCAL cMine146 := "a" AS STRING

   QOut("number, with @:", Bump146(@nMine146, 10), nMine146)
   QOut("number, left out:", Bump146(, 10))
   QOut("number, left out again:", Bump146(, 10))

   QOut("string, with @:", Tag146(@cMine146, "x"), cMine146)
   QOut("string, left out:", Tag146(, "x"))
   QOut("string, left out again:", Tag146(, "x"))

RETURN
