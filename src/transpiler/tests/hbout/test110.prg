#include "astype.ch"
// Test 110: a codeblock with several expressions runs every one of them.
//
// `{|| a, b, c }` evaluates a, b and c in order and yields c. The
// block's body is a chain of expressions, and the emitter wrote only
// the first: the C# of EasiPOS's Z reset
// `{ || oFixedTtl:ReadAll(), oFixedTtl:nDayValue := 0, ... }` read the
// row and never zeroed it (xzauto.prg, 14 tables), a Locate condition
// was `ReadAll()`'s result, and AADDs never ran. A block with more
// than one expression is now a statement lambda: each expression but
// the last is a statement — a call, an assignment or a step as it
// stands, a value with no effect dropped, an IIF as if/else, any other
// value discarded — and the last is returned. A `...` block runs them
// all before its `return null`. -GT writes the whole list back.
#include "hbclass.ch"

CLASS BlockTally

   DATA nPunches AS NUMERIC INIT 0
   DATA nDayTotal AS NUMERIC INIT 5
   METHOD Punch()

ENDCLASS

METHOD Punch() AS OBJECT CLASS BlockTally
   ::nPunches++
RETURN Self

PROCEDURE Main()

   LOCAL aSeen := {} AS ARRAY
   LOCAL nSum := 0 AS NUMERIC
   LOCAL cTrail := "" AS STRING
   LOCAL aNums := {3, 1, 2} AS ARRAY
   LOCAL nCalls := 0 AS NUMERIC
   LOCAL bPair := {|x| AAdd(aSeen, x), x * 10} AS BLOCK
   LOCAL bThree := {|| nSum += 1, nSum += 10, nSum} AS BLOCK
   LOCAL oTally := BlockTally():New() AS OBJECT

   QOut(Eval(bPair, 4))
   QOut(Len(aSeen))
   QOut(Eval(bThree))

   AEval(aNums, {|n| nSum += n, cTrail += Str(n, 1)})
   QOut(nSum)
   QOut(cTrail)

   ASort(aNums, , , {|a, b| nCalls++, a < b})
   QOut(aNums[1], aNums[2], aNums[3])
   QOut(nCalls > 0)

   // a value that is only discarded; a value with no effect at all
   // (`nSum`, `nSum > 0`) is Harbour's own W0027, which -es2 rejects
   QOut(Eval({|| nSum > 0 .AND. BlockSay("and"), "kept"}))

   // an IIF in the middle, a procedure, a member assigned through a send
   AEval(aNums, {|n| IIF(n > 1, AAdd(aSeen, n), NIL), BlockNote("n"), n})
   QOut(Len(aSeen))
   Eval({|| oTally:Punch(), oTally:nDayTotal := 0})
   QOut(oTally:nPunches, oTally:nDayTotal)

   // a FOR-style condition: the block's value is its last expression
   QOut(BlockCount(aNums, {|n| nCalls := 0, n >= 2}))
   QOut(nCalls)

   // a `...` block runs every expression too
   Eval({|...| nSum += 100, BlockNote("v")}, 1, 2)
   QOut(nSum)

RETURN

PROCEDURE BlockNote( cWhat AS STRING )
   QOut("note " + cWhat)
RETURN

FUNCTION BlockSay( cWhat AS STRING ) AS LOGICAL
   QOut("say " + cWhat)
RETURN .T.

FUNCTION BlockCount( aItems AS ARRAY, bFor AS BLOCK ) AS NUMERIC

   LOCAL nHits := 0 AS NUMERIC
   LOCAL nItem AS NUMERIC

   FOR EACH nItem IN aItems
      IF Eval(bFor, nItem)
         nHits++
      ENDIF
   NEXT

RETURN nHits
