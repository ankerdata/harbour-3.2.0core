// Test 38: IIF as statement, with empty branches.
//
// Real-prg idiom from easiposx/easikds.prg:
//     iif(cond, sideEffect(), )
// Harbour accepts an empty 3rd arg (NIL) and uses iif purely for its
// side effect. Before the fix, the emitter produced `(a ? b : );`
// which C# rejects on two grounds: empty false-branch, and ternary
// used as statement (CS0201). Fix expands statement-position IIF to
// an if/else and substitutes default for empty branches.

PROCEDURE Main()
   LOCAL n := 3

   // Fires side effect when cond true, nothing else.
   iif( n > 0, QOut( "pos3" ), )

   // Empty when cond false — false-branch default case.
   iif( n > 10, QOut( "big" ), )

   // Empty true-branch — exercises both.
   iif( n < 0, , QOut( "nonneg" ) )

   // Classic both-branches form — should still work.
   iif( n > 0, QOut( "yes" ), QOut( "no" ) )

RETURN
