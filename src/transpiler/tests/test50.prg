// Test 50: Function-pointer typing via 'f' prefix.
//
// In Harbour, @FuncName() takes the address of a function and yields
// a function pointer; the receiving parameter is invoked with
// fName:exec(args). The 'f' Hungarian prefix marks the parameter /
// variable as a function pointer (distinct from a code block, which
// uses 'b' and Eval).
//
// Coverage:
//   LOCAL fAdd := @AddOne()           -- prefix + initialiser via @
//   LOCAL fGreet                      -- prefix only, assigned later
//   PARAM fOp / fHandler              -- f-prefix parameter
//   STATIC sfPrint := @PrintTag()     -- s<H> two-letter convention
//   DATA fHook                        -- class member f-prefix
//   :exec(args) invocation            -- runtime dispatch
//
// Pattern observed in easipos adtdata.prg:
//   WriteFixedTotals(oTransaction, @ADTFixed(), cRecType, nSign)
//   PROCEDURE WriteFixedTotals(oTransaction, fFixedOutput, ...)
//      ...fFixedOutput:exec(cRecType, ...)
//
// All declarations must pass the W0021 Hungarian audit silently and
// runtime evaluation must produce identical output across the .prg,
// .hb, and .cs pipelines.

#include "hbclass.ch"

STATIC sfPrint := @PrintTag()

CLASS Widget
   DATA cLabel INIT ""
   DATA fHook
   METHOD New(cLabel, fHook)
   METHOD Fire()
ENDCLASS

METHOD New(cLabel, fHook) CLASS Widget
   ::cLabel := cLabel
   ::fHook  := fHook
RETURN Self

METHOD Fire() CLASS Widget
   IF ::fHook != NIL
      ? "fire: " + ::fHook:exec(::cLabel)
   ENDIF
RETURN NIL

PROCEDURE Main()
   LOCAL fAdd := @AddOne()
   LOCAL fGreet
   LOCAL oWidget

   fGreet := @SayHello()

   ? "fAdd(2,3)="  + LTrim(Str(fAdd:exec(2, 3)))
   ? "fGreet=>"    + fGreet:exec("world")
   ? "sfPrint=>"   + sfPrint:exec("hello")
   ? "Apply=>"     + Apply(@AddOne(), 10, 20)

   oWidget := Widget():New("OK", @PrintTag())
   oWidget:Fire()

   Invoke(@SayHello(), "param-passed")
RETURN

FUNCTION AddOne(nX, nY)
   RETURN nX + nY

FUNCTION SayHello(cName)
   RETURN "Hello, " + cName

FUNCTION PrintTag(cTag)
   RETURN "tag:" + cTag

FUNCTION Apply(fOp, nX, nY)
   RETURN LTrim(Str(fOp:exec(nX, nY)))

PROCEDURE Invoke(fHandler, cArg)
   ? "Invoke=>" + fHandler:exec(cArg)
RETURN
