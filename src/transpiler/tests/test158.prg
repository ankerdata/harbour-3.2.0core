// Test 158: an error in a thread keeps its traceback.
//
// hb_threadStart() calls the thread's function through FuncPtr(), and a
// block runs through InvokeBlock(); both make the call through a compiled
// delegate, so the error block, which runs before the stack unwinds, sees
// every frame between it and the thread's start: the routine that failed
// and the one that called it, as Harbour's does. ProcName() gives
// Harbour's upper-case names and C#'s declared ones, so they are printed
// upper-cased.

PROCEDURE Main()

   LOCAL pThread

   pThread := hb_threadStart( @Thr158Run() )
   hb_threadWait( pThread, 5 )
   ? "main goes on"

   RETURN

PROCEDURE Thr158Run()

   ErrorBlock( {| oErr | Thr158Report( oErr ) } )
   Thr158Fail()

   RETURN

PROCEDURE Thr158Fail()

   LOCAL nZero := 0
   LOCAL nResult := 10 / nZero

   ? nResult

   RETURN

FUNCTION Thr158Report( oErr )

   LOCAL n := 1

   ? "error:", oErr:subCode
   DO WHILE ! Empty( ProcName( n ) )
      ? "called from", Upper( ProcName( n ) )
      n++
   ENDDO
   QUIT

   RETURN NIL
