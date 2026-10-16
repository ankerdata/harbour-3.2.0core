#include "astype.ch"
// Test 154: a method send with a reference shim, standing in an expression.
//
// A call whose `@` argument needs a shim (the variable's type is not the
// slot's) is hoisted out of its expression into a temporary, so the shim's
// write-back can follow the call: `var _hbcall_<callee> = …`. The callee's
// name was the reftab key, which for a method is `Class::Class__Method`,
// and `var _hbcall_EasiTSE::EasiTSE__FinishTender = …` is not C#
// (CS1001): EasiPOS's fmde.prg hit it the first time a long local went
// into a method's decimal by-reference slot inside an IF. The temporary's
// name is an identifier now, whatever the key.

#include "hbclass.ch"

CLASS Meter154

   DATA nReadings AS INTEGER INIT 0
   METHOD Read( nValue, nCount )

ENDCLASS

METHOD Read( nValue AS NUMERIC, nCount AS NUMERIC ) AS NUMERIC CLASS Meter154
   nCount := ::nReadings + 1
   ::nReadings := nCount
RETURN nValue * 2

PROCEDURE Main()
   LOCAL oMeter := Meter154():New() AS OBJECT
   // a long, into the method's decimal slot: a shim
   LOCAL iCount := 0 AS NUMERIC

   // the send hoisted out of the IF
   IF oMeter:Read(21, @iCount) == 42
      QOut("read", iCount)
   ENDIF

   // a second, its own temporary
   IF oMeter:Read(5, @iCount) == 10
      QOut("again", iCount)
   ENDIF

   QOut(iCount, oMeter:nReadings)
RETURN
