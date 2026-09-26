// Test 120: an `i` name is a whole number, C# long.
//
// The Hungarian `n` is a Harbour number, C# decimal. An `i` name says the
// number is whole - a count, an index, a flag's integer value - and the
// transpiler takes it at its word, as it takes `AS INTEGER`: the local, the
// static and the parameter are long, a numeric initializer does not make
// them decimal again, and a decimal written into one is cast, as for a
// Pass 2.5 local. A value that may hold a fraction (a division, a power, a
// literal with decimals) is W0024 instead, so it is not in this test.
// Pinned: initializers, assignment from a decimal, the compound family, a
// FOR counter, an `i` parameter called with a number, an `i` value passed
// to an `n` parameter, a subscript, and a function returning an `i` local.

STATIC siCalls120 := 0

PROCEDURE Main()

   LOCAL aItems := { "a", "b", "c", "d" }
   LOCAL iCount := 0
   LOCAL iLen := Len( aItems )
   LOCAL nPrice := 2.5
   LOCAL iPos, iWhole

   FOR iPos := 1 TO iLen
      iCount += iPos
   NEXT
   ? "count:", iCount, iLen

   iWhole := Int( nPrice * 1.2 )
   ? "whole:", iWhole, aItems[ iWhole ]

   iWhole := Round( nPrice, 0 ) + iLen
   ? "rounded:", iWhole

   ? "twice:", Twice120( iLen ), Twice120( 7 ), Twice120( Len( "xyz" ) )
   ? "priced:", Price120( iCount, nPrice )
   ? "calls:", siCalls120

   RETURN

FUNCTION Twice120( iValue )
   LOCAL iResult := iValue * 2
   siCalls120++
   RETURN iResult

FUNCTION Price120( nQty, nPrice )
   RETURN nQty * nPrice
