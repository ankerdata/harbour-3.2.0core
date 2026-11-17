// Test 176: handles. A `p` name that ends in a kind of handle is the
// runtime's class of it (`pSocket`, `pListenSocket` an HbSocket,
// `pWorkThread` an HbThread, `pMemDBConnection` and `pCountDBStatement`
// hbsqlit3's connection and statement), and a function says what it returns with
// `AS HANDLE OF SOCKET` (DBCONNECTION, DBSTATEMENT, THREAD, MUTEX). A mutex's
// name ends in Mutex or Lock (`pQueueMutex`, `pStateLock`). A `p` name of
// no kind stays opaque.

#include "../include/astype.ch"

PROCEDURE Main()

   LOCAL pSocket := Open176()
   LOCAL pListenSocket
   LOCAL pWorkThread := Start176( 7 )
   LOCAL pMemDBConnection := NoDB176()
   LOCAL pCountDBStatement := NoStmt176()
   LOCAL pOther := 3
   LOCAL pQueueMutex := Mutex176()
   LOCAL pStateLock := hb_mutexCreate()

   hb_threadWait( pWorkThread, 5 )
   ? "open:", pSocket != NIL, Closed176( pSocket )
   pListenSocket := Open176()
   ? "listen:", hb_socketClose( pListenSocket )
   ? "none:", pMemDBConnection == NIL, pCountDBStatement == NIL, pOther
   ? "mutex:", Locked176( pQueueMutex ), Locked176( pStateLock )

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

FUNCTION Mutex176() AS HANDLE OF MUTEX

RETURN hb_mutexCreate()

FUNCTION Locked176( pLock ) AS LOGICAL

   LOCAL lLocked := hb_mutexLock( pLock )

   IF lLocked
      hb_mutexUnlock( pLock )
   ENDIF

RETURN lLocked

FUNCTION NoDB176() AS HANDLE OF DBCONNECTION

RETURN NIL

FUNCTION NoStmt176() AS HANDLE OF DBSTATEMENT

RETURN NIL
