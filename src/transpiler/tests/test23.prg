// Test 23: DEFAULT command — Harbour's idiomatic shorthand for
// optional parameters. Defined in common.ch as an #xcommand:
//
//   #xcommand DEFAULT <v1> TO <x1> [, <vn> TO <xn> ] => ;
//                       IF <v1> == NIL ; <v1> := <x1> ; END ;
//                       [; IF <vn> == NIL ; <vn> := <xn> ; END ]
//
// The expansion produces inline IF/END blocks with `;` statement
// separators. Stock Harbour accepts this; the transpiler used to
// reject it with "syntax error at <varname>" because the parser's
// AST-build mode mishandled the inline statement form.
//
// This is one of the most common Harbour idioms — every PROCEDURE
// with optional parameters tends to use it — so a regression here
// would block transpiling almost any real-world codebase.
#include "common.ch"

PROCEDURE Main()
   Greet()
   Greet( "Alice" )
   Greet( "Bob", .F. )
RETURN

PROCEDURE Greet( cName, lFormal )
   DEFAULT cName TO "world"
   DEFAULT lFormal TO .T.
   IF lFormal
      ? "Good day, " + cName
   ELSE
      ? "Hi " + cName
   ENDIF
RETURN
