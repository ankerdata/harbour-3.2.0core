#include "astype.ch"
// Test 119: an IIF is the type its two branches share.
//
// easiutil/settflag.prg and shared/buffflag.prg set and clear a bit in 100
// one-line functions, RETURN IIF( lx, hb_bitOr( n, BIT ), hb_bitAnd( n,
// hb_bitNot( BIT ) ) ). Both branches are numbers, but the return type never
// looked inside the IIF and every one returned dynamic. Pinned: two numbers
// (a decimal return), two strings, a number beside NIL (still dynamic, NIL
// says nothing), and a caller that reads the typed return.

#define FLAG119_BIT  4

PROCEDURE Main()

   LOCAL nFlags := 1 AS NUMERIC

   nFlags := SetBit119(nFlags, .T.)
   QOut("set:", nFlags, HasBit119(nFlags))
   nFlags := SetBit119(nFlags, .F.)
   QOut("cleared:", nFlags, HasBit119(nFlags))
   QOut("word:", Word119(.T.), Word119(.F.))
   QOut("maybe:", Maybe119(.T.), Maybe119(.F.) == NIL)
   QOut("sum:", SetBit119(0, .T.) + Half119(.T.) + Half119(.F.))

RETURN

FUNCTION SetBit119( nFlags AS NUMERIC, lx AS LOGICAL ) AS NUMERIC
RETURN IIF(lx, hb_bitOr(nFlags, FLAG119_BIT), hb_bitAnd(nFlags, hb_bitNot(FLAG119_BIT)))

FUNCTION HasBit119( nFlags AS NUMERIC ) AS LOGICAL
RETURN hb_bitAnd(nFlags, FLAG119_BIT) != 0

FUNCTION Word119( lx AS LOGICAL ) AS STRING
RETURN IIF(lx, "on", "off")

FUNCTION Maybe119( lx AS LOGICAL )
RETURN IIF(lx, 1, NIL)

   // a long branch beside a decimal one widens to decimal
FUNCTION Half119( lx AS LOGICAL ) AS NUMERIC
RETURN IIF(lx, hb_bitShift(8, -1), 0.5)
