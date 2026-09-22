#include "astype.ch"
// Test 115: threads (family 10) — what the emitter does for them.
//
// THREAD STATIC was emitted as a plain static, one for every thread; it
// is [ThreadStatic] now. @Func() of a STATIC function named it unmangled,
// so HbRuntime.FuncPtr found nothing; it names <FileBase>_<Func> now, as a
// call does. A codeblock around a function Harbour returns NIL from —
// {|| hb_idleSleep( n ) } — was an expression lambda of a void call;
// HbRuntime's procedures return null now. A QUIT on a thread unwinds that
// thread alone (HbQuit), and a BEGIN SEQUENCE WITH does not take it.

STATIC snSeen := 0 AS NUMERIC

PROCEDURE Main()

   LOCAL pMtx := hb_mutexCreate() AS BLOCK
   LOCAL xGot AS USUAL
   LOCAL pThread AS BLOCK

   // each thread its own THREAD STATIC, starting from its declared value
   xGot := "none"
   snSeen := 7
   pThread := hb_threadStart(@Thr115Bump(), pMtx)
   hb_threadWait(pThread, 5)
   hb_mutexSubscribe(pMtx, 5, @xGot)
   QOut("thread static:", xGot, snSeen)

   // a codeblock around a call Harbour returns NIL from
   pThread := hb_threadStart({|| hb_idleSleep(0.01)})
   QOut("block thread:", hb_threadWait(pThread, 5))

   // QUIT in a thread, inside BEGIN SEQUENCE WITH: the thread ends there,
   // the sequence does not take it, the program goes on
   xGot := "none"
   pThread := hb_threadStart(@Thr115Quit(), pMtx)
   QOut("quit thread:", hb_threadWait(pThread, 5))
   hb_mutexSubscribe(pMtx, 0.2, @xGot)
   QOut("after quit:", xGot)
   QOut("main goes on")

RETURN

STATIC PROCEDURE Thr115Bump( pMtx AS BLOCK )

   snSeen++
   hb_mutexNotify(pMtx, snSeen)

RETURN

STATIC PROCEDURE Thr115Quit( pMtx AS BLOCK )

   BEGIN SEQUENCE WITH {|oErr| Break( oErr ) }
      __Quit()
   RECOVER
      hb_mutexNotify(pMtx, "recovered")
   END SEQUENCE

   hb_mutexNotify(pMtx, "went on")

RETURN
