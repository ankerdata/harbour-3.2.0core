// Test 183: ordering two values the program leaves untyped.
// `<`, `<=`, `>` and `>=` between two `x` values, an element of a plain
// array, a function that answers any type or a block's result may meet
// two strings when the line runs, which C# cannot order with an operator
// through dynamic. They go through HbRuntime.Cmp(): strings by the
// program's collation, logicals .F. before .T., numbers and dates as the
// operator does. A typed operand keeps the operator.

#include "hbclass.ch"

CLASS Scope183
   VAR xTop
ENDCLASS

PROCEDURE Main()

   LOCAL aMixed183 := { "pear", "Apple", "fig" }
   LOCAL aNumbers183 := { 30, 4, 17 }
   LOCAL bPick183 := { |xValue| xValue }
   LOCAL oScope183 := Scope183():New()

   // two untyped parameters: strings, numbers, dates, logicals
   ? Lower183( "pear", "fig" ), Lower183( "fig", "pear" )
   ? Lower183( 3, 20 ), Lower183( 2.5, 2.25 )
   ? Lower183( Date(), Date() + 1 ) == Date()
   ? Lower183( .T., .F. ), Lower183( .F., .T. )

   // an element of a plain array against another
   ? aMixed183[ 1 ] > aMixed183[ 2 ], aMixed183[ 3 ] <= aMixed183[ 1 ]
   ? aNumbers183[ 1 ] >= aNumbers183[ 2 ]

   // a sort block with untyped parameters, over strings and over numbers
   ASort( aMixed183, , , { |xLeft, xRight| xLeft < xRight } )
   ? aMixed183[ 1 ], aMixed183[ 2 ], aMixed183[ 3 ]
   ASort( aNumbers183, , , { |xLeft, xRight| xLeft > xRight } )
   ? aNumbers183[ 1 ], aNumbers183[ 2 ], aNumbers183[ 3 ]

   // a block's result against an `x` member, as a scope is tested
   oScope183:xTop := "b"
   ? Eval( bPick183, "c" ) >= oScope183:xTop, Eval( bPick183, "a" ) >= oScope183:xTop
   oScope183:xTop := 10
   ? Eval( bPick183, 12 ) >= oScope183:xTop, Eval( bPick183, 9 ) >= oScope183:xTop

   // a function that answers any type
   ? AnyOf183( 1 ) < AnyOf183( 2 ), AnyOf183( 3 ) < AnyOf183( 4 )

   RETURN

FUNCTION Lower183( xLeft, xRight )

   RETURN iif( xLeft <= xRight, xLeft, xRight )

FUNCTION AnyOf183( nWhich )

   IF nWhich == 1
      RETURN "alpha"
   ELSEIF nWhich == 2
      RETURN "beta"
   ELSEIF nWhich == 3
      RETURN 7
   ENDIF

   RETURN 5
