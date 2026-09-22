/*
 * Our tests for threads and mutexes (family 10), the ways EasiPOS uses
 * them: threadmanager.prg's threads, sqliteserver.prg's request and reply
 * queues, the shutdown's polite wait and quit request, and the thread
 * numbers EasiPOS's thread checks rest on.
 *
 * Written as hbtest writes its assertions; Harbour runs them first, so
 * every expected value here is Harbour's own. The name ends in _mt, so
 * the Harbour side is built with threads (-mt).
 */

#include "rt_main.ch"
#include "set.ch"

THREAD STATIC snCount := 0

PROCEDURE Main_THREADS()

   /* Thread numbers: the main thread is 1, each thread started the next
      (hvm.c s_threadNo), the same inside the thread as outside */
   HBTEST hb_threadID()                               IS 1
   HBTEST ThreadNumbers()                             IS "2 2 3 3"
   HBTEST ValType( hb_threadSelf() )                  IS "P"
   HBTEST hb_threadID( hb_threadSelf() )              IS 1

   /* hb_threadWait(): 1 for a finished thread, 0 on a timeout; with lAll,
      how many finished */
   HBTEST WaitOne()                                   IS 1
   HBTEST WaitTimeout()                               IS 0
   HBTEST WaitAll()                                   IS 2

   /* A thread starts with a copy of its parent's SETs; its own changes
      stay its own */
   HBTEST SetsInherited()                             IS ".T. 4 2"

   /* THREAD STATIC: each thread has its own, starting from the declared
      value */
   HBTEST ThreadStatic()                              IS "1 5"

   /* QUIT ends the thread, not the program, running ALWAYS on the way */
   HBTEST ThreadQuit()                                IS "always 1"

   /* hb_threadQuitRequest() ends a thread that is waiting */
   HBTEST QuitRequest()                               IS 1

   /* Mutexes: taken again by the thread holding it; a timed lock gives up;
      notifications queue, oldest first */
   HBTEST ValType( hb_mutexCreate() )                 IS "P"
   HBTEST MutexRecursive()                            IS ".T. .T. .T. .T. .F."
   HBTEST MutexTimeout()                              IS .F.
   HBTEST NotifyOrder()                               IS "a b c"
   HBTEST hb_mutexSubscribe( hb_mutexCreate(), 0.05 ) IS .F.
   HBTEST NotifyNil()                                 IS "T U"

   RETURN

STATIC FUNCTION ThreadNumbers()

   LOCAL pMtx := hb_mutexCreate()
   LOCAL pThread1, pThread2
   LOCAL nSeen1 := 0, nSeen2 := 0

   pThread1 := hb_threadStart( @ReportID(), pMtx )
   hb_mutexSubscribe( pMtx, 5, @nSeen1 )
   pThread2 := hb_threadStart( @ReportID(), pMtx )
   hb_mutexSubscribe( pMtx, 5, @nSeen2 )
   hb_threadWait( { pThread1, pThread2 }, 5, .T. )

   RETURN hb_ntos( hb_threadID( pThread1 ) ) + " " + hb_ntos( nSeen1 ) + " " + ;
          hb_ntos( hb_threadID( pThread2 ) ) + " " + hb_ntos( nSeen2 )

STATIC PROCEDURE ReportID( pMtx )

   hb_mutexNotify( pMtx, hb_threadID() )

   RETURN

STATIC FUNCTION WaitOne()

   LOCAL pThread := hb_threadStart( {|| hb_idleSleep( 0.01 ) } )

   RETURN hb_threadWait( pThread, 5 )

STATIC FUNCTION WaitTimeout()

   LOCAL pThread := hb_threadStart( {|| hb_idleSleep( 0.5 ) } )
   LOCAL nGot := hb_threadWait( pThread, 0.01 )

   hb_threadWait( pThread, 5 )

   RETURN nGot

STATIC FUNCTION WaitAll()

   LOCAL aThreads := { hb_threadStart( {|| hb_idleSleep( 0.02 ) } ), ;
                       hb_threadStart( {|| hb_idleSleep( 0.05 ) } ) }

   RETURN hb_threadWait( aThreads, 5, .T. )

STATIC FUNCTION SetsInherited()

   LOCAL pMtx := hb_mutexCreate()
   LOCAL lExactOld := Set( _SET_EXACT, .T. )
   LOCAL cSeen := ""
   LOCAL cResult

   hb_threadWait( hb_threadStart( @SetsInThread(), pMtx ), 5 )
   hb_mutexSubscribe( pMtx, 5, @cSeen )
   cResult := cSeen + " " + hb_ntos( Set( _SET_DECIMALS ) )
   Set( _SET_EXACT, lExactOld )

   RETURN cResult

STATIC PROCEDURE SetsInThread( pMtx )

   Set( _SET_DECIMALS, 4 )
   hb_mutexNotify( pMtx, iif( Set( _SET_EXACT ), ".T.", ".F." ) + " " + ;
                         hb_ntos( Set( _SET_DECIMALS ) ) )

   RETURN

STATIC FUNCTION ThreadStatic()

   LOCAL pMtx := hb_mutexCreate()
   LOCAL nSeen := -1

   snCount := 5
   hb_threadWait( hb_threadStart( @BumpCount(), pMtx ), 5 )
   hb_mutexSubscribe( pMtx, 5, @nSeen )

   RETURN hb_ntos( nSeen ) + " " + hb_ntos( snCount )

STATIC PROCEDURE BumpCount( pMtx )

   snCount++
   hb_mutexNotify( pMtx, snCount )

   RETURN

STATIC FUNCTION ThreadQuit()

   LOCAL pMtx := hb_mutexCreate()
   LOCAL cSeen := ""
   LOCAL nDone := hb_threadWait( hb_threadStart( @QuitInThread(), pMtx ), 5 )

   hb_mutexSubscribe( pMtx, 1, @cSeen )

   RETURN cSeen + " " + hb_ntos( nDone )

STATIC PROCEDURE QuitInThread( pMtx )

   BEGIN SEQUENCE
      QUIT
   ALWAYS
      hb_mutexNotify( pMtx, "always" )
   END SEQUENCE
   hb_mutexNotify( pMtx, "after" )

   RETURN

STATIC FUNCTION QuitRequest()

   LOCAL pThread := hb_threadStart( @SleepAlways() )

   hb_idleSleep( 0.05 )
   hb_threadQuitRequest( pThread )

   RETURN hb_threadWait( pThread, 5 )

STATIC PROCEDURE SleepAlways()

   LOCAL nNaps := 0

   DO WHILE nNaps >= 0
      hb_idleSleep( 0.02 )
      nNaps++
   ENDDO

   RETURN

STATIC FUNCTION MutexRecursive()

   LOCAL pMtx := hb_mutexCreate()
   LOCAL cResult := LStr( hb_mutexLock( pMtx ) )

   cResult += " " + LStr( hb_mutexLock( pMtx ) )
   cResult += " " + LStr( hb_mutexUnlock( pMtx ) )
   cResult += " " + LStr( hb_mutexUnlock( pMtx ) )
   cResult += " " + LStr( hb_mutexUnlock( pMtx ) )

   RETURN cResult

STATIC FUNCTION LStr( lValue )

   RETURN iif( lValue, ".T.", ".F." )

STATIC FUNCTION MutexTimeout()

   LOCAL pMtx := hb_mutexCreate()
   LOCAL pHeld := hb_mutexCreate()
   LOCAL pRelease := hb_mutexCreate()
   LOCAL pThread := hb_threadStart( @HoldLock(), pMtx, pHeld, pRelease )
   LOCAL lGot

   hb_mutexSubscribe( pHeld, 5 )
   lGot := hb_mutexLock( pMtx, 0.05 )
   hb_mutexNotify( pRelease )
   hb_threadWait( pThread, 5 )

   RETURN lGot

STATIC PROCEDURE HoldLock( pMtx, pHeld, pRelease )

   hb_mutexLock( pMtx )
   hb_mutexNotify( pHeld )
   hb_mutexSubscribe( pRelease, 5 )
   hb_mutexUnlock( pMtx )

   RETURN

STATIC FUNCTION NotifyOrder()

   LOCAL pMtx := hb_mutexCreate()
   LOCAL cA := "", cB := "", cC := ""

   hb_mutexNotify( pMtx, "a" )
   hb_mutexNotify( pMtx, "b" )
   hb_mutexNotify( pMtx, "c" )
   hb_mutexSubscribe( pMtx, 1, @cA )
   hb_mutexSubscribe( pMtx, 1, @cB )
   hb_mutexSubscribe( pMtx, 1, @cC )

   RETURN cA + " " + cB + " " + cC

STATIC FUNCTION NotifyNil()

   LOCAL pMtx := hb_mutexCreate()
   LOCAL xGot := "unset"
   LOCAL lGot

   hb_mutexNotify( pMtx )
   lGot := hb_mutexSubscribe( pMtx, 1, @xGot )

   RETURN iif( lGot, "T", "F" ) + " " + ValType( xGot )
