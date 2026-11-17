// The audit's INT-CONFLICT: a local used as a subscript that an assignment
// keeps a decimal. A line marked `row` must give a row, and no other line
// may: a local set from another that earns its whole number, a SWITCH
// subject with whole labels, a name its prefix makes whole and a local
// that is a long already are none.

#define KIND46_ONE 1
#define KIND46_TWO 2

FUNCTION Conflict46( aRows, nGiven, iGiven )

   LOCAL nPos, iPos, i, j, k

   nPos := nGiven                                   // row: a decimal parameter
   ? aRows[ nPos ]
   iPos := nGiven                                   // quiet: an `i` name is whole whatever it is given
   ? aRows[ iPos ]

   FOR i := 1 TO Len( aRows )
      j := i + 1                                    // quiet: whole once i is
      ? aRows[ i ], aRows[ j ]
   NEXT

   FOR k := 1 TO KIND46_TWO
      SWITCH k                                      // quiet: every label is whole
      CASE KIND46_ONE
         ? aRows[ k ]
         EXIT
      CASE KIND46_TWO
         ? aRows[ k ], iGiven
         EXIT
      ENDSWITCH
   NEXT

RETURN NIL
