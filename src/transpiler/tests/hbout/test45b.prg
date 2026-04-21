#include "astype.ch"
// Test 45b: Main that calls LoadFlags (from test45a.prg) at both
// arities. See test45a.prg for the short-overload rationale.

PROCEDURE Main()

   LOCAL aArr := {} AS ARRAY

   LoadFlags("full.dat", @aArr, 4)
   QOut("full[1]=" + aArr[1])
   QOut("full[4]=" + aArr[4])

   LoadFlags("short.dat")
   QOut("short returned")

RETURN
