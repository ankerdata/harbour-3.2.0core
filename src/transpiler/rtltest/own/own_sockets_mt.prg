/*
 * Our tests for sockets (family 11), the ways EasiPOS uses them:
 * easisock.prg's client (a connect with a timeout, a send, a receive into
 * a sized buffer), socketloop.prg's servers (bind, listen, accept with a
 * timeout, receive with a timeout until the other end closes),
 * sqliteserver.prg's retried connect, and the error codes and texts they
 * log. Everything runs over loopback; a thread plays the other end.
 *
 * Written as hbtest writes its assertions; Harbour runs them first, so
 * every expected value here is Harbour's own. The name ends in _mt: the
 * other end runs in a thread.
 */

#include "rt_main.ch"
#include "hbsocket.ch"
#include "error.ch"

/* one port per test, so no test binds where another's connection lingers */
#define PORT_ECHO       51737
#define PORT_BYTES      51739
#define PORT_SHORT      51741
#define PORT_INUSE      51743
#define PORT_SHUTDOWN   51745
#define PORT_NOBODY     51747

/* the subCode of hbsockhb.c's argument errors; Harbour's headers name none */
#define SOCKET_ARG_SUBCODE  3012

PROCEDURE Main_SOCKETS()

   HBTEST hb_inetInit()                               IS .T.

   /* The error texts, by Harbour's code */
   HBTEST hb_socketErrorString( HB_SOCKET_ERR_NONE )  IS "OK"
   HBTEST hb_socketErrorString( HB_SOCKET_ERR_TIMEOUT ) IS "ETIMEOUT"
   HBTEST hb_socketErrorString( HB_SOCKET_ERR_CONNREFUSED ) IS "ECONNREFUSED"
   HBTEST hb_socketErrorString( HB_SOCKET_ERR_OTHER ) IS "EOTHER"
   HBTEST hb_socketErrorString( HB_SOCKET_ERR_OTHER + 1 ) IS ""

   /* A socket is a pointer; making one clears the error */
   HBTEST ValType( hb_socketOpen() )                  IS "P"
   HBTEST OpenError()                                 IS ".T. OK"

   /* Accept with nobody connecting: empty, and a timeout */
   HBTEST AcceptTimeout( 0 )                          IS ".T. .T. ETIMEOUT"
   HBTEST AcceptTimeout( 50 )                         IS ".T. .T. ETIMEOUT"

   /* Connect to a port nobody listens on: refused — with a timeout, a
      non-blocking connect waited for, and without, blocking */
   HBTEST ConnectRefused( 5000 )                      IS ".F. .T. ECONNREFUSED"
   HBTEST ConnectRefusedBlocking()                    IS ".F. .T. ECONNREFUSED"

   /* The echo: connect, send, receive over the start of the buffer, which
      keeps its length; then 0 once the server has closed */
   HBTEST Echo( "hello" )                             IS ".T. OK 5 5 [HELLO     ] 0"

   /* Bytes cross as they are */
   HBTEST EchoBytes()                                 IS .T.

   /* nLen reads less than the buffer holds; the rest comes next; with
      nothing more, a receive with a timeout is -1 */
   HBTEST RecvShort()                                 IS "2 [he   ] 3 [llo  ] -1 ETIMEOUT"

   /* A second socket on a port in use */
   HBTEST BindInUse()                                 IS ".F. .T. EADDRINUSE"

   /* Shutting down a listener, which is not connected; a mode that is
      none of the three */
   HBTEST ShutdownListener()                          IS ".F. .T. ENOTCONN .F. .T. EPARAMVALUE"

   /* A closed handle, or an address that is not dotted decimal: the
      argument error */
   HBTEST CloseTwice()                                IS "argument error in HB_SOCKETCLOSE"
   HBTEST BadAddress( "localhost" )                   IS "argument error in HB_SOCKETCONNECT"
   HBTEST BadAddress( "1.2.3" )                       IS "argument error in HB_SOCKETCONNECT"

   HBTEST ValType( hb_inetCleanup() )                 IS "U"

   RETURN

/* The last error, as .T. when it is the one expected, and its text */
STATIC FUNCTION ErrorIs( nExpected )

   RETURN LStr( hb_socketGetError() == nExpected ) + " " + hb_socketErrorString()

STATIC FUNCTION LStr( lValue )

   RETURN iif( lValue, ".T.", ".F." )

STATIC FUNCTION Listener( nPort )

   LOCAL pListen := hb_socketOpen()

   hb_socketBind( pListen, { HB_SOCKET_AF_INET, "127.0.0.1", nPort } )
   hb_socketListen( pListen )

   RETURN pListen

STATIC FUNCTION Connected( nPort )

   LOCAL pSocket := hb_socketOpen()

   hb_socketConnect( pSocket, { HB_SOCKET_AF_INET, "127.0.0.1", nPort }, 5000 )

   RETURN pSocket

/* The other end: accept one connection, then echo what comes (in upper
   case for "upper"), or send "hello", or send nothing, and in those two
   wait for the word to close */
STATIC PROCEDURE Server( pListen, cMode, pMtx )

   LOCAL pConn := hb_socketAccept( pListen, , 5000 )
   LOCAL cBuffer := Space( 100 )
   LOCAL nLen

   IF Empty( pConn )
      RETURN
   ENDIF
   DO CASE
   CASE cMode == "upper" .OR. cMode == "echo"
      nLen := hb_socketRecv( pConn, @cBuffer, , , 5000 )
      IF nLen > 0
         cBuffer := Left( cBuffer, nLen )
         hb_socketSend( pConn, iif( cMode == "upper", Upper( cBuffer ), cBuffer ) )
      ENDIF
   CASE cMode == "hello"
      hb_socketSend( pConn, "hello" )
      hb_mutexSubscribe( pMtx, 5 )
   OTHERWISE
      hb_mutexSubscribe( pMtx, 5 )
   ENDCASE
   hb_socketClose( pConn )

   RETURN

STATIC FUNCTION OpenError()

   LOCAL pSocket := hb_socketOpen()
   LOCAL cResult := ErrorIs( HB_SOCKET_ERR_NONE )

   hb_socketClose( pSocket )

   RETURN cResult

STATIC FUNCTION AcceptTimeout( nTimeout )

   LOCAL pListen := Listener( PORT_NOBODY )
   LOCAL pSocket := hb_socketAccept( pListen, , nTimeout )
   LOCAL cResult := LStr( Empty( pSocket ) ) + " " + ErrorIs( HB_SOCKET_ERR_TIMEOUT )

   hb_socketClose( pListen )

   RETURN cResult

STATIC FUNCTION ConnectRefused( nTimeout )

   LOCAL pSocket := hb_socketOpen()
   LOCAL cResult

   cResult := LStr( hb_socketConnect( pSocket, { HB_SOCKET_AF_INET, "127.0.0.1", PORT_NOBODY }, nTimeout ) )
   cResult += " " + ErrorIs( HB_SOCKET_ERR_CONNREFUSED )
   hb_socketClose( pSocket )

   RETURN cResult

STATIC FUNCTION ConnectRefusedBlocking()

   LOCAL pSocket := hb_socketOpen()
   LOCAL cResult

   cResult := LStr( hb_socketConnect( pSocket, { HB_SOCKET_AF_INET, "127.0.0.1", PORT_NOBODY } ) )
   cResult += " " + ErrorIs( HB_SOCKET_ERR_CONNREFUSED )
   hb_socketClose( pSocket )

   RETURN cResult

STATIC FUNCTION Echo( cSend )

   LOCAL pListen := Listener( PORT_ECHO )
   LOCAL pThread := hb_threadStart( @Server(), pListen, "upper" )
   LOCAL pSocket := hb_socketOpen()
   LOCAL cBuffer := Space( 10 )
   LOCAL cResult
   LOCAL nGot

   cResult := LStr( hb_socketConnect( pSocket, { HB_SOCKET_AF_INET, "127.0.0.1", PORT_ECHO }, 5000 ) )
   cResult += " " + hb_socketErrorString()
   cResult += " " + hb_ntos( hb_socketSend( pSocket, cSend, , , 5000 ) )
   nGot := hb_socketRecv( pSocket, @cBuffer, , , 5000 )
   cResult += " " + hb_ntos( nGot ) + " [" + cBuffer + "]"
   nGot := hb_socketRecv( pSocket, @cBuffer, , , 5000 )
   cResult += " " + hb_ntos( nGot )
   hb_socketClose( pSocket )
   hb_threadWait( pThread, 5 )
   hb_socketClose( pListen )

   RETURN cResult

STATIC FUNCTION EchoBytes()

   LOCAL cSend := Chr( 0 ) + Chr( 255 ) + Chr( 128 ) + "x" + Chr( 13 ) + Chr( 10 ) + Chr( 1 )
   LOCAL pListen := Listener( PORT_BYTES )
   LOCAL pThread := hb_threadStart( @Server(), pListen, "echo" )
   LOCAL pSocket := Connected( PORT_BYTES )
   LOCAL cBuffer := Space( 20 )
   LOCAL nGot

   hb_socketSend( pSocket, cSend )
   nGot := hb_socketRecv( pSocket, @cBuffer, , , 5000 )
   hb_socketClose( pSocket )
   hb_threadWait( pThread, 5 )
   hb_socketClose( pListen )

   RETURN nGot == Len( cSend ) .AND. Left( cBuffer, nGot ) == cSend

STATIC FUNCTION RecvShort()

   LOCAL pMtx := hb_mutexCreate()
   LOCAL pListen := Listener( PORT_SHORT )
   LOCAL pThread := hb_threadStart( @Server(), pListen, "hello", pMtx )
   LOCAL pSocket := Connected( PORT_SHORT )
   LOCAL cBuffer := Space( 5 )
   LOCAL cResult
   LOCAL nGot

   nGot := hb_socketRecv( pSocket, @cBuffer, 2, , 5000 )
   cResult := hb_ntos( nGot ) + " [" + cBuffer + "]"
   nGot := hb_socketRecv( pSocket, @cBuffer, , , 5000 )
   cResult += " " + hb_ntos( nGot ) + " [" + cBuffer + "]"
   nGot := hb_socketRecv( pSocket, @cBuffer, , , 50 )
   cResult += " " + hb_ntos( nGot ) + " " + hb_socketErrorString()
   hb_mutexNotify( pMtx )
   hb_socketClose( pSocket )
   hb_threadWait( pThread, 5 )
   hb_socketClose( pListen )

   RETURN cResult

STATIC FUNCTION BindInUse()

   LOCAL pListen := Listener( PORT_INUSE )
   LOCAL pSocket := hb_socketOpen()
   LOCAL cResult

   cResult := LStr( hb_socketBind( pSocket, { HB_SOCKET_AF_INET, "127.0.0.1", PORT_INUSE } ) )
   cResult += " " + ErrorIs( HB_SOCKET_ERR_ADDRINUSE )
   hb_socketClose( pSocket )
   hb_socketClose( pListen )

   RETURN cResult

STATIC FUNCTION ShutdownListener()

   LOCAL pListen := Listener( PORT_SHUTDOWN )
   LOCAL cResult

   cResult := LStr( hb_socketShutdown( pListen ) ) + " " + ErrorIs( HB_SOCKET_ERR_NOTCONN )
   cResult += " " + LStr( hb_socketShutdown( pListen, HB_SOCKET_SHUT_RDWR + 1 ) ) + ;
              " " + ErrorIs( HB_SOCKET_ERR_PARAMVALUE )
   hb_socketClose( pListen )

   RETURN cResult

STATIC FUNCTION CloseTwice()

   LOCAL pSocket := hb_socketOpen()
   LOCAL oError

   hb_socketClose( pSocket )
   BEGIN SEQUENCE WITH {| oErr | Break( oErr ) }
      hb_socketClose( pSocket )
   RECOVER USING oError
   END SEQUENCE

   RETURN ErrorText( oError )

STATIC FUNCTION BadAddress( cAddress )

   LOCAL pSocket := hb_socketOpen()
   LOCAL oError

   BEGIN SEQUENCE WITH {| oErr | Break( oErr ) }
      hb_socketConnect( pSocket, { HB_SOCKET_AF_INET, cAddress, PORT_NOBODY }, 100 )
   RECOVER USING oError
   END SEQUENCE
   hb_socketClose( pSocket )

   RETURN ErrorText( oError )

/* "argument error in <function>" for hbsockhb.c's argument error, else
   what the error was */
STATIC FUNCTION ErrorText( oError )

   IF oError == NIL
      RETURN "no error"
   ELSEIF oError:genCode == EG_ARG .AND. oError:subCode == SOCKET_ARG_SUBCODE
      RETURN "argument error in " + oError:operation
   ENDIF

   RETURN "genCode " + hb_ntos( oError:genCode ) + ", subCode " + hb_ntos( oError:subCode ) + ;
          " in " + oError:operation
