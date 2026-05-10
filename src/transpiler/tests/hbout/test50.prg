#include "astype.ch"
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

STATIC sfPrint := @PrintTag() AS BLOCK

CLASS Widget

   DATA cLabel AS STRING INIT ""
   DATA fHook AS BLOCK
   METHOD New( cLabel, fHook )
   METHOD Fire()

ENDCLASS

METHOD New( cLabel AS STRING, fHook AS BLOCK ) AS OBJECT CLASS Widget
   ::cLabel := cLabel
   ::fHook := fHook
RETURN Self

METHOD Fire() CLASS Widget
   IF ::fHook != NIL
      QOut("fire: " + ::fHook:exec(::cLabel))
   ENDIF

RETURN NIL

PROCEDURE Main()
   LOCAL fAdd := @AddOne() AS BLOCK
   LOCAL fGreet AS BLOCK
   LOCAL oWidget AS OBJECT

   fGreet := @SayHello()

   QOut("fAdd(2,3)=" + LTrim(Str(fAdd:exec(2, 3))))
   QOut("fGreet=>" + fGreet:exec("world"))
   QOut("sfPrint=>" + sfPrint:exec("hello"))
   QOut("Apply=>" + Apply(@AddOne(), 10, 20))

   oWidget := Widget():New("OK", @PrintTag())
   oWidget:Fire()

   Invoke(@SayHello(), "param-passed")
RETURN

FUNCTION AddOne( nX AS NUMERIC, nY AS NUMERIC ) AS NUMERIC
RETURN nX + nY

FUNCTION SayHello( cName AS STRING ) AS STRING
RETURN "Hello, " + cName

FUNCTION PrintTag( cTag AS STRING ) AS STRING
RETURN "tag:" + cTag

FUNCTION Apply( fOp AS BLOCK, nX AS NUMERIC, nY AS NUMERIC ) AS STRING
RETURN LTrim(Str(fOp:exec(nX, nY)))

PROCEDURE Invoke( fHandler AS BLOCK, cArg AS STRING )
   QOut("Invoke=>" + fHandler:exec(cArg))
RETURN
