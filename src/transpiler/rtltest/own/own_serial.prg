/*
 * Our tests for family 12, the serial ports, which hbtest does not cover.
 * EasiPOS reaches them through one class, shared/hbcom.prg — the customer
 * display, the scale, the card terminals and the serial printers — so
 * these are the calls that class makes.
 *
 * The port tests are OPTIONAL: with no port named they assert what the
 * functions answer for a port that is not open, which is most of the
 * surface and needs no hardware. Name a looped-back port in
 * HB_RTL_COM_PORT (pins 2 and 3 bridged, or one end of a virtual pair)
 * and the send-and-receive assertions become real. Harbour and C# both
 * read the same variable on the same machine, so the two agree either way.
 *
 * Written as hbtest writes its assertions; Harbour runs them first, so
 * every expected value here is Harbour's own.
 */

#include "rt_main.ch"
#include "hbcom.ch"

#define A_PORT_THAT_IS_NOT_THERE   99

PROCEDURE Main_SERIAL()

   LOCAL nPort := hb_comFindPort( "COM" + hb_ntos( A_PORT_THAT_IS_NOT_THERE ), .T. )
   LOCAL cBuffer := Space( 8 )

   /* --- the port table: a name is NOT its own number. On Windows
          hb_comGetPortNum() cannot read the digits of "COM1" (an upstream
          slip: it multiplies zero for ever), so a name only ever matches a
          port that was given it, and creating one takes the highest free
          port above 16. EasiPOS hands the number straight to hb_comOpen(),
          so only the name matters. --- */
   HBTEST hb_comFindPort( "COM1" )                    IS 0
   HBTEST hb_comFindPort( "" )                        IS 0
   HBTEST hb_comFindPort( "NOTAPORT" )                IS 0
   HBTEST hb_comFindPort( "NOTAPORT", .T. ) > 16      IS .T.
   HBTEST nPort > 16                                  IS .T.
   HBTEST hb_comFindPort( "COM" + hb_ntos( A_PORT_THAT_IS_NOT_THERE ) ) IS nPort

   /* --- a port that is not open: every call says so --- */
   HBTEST hb_comClose( nPort )                        IS .F.
   HBTEST hb_comGetError( nPort )                     IS HB_COM_ERR_CLOSED
   HBTEST hb_comInit( nPort, 9600, "N", 8, 1 )        IS .F.
   HBTEST hb_comGetError( nPort )                     IS HB_COM_ERR_CLOSED
   HBTEST hb_comSend( nPort, "x" )                    IS -1
   HBTEST hb_comRecv( nPort, @cBuffer )               IS -1
   HBTEST hb_comFlush( nPort )                        IS .F.
   HBTEST hb_comInputCount( nPort )                   IS -1
   HBTEST hb_comOutputCount( nPort )                  IS -1
   HBTEST MCRRead( nPort )                            IS ".F. 0"
   HBTEST MSRRead( nPort )                            IS ".F. 0"
   HBTEST FlowRead( nPort )                           IS ".F. 0"
   HBTEST hb_comFlowSet( nPort, HB_COM_FL_SOFT )      IS .F.

   /* --- a port number that is no port at all --- */
   HBTEST hb_comOpen( 0 )                             IS .F.
   HBTEST hb_comSend( 0, "x" )                        IS -1
   HBTEST hb_comRecv( 0, @cBuffer )                   IS -1

   /* --- opening a port that is not on this machine --- */
   HBTEST hb_comOpen( nPort )                         IS .F.
   HBTEST hb_comGetOSError( nPort ) != 0              IS .T.

   /* --- the buffer a receive leaves alone --- */
   HBTEST Len( cBuffer )                              IS 8

   /* --- optional: a looped-back port, named in HB_RTL_COM_PORT --- */
   HBTEST Loopback()                                  IS iif( Empty( hb_GetEnv( "HB_RTL_COM_PORT" ) ), "no port", "ping" )
   HBTEST LoopbackLines()                             IS iif( Empty( hb_GetEnv( "HB_RTL_COM_PORT" ) ), "no port", "cts" )

   RETURN

/* hb_comMCR() and the others answer through a by-reference value, so the
   result and the value are read together. */
STATIC FUNCTION MCRRead( nPort )

   LOCAL nValue := 123

   RETURN hb_ValToExp( hb_comMCR( nPort, @nValue, 0, 0 ) ) + " " + hb_ntos( nValue )

STATIC FUNCTION MSRRead( nPort )

   LOCAL nValue := 123

   RETURN hb_ValToExp( hb_comMSR( nPort, @nValue ) ) + " " + hb_ntos( nValue )

STATIC FUNCTION FlowRead( nPort )

   LOCAL nValue := 123

   RETURN hb_ValToExp( hb_comFlowControl( nPort, @nValue ) ) + " " + hb_ntos( nValue )

/* Send four bytes and read them back off a port looped onto itself. */
STATIC FUNCTION Loopback()

   LOCAL cPort := hb_GetEnv( "HB_RTL_COM_PORT" )
   LOCAL cBuffer := Space( 4 )
   LOCAL nPort, nSent, nRead

   IF Empty( cPort )
      RETURN "no port"
   ENDIF

   nPort := hb_comFindPort( cPort, .T. )
   IF ! hb_comOpen( nPort )
      RETURN "open failed " + hb_ntos( hb_comGetOSError( nPort ) )
   ENDIF
   hb_comInit( nPort, 9600, "N", 8, 1 )
   hb_comFlush( nPort )
   nSent := hb_comSend( nPort, "ping", 4, 1000 )
   nRead := hb_comRecv( nPort, @cBuffer, 4, 1000 )
   hb_comClose( nPort )

   IF nSent != 4 .OR. nRead != 4
      RETURN "sent " + hb_ntos( nSent ) + " read " + hb_ntos( nRead )
   ENDIF

   RETURN cBuffer

/* RTS looped to CTS: setting RTS shows up in the modem status. */
STATIC FUNCTION LoopbackLines()

   LOCAL cPort := hb_GetEnv( "HB_RTL_COM_PORT" )
   LOCAL nValue := 0
   LOCAL nPort

   IF Empty( cPort )
      RETURN "no port"
   ENDIF

   nPort := hb_comFindPort( cPort, .T. )
   IF ! hb_comOpen( nPort )
      RETURN "open failed"
   ENDIF
   hb_comInit( nPort, 9600, "N", 8, 1 )
   hb_comMCR( nPort, @nValue, 0, HB_COM_MCR_RTS )
   hb_comMSR( nPort, @nValue )
   hb_comClose( nPort )

   RETURN iif( hb_bitAnd( nValue, HB_COM_MSR_CTS ) != 0, "cts", "no cts" )
