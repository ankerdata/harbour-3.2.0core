#include "astype.ch"
// Test 152: the scan knows Harbour's Error class, HbRuntime's HbError.
//
// `oError` held Harbour's error object everywhere (ErrorNew(), a RECOVER
// USING variable, the WITH block's parameter, the parameter of every
// error routine) and was `dynamic` everywhere: the scan knew no class
// named Error, so the name rule had nothing to type it as, `AS CLASS
// Error` was W0040, and every member read went through the binder (in
// EasiPOS, 112 declarations). hbclasses.tab, the runtime's own classes
// in the --extern= table's format and loaded with it, declares Error as
// the C# HbError with its typed members, so `oError` is an HbError by
// its name, its members are typed, and a codeblock whose parameter is
// named oError is typed too. The name is the way: Harbour's own -w3
// refuses `AS CLASS Error` (W0025, no such class declared to it).

#include "hbclass.ch"

FUNCTION Describe152( oError AS OBJECT )
RETURN oError:subSystem + "/" + LTrim(Str(oError:subCode)) + ": " + oError:description

FUNCTION Make152( cText AS STRING ) AS OBJECT
   LOCAL oError := ErrorNew() AS OBJECT
   oError:subSystem := "TEST"
   oError:subCode := 152
   oError:description := cText
   oError:canRetry := .F.
RETURN oError

PROCEDURE Main()
   LOCAL oError := Make152("made by hand") AS OBJECT
   LOCAL bShow := {|oError| Describe152(oError)} AS BLOCK
   LOCAL nZero := 0 AS NUMERIC

   QOut(Describe152(oError))
   QOut(Eval(bShow, oError), oError:canRetry)

   BEGIN SEQUENCE WITH {|oErr| Break( oErr ) }
      QOut(1 / nZero)
   RECOVER USING oError
      QOut(Describe152(oError), oError:operation)
   END SEQUENCE

   BEGIN SEQUENCE
      BREAK Make152("broken")
   RECOVER USING oError
      QOut(oError:description, Len(oError:description))
   END SEQUENCE

RETURN
