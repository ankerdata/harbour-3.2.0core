#include "astype.ch"
// Test 128: an empty argument slot on an untyped receiver takes the
// parameter's declared default.
//
// Leaving an argument out is an empty slot, `o:Eval( b, , , , 5 )`: the
// callee declares the default (hb_default( @nNext, 0 )), C# puts it on the
// signature, and the arguments after the gap are named (`nRecord: 5`). That
// took the receiver's class; for a receiver of unknown class (C# dynamic) the
// gap went in as null, which a decimal or bool parameter cannot take - easipos
// ormtestsuite.prg's TestEval on ::oOrmTable failed that way. When every class
// declaring the method gives it the same parameters, an untyped receiver's
// call names them too.

#include "hbclass.ch"

CLASS Counter128

   METHOD Tally128( cLabel, nStart, nStep, lShout )

ENDCLASS

METHOD Tally128( cLabel AS STRING, nStart AS NUMERIC, nStep AS NUMERIC, lShout AS LOGICAL ) AS STRING CLASS Counter128
   hb_default(@nStart, 1)
   hb_default(@nStep, 10)
   hb_default(@lShout, .F.)
RETURN IIF(lShout, Upper(cLabel), cLabel) + " " + hb_ntos(nStart + nStep)

PROCEDURE Main()
   // a hash's value is untyped in C#, so every send below is dynamic
   LOCAL hBox128 := {"counter" => Counter128():New()} AS HASH

   QOut(hBox128["counter"]:Tally128("a"))
   QOut(hBox128["counter"]:Tally128("b", , 5))
   QOut(hBox128["counter"]:Tally128("c", , , .T.))
   QOut(hBox128["counter"]:Tally128("d", 2, , .T.))

RETURN
