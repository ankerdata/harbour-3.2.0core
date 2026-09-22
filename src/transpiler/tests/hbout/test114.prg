#include "astype.ch"
// Test 114: BREAK, BEGIN SEQUENCE and the Error object (family 9).
//
// BREAK emitted `throw new Exception(x)`, which binds an Error object to
// Exception(string) and throws at run time; RECOVER USING got the .NET
// exception instead of the BREAK's value; and BEGIN SEQUENCE WITH <block>
// was parsed and then dropped. Now (Alex, 2026-09-22) BREAK throws
// HbBreak carrying its value and a plain sequence catches only that, so
// a runtime error goes on to the error block at the entry point, as in
// Harbour; WITH { |e| break(e) }, the one error block the transpiler
// accepts (any other is E0101), catches every exception, RECOVER USING
// getting the Error object HbError.From makes of it. A RECOVER USING
// target that commits to no type is dynamic, whatever its initializer.

PROCEDURE Main()

   LOCAL oError AS OBJECT
   LOCAL xGot := "unset" AS USUAL
   LOCAL nZero := 0 AS NUMERIC
   LOCAL cTrail := "" AS STRING
   LOCAL bOld AS BLOCK

   // BREAK with a value, from a called function
   BEGIN SEQUENCE
      Brk114Throw(5)
   RECOVER USING xGot
   END SEQUENCE
   QOut("value:", xGot)

   // an Error object built and broken, as easiudm.prg's print errors are
   BEGIN SEQUENCE
      Brk114Throw(Brk114Udm())
   RECOVER USING oError
      QOut("udm:", oError:subSystem, oError:subCode, oError:description)
   END SEQUENCE

   // Break() in a codeblock: reaches the sequence through Eval() as the
   // BREAK it is, not wrapped by reflection
   BEGIN SEQUENCE
      Eval({|| Break("from a block")})
   RECOVER USING xGot
   END SEQUENCE
   QOut("block:", xGot)

   // a bare BREAK: RECOVER USING gets NIL
   BEGIN SEQUENCE
      BREAK
   RECOVER USING xGot
   END SEQUENCE
   QOut("bare:", ValType(xGot))

   // WITH { |e| break(e) }: a runtime error reaches RECOVER as an Error
   BEGIN SEQUENCE WITH {|oErr| Break( oErr ) }
      QOut(1 / nZero)
   RECOVER USING oError
      QOut("zero:", oError:genCode, oError:subSystem, oError:subCode, oError:description, oError:operation)
   END SEQUENCE

   // WITH { || break() }: the error is NIL, an explicit BREAK keeps its value
   BEGIN SEQUENCE WITH {|| Break() }
      QOut(1 / nZero)
   RECOVER USING xGot
      QOut("nil block:", ValType(xGot))
   END SEQUENCE

   BEGIN SEQUENCE WITH {|| Break() }
      Brk114Throw("own")
   RECOVER USING xGot
      QOut("own value:", xGot)
   END SEQUENCE

   // no RECOVER: the BREAK ends at END; ALWAYS always runs, and without
   // RECOVER lets the BREAK go on outward
   BEGIN SEQUENCE
      cTrail += "a"
      Brk114Throw(1)
   END SEQUENCE

   BEGIN SEQUENCE
      BEGIN SEQUENCE
         cTrail += "b"
         Brk114Throw("c")
      ALWAYS
         cTrail += "w"
      END SEQUENCE
   RECOVER USING xGot
      cTrail += xGot
   END SEQUENCE

   QOut("trail:", cTrail)

   // a BREAK inside RECOVER goes to the next sequence out
   BEGIN SEQUENCE
      BEGIN SEQUENCE
         Brk114Throw("in")
      RECOVER USING xGot
         Brk114Throw(xGot + "out")
      END SEQUENCE
   RECOVER USING xGot
      QOut("nested:", xGot)
   END SEQUENCE

   // ErrorBlock(): the one it replaced comes back
   bOld := ErrorBlock({|oErr| Brk114Handler(oErr)})
   QOut("error block:", Eval(ErrorBlock(), Brk114Udm()))
   ErrorBlock(bOld)

RETURN

STATIC PROCEDURE Brk114Throw( xValue AS USUAL )

   BREAK xValue

STATIC FUNCTION Brk114Udm() AS OBJECT

   LOCAL oError := ErrorNew() AS OBJECT

   oError:severity := 2
   oError:subSystem := "UDMPRINT"
   oError:subCode := 7
   oError:description := "Paper out"
   oError:canRetry := .T.

RETURN oError

STATIC FUNCTION Brk114Handler( oErr AS OBJECT )

RETURN "handled " + oErr:subSystem
