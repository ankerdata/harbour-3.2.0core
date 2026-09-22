/*
 * Our tests for reflection (family 14), the ways EasiPOS uses it. So far
 * Type(): EasiPOS asks it whether a PUBLIC exists yet (adtio.prg,
 * easieats.prg, ecr.prg: Type( "aHWFlag" ) == "U") and whether a function
 * is linked (jnlprt.prg: Type( "GetJnlOn()" ) == "U").
 *
 * Written as hbtest writes its assertions; Harbour runs them first, so
 * every expected value here is Harbour's own.
 */

#include "rt_main.ch"

PROCEDURE Main_REFLECT()

   /* A name: the PUBLIC's type, "U" before it exists or while it is NIL */
   HBTEST Type( "pnRtlNeverDeclared" )                IS "U"
   HBTEST Type( "pnRtlCount" )                        IS "U"
   HBTEST DeclarePublics()                            IS .T.
   HBTEST Type( "pnRtlCount" )                        IS "N"
   HBTEST Type( "paRtlList" )                         IS "A"
   HBTEST Type( "pxRtlNil" )                          IS "U"
   HBTEST Type( "PNRTLCOUNT" )                        IS "N"

   /* name(): "UI" for a function of the program, "U" for none */
   HBTEST Type( "ReflectLinked()" )                   IS "UI"
   HBTEST Type( "rtlNoSuchFunction_77()" )            IS "U"

   /* hb_IsFunction(): the program's functions and Harbour's own, in any
      case (messagebox.prg asks it of the BESILENT() hook) */
   HBTEST hb_IsFunction( "ReflectLinked" )            IS .T.
   HBTEST hb_IsFunction( "REFLECTLINKED" )            IS .T.
   HBTEST hb_IsFunction( "rtlNoSuchFunction_77" )     IS .F.
   HBTEST hb_IsFunction( "Str" )                      IS .T.

   /* hb_ExecFromArray(), in each form vm/eval.c reads */
   HBTEST hb_ExecFromArray( { "ReflectLinked" } )     IS 1
   HBTEST hb_ExecFromArray( { "ReflectAdd", 2, 3 } )  IS 5
   HBTEST hb_ExecFromArray( { @ReflectAdd(), 4, 5 } ) IS 9
   HBTEST hb_ExecFromArray( "ReflectAdd", { 6, 7 } )  IS 13
   HBTEST hb_ExecFromArray( {| nA, nB | nA * nB }, { 3, 4 } ) IS 12

   RETURN

STATIC FUNCTION DeclarePublics()

   PUBLIC pnRtlCount := 5
   PUBLIC paRtlList := { 1, 2 }
   PUBLIC pxRtlNil := NIL

   RETURN .T.

FUNCTION ReflectLinked()

   RETURN 1

FUNCTION ReflectAdd( nA, nB )

   RETURN nA + nB
