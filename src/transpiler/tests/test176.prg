// Test 176: handles. A `p` name that ends in a kind of handle is the
// runtime's class of it (`pSocket`, `pListenSocket` an HbSocket,
// `pWorkThread` an HbThread, `pMemDB` and `pCountStmt` hbsqlit3's
// connection and statement), and a function says what it returns with
// `AS HANDLE OF SOCKET` (DATABASE, STATEMENT, THREAD). A `p` name of no
// kind stays opaque.

#include "../include/astype.ch"

PROCEDURE Main()

   LOCAL pSocket := Open176()
   LOCAL pListenSocket
   LOCAL pWorkThread := Start176( 7 )
   LOCAL pMemDB := NoDB176()
   LOCAL pCountStmt := NoStmt176()
   LOCAL pOther := 3

   hb_threadWait( pWorkThread, 5 )
   ? "open:", pSocket != NIL, Closed176( pSocket )
   pListenSocket := Open176()
   ? "listen:", hb_socketClose( pListenSocket )
   ? "none:", pMemDB == NIL, pCountStmt == NIL, pOther

   RETURN

FUNCTION Open176() AS HANDLE OF SOCKET

RETURN hb_socketOpen()

FUNCTION Closed176( pSocket ) AS LOGICAL

RETURN hb_socketClose( pSocket )

FUNCTION Start176( iValue ) AS HANDLE OF THREAD

RETURN hb_threadStart( @Work176(), iValue )

PROCEDURE Work176( iValue )

   ? "thread:", iValue

   RETURN

FUNCTION NoDB176() AS HANDLE OF DATABASE

RETURN NIL

FUNCTION NoStmt176() AS HANDLE OF STATEMENT

RETURN NIL
