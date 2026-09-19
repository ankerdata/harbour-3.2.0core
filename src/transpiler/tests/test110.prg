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
   VAR nPunches INIT 0
   VAR nDayTotal INIT 5
   METHOD Punch()
ENDCLASS

METHOD Punch() CLASS BlockTally
   ::nPunches++
RETURN Self

PROCEDURE Main()

   LOCAL aSeen := {}
   LOCAL nSum := 0
   LOCAL cTrail := ""
   LOCAL aNums := { 3, 1, 2 }
   LOCAL nCalls := 0
   LOCAL bPair := {| x | AAdd( aSeen, x ), x * 10 }
   LOCAL bThree := {|| nSum += 1, nSum += 10, nSum }
   LOCAL oTally := BlockTally():New()

   ? Eval( bPair, 4 )
   ? Len( aSeen )
   ? Eval( bThree )

   AEval( aNums, {| n | nSum += n, cTrail += Str( n, 1 ) } )
   ? nSum
   ? cTrail

   ASort( aNums, , , {| a, b | nCalls++, a < b } )
   ? aNums[ 1 ], aNums[ 2 ], aNums[ 3 ]
   ? nCalls > 0

   // a value that is only discarded; a value with no effect at all
   // (`nSum`, `nSum > 0`) is Harbour's own W0027, which -es2 rejects
   ? Eval( {|| nSum > 0 .AND. BlockSay( "and" ), "kept" } )

   // an IIF in the middle, a procedure, a member assigned through a send
   AEval( aNums, {| n | iif( n > 1, AAdd( aSeen, n ), NIL ), BlockNote( "n" ), n } )
   ? Len( aSeen )
   Eval( {|| oTally:Punch(), oTally:nDayTotal := 0 } )
   ? oTally:nPunches, oTally:nDayTotal

   // a FOR-style condition: the block's value is its last expression
   ? BlockCount( aNums, {| n | nCalls := 0, n >= 2 } )
   ? nCalls

   // a `...` block runs every expression too
   Eval( {| ... | nSum += 100, BlockNote( "v" ) }, 1, 2 )
   ? nSum

RETURN

PROCEDURE BlockNote( cWhat )
   ? "note " + cWhat
RETURN

FUNCTION BlockSay( cWhat )
   ? "say " + cWhat
RETURN .T.

FUNCTION BlockCount( aItems, bFor )

   LOCAL nHits := 0
   LOCAL nItem

   FOR EACH nItem IN aItems
      IF Eval( bFor, nItem )
         nHits++
      ENDIF
   NEXT

RETURN nHits
