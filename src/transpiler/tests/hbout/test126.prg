#include "astype.ch"
// Test 126: a SWITCH runs on from the matching CASE until an EXIT.
//
// Harbour's SWITCH falls through: `CASE "C"; CASE "M"; x := ""; EXIT` is one
// body for both labels, and a body without EXIT runs on into the next
// CASE's, and from the last CASE into OTHERWISE. The C# ended every CASE
// with `break;`, so a label with no statements of its own did nothing:
// easipos's GetDefaultFromType() (sqlthelpers.prg) answered NIL for "C",
// "M" and "N", and every INSERT the ORM built from those defaults failed.
//
// C# falls through only between labels with nothing between them: an empty
// CASE stacks its label on the next, and a body that runs on says so with
// `goto case` / `goto default`.

// labels stacked, a comment between them, as in GetDefaultFromType()
STATIC FUNCTION Kind126( cType AS STRING ) AS USUAL
   LOCAL xValue AS USUAL

   SWITCH cType
      CASE "C"
      CASE "M"
         // a comment of its own
      CASE "B"
         xValue := "text"
         EXIT
      CASE "N"
      CASE "I"
         xValue := 0
         EXIT
      CASE "L"
         xValue := .F.
         EXIT
   ENDSWITCH

RETURN xValue

   // a body without EXIT runs on into the next body, and into OTHERWISE
STATIC FUNCTION Grade126( nScore AS NUMERIC ) AS STRING
   LOCAL cTrail := "" AS STRING

   SWITCH nScore
      CASE 3
         cTrail += "three "
      CASE 2
         cTrail += "two "
         EXIT
      CASE 1
         cTrail += "one "
      OTHERWISE
         cTrail += "other"
   ENDSWITCH

RETURN cTrail

   // a dynamic value switched on; the last CASE, empty, falls out
STATIC FUNCTION Tail126( xKey AS USUAL ) AS STRING
   LOCAL cSeen := "-" AS STRING

   SWITCH xKey
      CASE "a"
         cSeen := "A"
      CASE "b"
         cSeen += "B"
         EXIT
      CASE "z"
   ENDSWITCH
RETURN cSeen

PROCEDURE Main()

   QOut(Kind126("C"), Kind126("M"), Kind126("B"))
   QOut(Kind126("N"), Kind126("I"), Kind126("L"), Kind126("D") == NIL)
   QOut(Grade126(3) + "|" + Grade126(2) + "|" + Grade126(1) + "|" + Grade126(9))
   QOut(Tail126("a"), Tail126("b"), Tail126("z"), Tail126(7))

RETURN
