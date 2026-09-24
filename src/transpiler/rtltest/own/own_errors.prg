/*
 * Our tests for error handling (family 9), which hbtest checks only
 * through the runtime errors its assertions expect: the Error object,
 * ErrorBlock(), and BREAK / BEGIN SEQUENCE, the ways EasiPOS uses them.
 *
 * Written as hbtest writes its assertions; Harbour runs them first, so
 * every expected value here is Harbour's own.
 *
 * In C# (Alex, 2026-09-22) BREAK throws HbBreak and a plain BEGIN
 * SEQUENCE catches only that, so a runtime error goes on to the error
 * block at the entry point; BEGIN SEQUENCE WITH { |e| break(e) }, the
 * only error block the transpiler accepts, catches every exception and
 * RECOVER USING gets the Error object Harbour would have raised.
 */

#include "rt_main.ch"

PROCEDURE Main_ERRORS()

   /* A new Error object: every member reads as Harbour's getters read
      an unset one */
   HBTEST ValType( ErrorNew() )                       IS "O"
   HBTEST ErrorNew():className()                      IS "ERROR"
   HBTEST ErrorNew():severity                         IS 0
   HBTEST ErrorNew():genCode                          IS 0
   HBTEST ErrorNew():subSystem                        IS ""
   HBTEST ErrorNew():subCode                          IS 0
   HBTEST ErrorNew():description                      IS ""
   HBTEST ErrorNew():operation                        IS ""
   HBTEST ErrorNew():fileName                         IS ""
   HBTEST ErrorNew():osCode                           IS 0
   HBTEST ErrorNew():tries                            IS 0
   HBTEST ErrorNew():canRetry                         IS .F.
   HBTEST ErrorNew():canDefault                       IS .F.
   HBTEST ErrorNew():canSubstitute                    IS .F.
   HBTEST ErrorNew():args                             IS NIL
   HBTEST ErrorNew():cargo                            IS NIL

   /* Filled in as easiudm.prg's SetUDMErrorCode() fills one in */
   HBTEST UdmError():severity                         IS 2
   HBTEST UdmError():subSystem                        IS "UDMPRINT"
   HBTEST UdmError():subCode                          IS 7
   HBTEST UdmError():description                      IS "Paper out"
   HBTEST UdmError():canRetry                         IS .T.
   HBTEST UdmError():canDefault                       IS .F.
   HBTEST UdmError():SubSystem                        IS "UDMPRINT"

   /* BREAK: RECOVER USING gets the value, from however deep it came */
   HBTEST Recovered( 5 )                              IS 5
   HBTEST Recovered( "x" )                            IS "x"
   HBTEST Recovered( NIL )                            IS NIL
   HBTEST Recovered( UdmError() ):subCode             IS 7
   HBTEST BareBreak()                                 IS NIL
   HBTEST BlockBreak()                                IS 7

   /* What runs, in what order: the body up to the BREAK, RECOVER, then
      on after END; no RECOVER ends the BREAK at END; ALWAYS always runs */
   HBTEST Trail()                                     IS "are"
   HBTEST NoRecover()                                 IS "ae"
   HBTEST WithAlways( .F. )                           IS "abwe"
   HBTEST WithAlways( .T. )                           IS "arwe"
   HBTEST AlwaysOnly()                                IS "awrv"
   HBTEST Nested()                                    IS "inout"

   /* WITH { |e| break(e) }: a runtime error reaches RECOVER as an Error */
   HBTEST ValType( ZeroDiv() )                        IS "O"
   HBTEST ZeroDiv():className()                       IS "ERROR"
   HBTEST ZeroDiv():severity                          IS 2
   HBTEST ZeroDiv():genCode                           IS 5
   HBTEST ZeroDiv():subSystem                         IS "BASE"
   HBTEST ZeroDiv():subCode                           IS 1340
   HBTEST ZeroDiv():description                       IS "Zero divisor"
   HBTEST ZeroDiv():operation                         IS "/"
   HBTEST Bound():genCode                             IS 2
   HBTEST Bound():subSystem                           IS "BASE"
   HBTEST Bound():subCode                             IS 1132
   HBTEST Bound():description                         IS "Bound error"
   HBTEST Bound():operation                           IS "array access"
   HBTEST WithValue()                                 IS "own"
   HBTEST WithNil()                                   IS NIL

   /* A plain sequence inside a WITH one. Harbour's WITH block is the
      error block for everything the body runs, so the error BREAKs to
      the inner, nearer sequence; in C# a plain sequence catches only
      BREAK and the WITH one takes it. No WITH body in EasiPOS reaches
      a sequence (they wrap one ADO or OLE call each). */
   HBTEST PlainInsideWith()                           IS "inner"

   /* ErrorBlock(): the one it replaced back, NIL leaves it alone */
   HBTEST BlockRoundTrip()                            IS "mine"
   HBTEST BlockKeep()                                 IS "kept"

   /* An instance variable sent as a message with parentheses answers its
      value, as errorsys.prg's ErrorMessage() reads oError:subsystem() */
   HBTEST UdmError():subSystem()                      IS "UDMPRINT"
   HBTEST UdmError():subCode()                        IS 7
   HBTEST ErrorNew():description()                    IS ""

   /* A message the object does not answer, a hash key that is not there
      and an operator given the wrong types: in C# a runtime binder error
      and a KeyNotFoundException, which HbError.From names as Harbour does */
   HBTEST NoMethod():genCode                          IS 13
   HBTEST NoMethod():subSystem                        IS "BASE"
   HBTEST NoMethod():subCode                          IS 1004
   HBTEST NoMethod():description                      IS "No exported method"
   HBTEST NoMethod():operation                        IS "NOSUCHMESSAGE"
   HBTEST NoValue():genCode                           IS 13
   HBTEST NoValue():subCode                           IS 1004
   HBTEST NoValue():operation                         IS "NOSUCHVALUE"
   HBTEST HashMiss():genCode                          IS 2
   HBTEST HashMiss():subSystem                        IS "BASE"
   HBTEST HashMiss():subCode                          IS 1132
   HBTEST HashMiss():description                      IS "Bound error"
   HBTEST HashMiss():operation                        IS "array access"
   HBTEST OpMismatch():genCode                        IS 1
   HBTEST OpMismatch():subSystem                      IS "BASE"
   HBTEST OpMismatch():subCode                        IS 1083
   HBTEST OpMismatch():description                    IS "Argument error"
   HBTEST OpMismatch():operation                      IS "*"

   RETURN

STATIC FUNCTION UdmError()

   LOCAL oError := ErrorNew()

   oError:severity    := 2
   oError:genCode     := 0
   oError:subSystem   := "UDMPRINT"
   oError:subCode     := 7
   oError:description := "Paper out"
   oError:canRetry    := .T.
   oError:canDefault  := .F.
   oError:fileName    := ""
   oError:osCode      := 0

   RETURN oError

STATIC PROCEDURE BreakWith( xValue )

   BREAK xValue

STATIC FUNCTION Recovered( xValue )

   LOCAL xGot := "none"

   BEGIN SEQUENCE
      BreakWith( xValue )
      xGot := "not reached"
   RECOVER USING xGot
   END SEQUENCE

   RETURN xGot

STATIC FUNCTION BareBreak()

   LOCAL xGot := "unset"

   BEGIN SEQUENCE
      BREAK
   RECOVER USING xGot
   END SEQUENCE

   RETURN xGot

STATIC FUNCTION BlockBreak()

   LOCAL xGot := "unset"

   BEGIN SEQUENCE
      Eval( {|| Break( 7 ) } )
   RECOVER USING xGot
   END SEQUENCE

   RETURN xGot

STATIC FUNCTION Trail()

   LOCAL cTrail := "a"

   BEGIN SEQUENCE
      BreakWith( 1 )
      cTrail += "x"
   RECOVER
      cTrail += "r"
   END SEQUENCE

   RETURN cTrail + "e"

STATIC FUNCTION NoRecover()

   LOCAL cTrail := "a"

   BEGIN SEQUENCE
      BreakWith( 1 )
      cTrail += "x"
   END SEQUENCE

   RETURN cTrail + "e"

STATIC FUNCTION WithAlways( lBreak )

   LOCAL cTrail := ""

   BEGIN SEQUENCE
      cTrail += "a"
      IF lBreak
         BreakWith( 1 )
      ENDIF
      cTrail += "b"
   RECOVER
      cTrail += "r"
   ALWAYS
      cTrail += "w"
   END SEQUENCE

   RETURN cTrail + "e"

STATIC FUNCTION AlwaysOnly()

   LOCAL cTrail := ""
   LOCAL xGot

   BEGIN SEQUENCE
      BEGIN SEQUENCE
         cTrail += "a"
         BreakWith( "v" )
      ALWAYS
         cTrail += "w"
      END SEQUENCE
      cTrail += "x"
   RECOVER USING xGot
      cTrail += "r" + xGot
   END SEQUENCE

   RETURN cTrail

STATIC FUNCTION Nested()

   LOCAL cTrail := ""
   LOCAL xGot

   BEGIN SEQUENCE
      BEGIN SEQUENCE
         BreakWith( "in" )
      RECOVER USING xGot
         cTrail += xGot
         BreakWith( "out" )
      END SEQUENCE
      cTrail += "x"
   RECOVER USING xGot
      cTrail += xGot
   END SEQUENCE

   RETURN cTrail

STATIC FUNCTION ZeroDiv()

   LOCAL oError
   LOCAL nZero := 0

   BEGIN SEQUENCE WITH {| oErr | Break( oErr ) }
      nZero := 1 / nZero
   RECOVER USING oError
   END SEQUENCE

   RETURN oError

STATIC FUNCTION Bound()

   LOCAL oError
   LOCAL aList := { 1, 2 }
   LOCAL nPos := 3

   BEGIN SEQUENCE WITH {| oErr | Break( oErr ) }
      nPos := aList[ nPos ]
   RECOVER USING oError
   END SEQUENCE

   RETURN oError

STATIC FUNCTION NoMethod()

   LOCAL oError
   LOCAL oTarget := ErrorNew()

   BEGIN SEQUENCE WITH {| oErr | Break( oErr ) }
      oTarget:NoSuchMessage()
   RECOVER USING oError
   END SEQUENCE

   RETURN oError

STATIC FUNCTION NoValue()

   LOCAL oError
   LOCAL oTarget := ErrorNew()
   LOCAL xValue

   BEGIN SEQUENCE WITH {| oErr | Break( oErr ) }
      xValue := oTarget:NoSuchValue
   RECOVER USING oError
   END SEQUENCE

   HB_SYMBOL_UNUSED( xValue )

   RETURN oError

STATIC FUNCTION HashMiss()

   LOCAL oError
   LOCAL hList := { "a" => 1 }
   LOCAL nValue

   BEGIN SEQUENCE WITH {| oErr | Break( oErr ) }
      nValue := hList[ "b" ]
   RECOVER USING oError
   END SEQUENCE

   HB_SYMBOL_UNUSED( nValue )

   RETURN oError

STATIC FUNCTION OpMismatch()

   LOCAL oError
   LOCAL aPair := { "a", 1 }   /* elements, so C# meets the types at run time */
   LOCAL xResult

   BEGIN SEQUENCE WITH {| oErr | Break( oErr ) }
      xResult := aPair[ 1 ] * aPair[ 2 ]
   RECOVER USING oError
   END SEQUENCE

   HB_SYMBOL_UNUSED( xResult )

   RETURN oError

STATIC FUNCTION WithValue()

   LOCAL xGot := "unset"

   BEGIN SEQUENCE WITH {| oErr | Break( oErr ) }
      BreakWith( "own" )
   RECOVER USING xGot
   END SEQUENCE

   RETURN xGot

STATIC FUNCTION WithNil()

   LOCAL xGot := "unset"
   LOCAL nZero := 0

   BEGIN SEQUENCE WITH {|| Break() }
      nZero := 1 / nZero
   RECOVER USING xGot
   END SEQUENCE

   RETURN xGot

STATIC FUNCTION PlainInsideWith()

   LOCAL cGot := "none"
   LOCAL nZero := 0

   BEGIN SEQUENCE WITH {| oErr | Break( oErr ) }
      BEGIN SEQUENCE
         nZero := 1 / nZero
      RECOVER
         cGot := "inner"
      END SEQUENCE
   RECOVER
      cGot := "outer"
   END SEQUENCE

   RETURN cGot

STATIC FUNCTION BlockRoundTrip()

   LOCAL bOld := ErrorBlock( {| oErr | HB_SYMBOL_UNUSED( oErr ), "mine" } )
   LOCAL xGot := Eval( ErrorBlock(), NIL )

   ErrorBlock( bOld )

   RETURN xGot

STATIC FUNCTION BlockKeep()

   LOCAL bOld := ErrorBlock( {|| "kept" } )
   LOCAL xGot

   ErrorBlock( NIL )
   xGot := Eval( ErrorBlock() )
   ErrorBlock( bOld )

   RETURN xGot
