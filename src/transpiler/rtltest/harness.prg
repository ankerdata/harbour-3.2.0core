/*
 * rtltest harness: what utils/hbtest/hbtest.prg is to the rt_*.prg
 * modules, for the generated test programs (extract.py) and our own
 * (own/*.prg). Built and run twice — by Harbour, whose output is the
 * reference, and transpiled to C# against HbRuntime — and runrtl.py
 * compares the two, assertion by assertion.
 *
 * Each assertion prints one line, "<id> PASS", "<id> FAIL <result> |
 * <expected>" or "<id> RTE <error>". PASS and FAIL follow hbtest's
 * ResultCompare: the same type and ==, or an array/object whose XToStr()
 * is the expected string. An error is RTE on both sides: Harbour's comes
 * through BEGIN SEQUENCE, C#'s is the exception the C# side catches
 * (HbRuntime's stubs throw NotImplementedException), so one missing
 * function fails its own lines and not the run.
 */

#include "set.ch"
#ifndef __HB_TRANSPILER__
#include "error.ch"
#endif

STATIC s_nPass := 0
STATIC s_nFail := 0
STATIC s_nError := 0

PROCEDURE TEST_BEGIN()

   /* hbtest's TEST_BEGIN: the date format the expected results use,
      and SET EXACT OFF */
   Set( _SET_DATEFORMAT, "yyyy-mm-dd" )
   Set( _SET_EXACT, .F. )

   RETURN

PROCEDURE TEST_END()

   ? "passed", s_nPass, "failed", s_nFail, "errors", s_nError

   RETURN

PROCEDURE TEST_CALL( cId, cExpr, bBlock, xExpected, lHasAlt, xAlt )

   LOCAL cVerdict
   LOCAL lError := .F.
#ifndef __HB_TRANSPILER__
   LOCAL oError
   LOCAL bOldError := ErrorBlock( {| oErr | Break( oErr ) } )

   BEGIN SEQUENCE
      cVerdict := TEST_Verdict( bBlock, xExpected, lHasAlt, xAlt )
   RECOVER USING oError
      lError := .T.
      cVerdict := "RTE " + ErrorMessage( oError )
   END SEQUENCE
   ErrorBlock( bOldError )
#else
#pragma BEGINCSHARP
   try
   {
      cVerdict = TEST_Verdict(bBlock, xExpected, lHasAlt, xAlt);
   }
   catch (System.Exception e)
   {
      // Eval invokes the block through reflection, which wraps what
      // the block threw.
      System.Exception eInner = e is System.Reflection.TargetInvocationException t && t.InnerException != null ? t.InnerException : e;
      lError = true;
      cVerdict = "RTE " + eInner.GetType().Name + ": " + eInner.Message;
   }
#pragma ENDCSHARP
#endif

   HB_SYMBOL_UNUSED( cExpr )

   IF lError
      s_nError++
   ELSEIF cVerdict == "PASS"
      s_nPass++
   ELSE
      s_nFail++
   ENDIF
   ? cId, cVerdict

   RETURN

/* The assertion's verdict, "PASS" or "FAIL <result> | <expected>". The
   evaluation, the comparison and the formatting all run under
   TEST_CALL's guard: an error in any of them is the assertion's RTE,
   never the end of the run. */
FUNCTION TEST_Verdict( bBlock, xExpected, lHasAlt, xAlt )

   LOCAL xResult := Eval( bBlock )

   IF ResultMatches( xResult, xExpected ) .OR. ( lHasAlt .AND. ResultMatches( xResult, xAlt ) )
      RETURN "PASS"
   ENDIF

   RETURN "FAIL " + XToStr( xResult ) + " | " + XToStr( xExpected )

/* hbtest's ResultCompare, inverted: .T. when the result is the one expected */
STATIC FUNCTION ResultMatches( xResult, xExpected )

   IF ValType( xResult ) == ValType( xExpected )
      RETURN xResult == xExpected
   ELSEIF ValType( xExpected ) == "C" .AND. ValType( xResult ) $ "ABMO"
      RETURN XToStr( xResult ) == xExpected
   ENDIF

   RETURN .F.

/* hbtest's XToStr */
FUNCTION XToStr( xValue )

   LOCAL cType := ValType( xValue )

   DO CASE
   CASE cType == "C"
      xValue := StrTran( xValue, Chr( 0 ), '" + Chr( 0 ) + "' )
      xValue := StrTran( xValue, Chr( 9 ), '" + Chr( 9 ) + "' )
      xValue := StrTran( xValue, Chr( 10 ), '" + Chr( 10 ) + "' )
      xValue := StrTran( xValue, Chr( 13 ), '" + Chr( 13 ) + "' )
      xValue := StrTran( xValue, Chr( 26 ), '" + Chr( 26 ) + "' )
      RETURN '"' + xValue + '"'
   CASE cType == "N" ; RETURN LTrim( Str( xValue ) )
   CASE cType == "D" ; RETURN 'hb_SToD("' + DToS( xValue ) + '")'
   CASE cType == "L" ; RETURN iif( xValue, ".T.", ".F." )
   CASE cType == "O" ; RETURN xValue:className() + " Object"
   CASE cType == "U" ; RETURN "NIL"
   CASE cType == "B" ; RETURN "{||...}"
   CASE cType == "A" ; RETURN "{.[" + LTrim( Str( Len( xValue ) ) ) + "].}"
   CASE cType == "M" ; RETURN "M:" + '"' + xValue + '"'
   ENDCASE

   RETURN ""

/* hbtest's XToStrX: rt_array formats its arrays with it */
FUNCTION XToStrX( xValue )

   LOCAL cType := ValType( xValue )
   LOCAL tmp
   LOCAL cRetVal

   DO CASE
   CASE cType == "C"
      xValue := StrTran( xValue, Chr( 0 ), '" + Chr( 0 ) + "' )
      xValue := StrTran( xValue, Chr( 1 ), '" + Chr( 1 ) + "' )
      xValue := StrTran( xValue, Chr( 2 ), '" + Chr( 2 ) + "' )
      xValue := StrTran( xValue, Chr( 9 ), '" + Chr( 9 ) + "' )
      xValue := StrTran( xValue, Chr( 10 ), '" + Chr( 10 ) + "' )
      xValue := StrTran( xValue, Chr( 13 ), '" + Chr( 13 ) + "' )
      xValue := StrTran( xValue, Chr( 26 ), '" + Chr( 26 ) + "' )
      RETURN xValue
   CASE cType == "N" ; RETURN LTrim( Str( xValue ) )
   CASE cType == "D" ; RETURN DToS( xValue )
   CASE cType == "L" ; RETURN iif( xValue, ".T.", ".F." )
   CASE cType == "O" ; RETURN xValue:className() + " Object"
   CASE cType == "U" ; RETURN "NIL"
   CASE cType == "B" ; RETURN "{||...} -> " + XToStrX( Eval( xValue ) )
   CASE cType == "A"
      cRetVal := "{ "
      FOR tmp := 1 TO Len( xValue )
         cRetVal += XToStrX( xValue[ tmp ] )
         IF tmp < Len( xValue )
            cRetVal += ", "
         ENDIF
      NEXT
      RETURN cRetVal + " }"
   CASE cType == "M" ; RETURN "M:" + xValue
   ENDCASE

   RETURN ""

/* hbtest's rt_*.prg call these (hbtest.prg defines them) */
FUNCTION TEST_DBFAvail()
   RETURN .F.

FUNCTION TEST_OPT_Z()
   RETURN .T.

#ifndef __HB_TRANSPILER__

/* hbtest's ErrorMessage: the error as hbtest's expected results spell it */
STATIC FUNCTION ErrorMessage( oError )

   LOCAL cMessage := ""
   LOCAL tmp

   IF ValType( oError:severity ) == "N"
      DO CASE
      CASE oError:severity == ES_WHOCARES     ; cMessage += "M "
      CASE oError:severity == ES_WARNING      ; cMessage += "W "
      CASE oError:severity == ES_ERROR        ; cMessage += "E "
      CASE oError:severity == ES_CATASTROPHIC ; cMessage += "C "
      ENDCASE
   ENDIF
   IF ValType( oError:genCode ) == "N"
      cMessage += LTrim( Str( oError:genCode ) ) + " "
   ENDIF
   IF ValType( oError:subsystem ) == "C"
      cMessage += oError:subsystem + " "
   ENDIF
   IF ValType( oError:subCode ) == "N"
      cMessage += LTrim( Str( oError:subCode ) ) + " "
   ENDIF
   IF ValType( oError:description ) == "C"
      cMessage += oError:description + " "
   ENDIF
   IF ! Empty( oError:operation )
      cMessage += "(" + oError:operation + ") "
   ENDIF
   IF ! Empty( oError:filename )
      cMessage += "<" + oError:filename + "> "
   ENDIF
   IF ValType( oError:osCode ) == "N"
      cMessage += "OS:" + LTrim( Str( oError:osCode ) ) + " "
   ENDIF
   IF ValType( oError:tries ) == "N"
      cMessage += "#:" + LTrim( Str( oError:tries ) ) + " "
   ENDIF
   IF ValType( oError:Args ) == "A"
      cMessage += "A:" + LTrim( Str( Len( oError:Args ) ) ) + ":"
      FOR tmp := 1 TO Len( oError:Args )
         cMessage += ValType( oError:Args[ tmp ] )
         IF tmp < Len( oError:Args )
            cMessage += ";"
         ENDIF
      NEXT
      cMessage += " "
   ENDIF

   RETURN cMessage

#endif
