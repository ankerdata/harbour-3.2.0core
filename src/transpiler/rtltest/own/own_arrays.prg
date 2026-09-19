/*
 * Our tests for the array functions hbtest leaves uncovered or nearly
 * so: hb_ADel and hb_AIns, which EasiPOS calls 33 and 34 times, always
 * with lAutoSize, to delete and insert sale-buffer lines; hb_AScan (6,
 * looking numbers up); and AEval's start and count, AAdd's value,
 * AClone's depth, ATail. Written as hbtest writes its assertions;
 * Harbour runs them first, so every expected value here is Harbour's own.
 */

#include "rt_main.ch"

PROCEDURE Main_ARRAYS()

   /* hb_ADel: delete, then shrink when lAutoSize */
   HBTEST hb_ValToExp( DelOf( { 1, 2, 3 }, 2, .T. ) )      IS "{1, 3}"
   HBTEST hb_ValToExp( DelOf( { 1, 2, 3 }, 2, .F. ) )      IS "{1, 3, NIL}"
   HBTEST hb_ValToExp( DelOf( { 1, 2, 3 }, 1, .T. ) )      IS "{2, 3}"
   HBTEST hb_ValToExp( DelOf( { 1, 2, 3 }, 3, .T. ) )      IS "{1, 2}"
   HBTEST hb_ValToExp( DelOf( { 1, 2, 3 }, 0, .T. ) )      IS "{2, 3}"
   HBTEST hb_ValToExp( DelOf( { 1, 2, 3 }, 4, .T. ) )      IS "{1, 2, 3}"
   HBTEST hb_ValToExp( DelOf( { 1 }, 1, .T. ) )            IS "{}"
   HBTEST hb_ValToExp( DelOf( {}, 1, .T. ) )               IS "{}"

   /* hb_AIns: grow when lAutoSize, then insert and set */
   HBTEST hb_ValToExp( InsOf( { 1, 2, 3 }, 2, 9, .T. ) )   IS "{1, 9, 2, 3}"
   HBTEST hb_ValToExp( InsOf( { 1, 2, 3 }, 2, 9, .F. ) )   IS "{1, 9, 2}"
   HBTEST hb_ValToExp( InsOf( { 1, 2, 3 }, 4, 9, .T. ) )   IS "{1, 2, 3, 9}"
   HBTEST hb_ValToExp( InsOf( { 1, 2, 3 }, 5, 9, .T. ) )   IS "{1, 2, 3}"
   HBTEST hb_ValToExp( InsOf( { 1, 2, 3 }, 1, NIL, .T. ) ) IS "{NIL, 1, 2, 3}"
   HBTEST hb_ValToExp( InsOf( { 1, 2, 3 }, 0, 9, .T. ) )   IS "{9, 1, 2, 3}"
   HBTEST hb_ValToExp( InsOf( {}, 1, 9, .T. ) )            IS "{9}"
   HBTEST hb_ValToExp( InsOf( {}, 1, 9, .F. ) )            IS "{}"

   /* the array they return (compared by length) — in C# the form an
      argument takes that is not a variable or a member: the returned
      array is resized; NIL for anything but an array */
   HBTEST hb_ADel( { 1, 2, 3 }, 2, .T. )                   IS "{.[2].}"
   HBTEST hb_ADel( { 1, 2, 3 }, 2 )                        IS "{.[3].}"
   HBTEST hb_AIns( { 1, 2, 3 }, 2, 9, .T. )                IS "{.[4].}"
   HBTEST hb_AIns( { 1, 2, 3 }, 2, 9 )                     IS "{.[3].}"
   HBTEST hb_ADel( "abc", 1 )                              IS NIL
   HBTEST hb_AIns( 12, 1 )                                 IS NIL

   /* hb_AScan: EasiPOS looks numbers up; a string compares exactly */
   HBTEST hb_AScan( { 10, 20, 30 }, 20 )                   IS 2
   HBTEST hb_AScan( { 10, 20, 30 }, 40 )                   IS 0
   HBTEST hb_AScan( { 10, 20, 30, 20 }, 20, 3 )            IS 4
   HBTEST hb_AScan( { 10, 20, 30, 20 }, 20, 3, 1 )         IS 0
   HBTEST hb_AScan( { "ab", "abc" }, "abc", , , .T. )      IS 2
   HBTEST hb_AScan( { 1, 2, 3 }, {| n | n > 1 } )          IS 2
   HBTEST hb_AScan( {}, 1 )                                IS 0

   /* AEval's start and count, AAdd's value, AClone's depth, ATail */
   HBTEST EvalSum( { 1, 2, 3, 4 }, 2, 2 )                  IS 5
   HBTEST EvalSum( { 1, 2, 3, 4 }, 3 )                     IS 7
   HBTEST AAdd( {}, "x" )                                  IS "x"
   HBTEST CloneDeep()                                      IS "1 2"
   HBTEST ATail( {} )                                      IS NIL
   HBTEST ATail( { 1, 2 } )                                IS 2

   RETURN

STATIC FUNCTION DelOf( aList, nPos, lAutoSize )

   hb_ADel( aList, nPos, lAutoSize )

   RETURN aList

STATIC FUNCTION InsOf( aList, nPos, xValue, lAutoSize )

   hb_AIns( aList, nPos, xValue, lAutoSize )

   RETURN aList

/* xCount, not nCount: an omitted count must reach AEval as NIL (to the
   end), and an n name is numeric — omitted, it is 0 in C# */
STATIC FUNCTION EvalSum( aList, nStart, xCount )

   LOCAL nSum := 0

   AEval( aList, {| n | nSum += n }, nStart, xCount )

   RETURN nSum

STATIC FUNCTION CloneDeep()

   LOCAL aOrig := { { 1 } }
   LOCAL aCopy := AClone( aOrig )

   aCopy[ 1 ][ 1 ] := 2

   RETURN Str( aOrig[ 1 ][ 1 ], 1 ) + " " + Str( aCopy[ 1 ][ 1 ], 1 )
