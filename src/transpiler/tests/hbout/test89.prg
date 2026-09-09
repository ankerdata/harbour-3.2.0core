#include "astype.ch"
// Test 89: a function-final RETURN no path reaches, and SWITCH cases
// that already end in a jump.
//
// Harbour requires a FUNCTION to end with RETURN (level-1 warning, and
// the corpus builds with -w3 -es2) even when the statement before it
// never completes: IF/ELSE with every arm returning, DO CASE with an
// OTHERWISE that returns, DO WHILE .T. with no EXIT. C# reports CS0162
// on that RETURN. The emitter skips exactly that one statement, and
// nothing else — FirstOver below keeps its final RETURN because its
// loop has an EXIT.
//
// In a SWITCH, a comment preserved after a case's `return` or `exit`
// used to hide the jump from the emitter, which then added a second
// `break;` — CS0162 again. Comments no longer count.

PROCEDURE Main()
   QOut("sign=" + SignOf(5) + SignOf(-5) + SignOf(0))
   QOut("kind=" + Kind(1) + Kind(2) + Kind(9))
   QOut("count=" + LTrim(Str(CountTo(3))))
   QOut("first=" + LTrim(Str(FirstOver({1, 5, 9}, 4))))
   QOut("none=" + LTrim(Str(FirstOver({1, 2}, 4))))
   QOut("name=" + Ordinal(1) + Ordinal(2) + Ordinal(3))
RETURN

   /* Every arm returns: the final RETURN is not emitted. */
FUNCTION SignOf( n AS NUMERIC ) AS STRING
   IF n > 0
   RETURN "+"
   ELSEIF n < 0
   RETURN "-"
   ELSE
   RETURN "0"
   ENDIF

RETURN "?"

   /* DO CASE with OTHERWISE, every case returning. */
FUNCTION Kind( n AS NUMERIC ) AS STRING
   DO CASE
      CASE n == 1
      RETURN "one"
      CASE n == 2
      RETURN "two"
      OTHERWISE
      RETURN "many"
   ENDCASE

RETURN ""

   /* DO WHILE .T. with no EXIT: only RETURN leaves it. */
FUNCTION CountTo( nMax AS NUMERIC ) AS NUMERIC
   LOCAL n := 0 AS NUMERIC
   DO WHILE .T.
      n++
      IF n >= nMax
      RETURN n
      ENDIF
   ENDDO

RETURN -1

   /* Control: a DO WHILE .T. WITH an EXIT falls through, so the final
   RETURN stays — and the second call above returns through it. */
FUNCTION FirstOver( aList AS ARRAY, nOver AS NUMERIC ) AS USUAL
   LOCAL i := 0 AS NUMERIC
   DO WHILE .T.
      i++
      IF i > Len(aList)
         EXIT
      ENDIF

      IF aList[i] > nOver
      RETURN aList[i]
      ENDIF
   ENDDO

RETURN 0

   /* SWITCH: `return` followed by a trailing comment, and `exit` followed
   by a commented-out case — neither gets a second break. */
FUNCTION Ordinal( n AS NUMERIC ) AS STRING
   LOCAL cName := "?" AS STRING

   SWITCH n
      CASE 1
         // the first
      RETURN "one"
      CASE 2
         cName := "two"
         EXIT
         //CASE 3 ; cName := "three" ; EXIT
      OTHERWISE
         cName := "many"
   ENDSWITCH

RETURN cName
