#include "astype.ch"
// Test 78: INTEGER inference (Pass 2.5 int candidacy).
//
// decimal stays the default for Harbour numerics, but a variable whose
// numeric life is purely index-shaped — array subscripts, FOR loop
// variables, integral initializers/arithmetic (literals, Len(), other
// INTEGER vars) — emits as C# `int`: subscripts drop the (int) cast,
// FOR loops declare int, and int widens to decimal implicitly at every
// consumer boundary. Division is the hard disqualifier (Harbour 5/2 is
// 2.5, C# int/int truncates): a division-fed variable stays decimal
// and, when used as an index, earns W0026 pointing at the division so
// the source can decide (wrap with int() or keep decimal). W0026 is
// expected on nHalf below.

PROCEDURE Main()

   LOCAL aItems := {"alpha", "beta", "gamma", "delta"} AS ARRAY
   LOCAL i AS NUMERIC
   LOCAL nIdx := 1 AS NUMERIC
   LOCAL nLast := Len(aItems) AS NUMERIC
   // division → stays decimal, W0026
   LOCAL nHalf := 4 / 2 AS NUMERIC

   FOR i := 1 TO Len(aItems)
      QOut("i=", aItems[i])
   NEXT

   // integral arithmetic keeps int
   nIdx := nIdx + 2
   QOut("a=", aItems[nIdx])
   QOut("b=", aItems[nLast])
   // decimal index — cast path
   QOut("c=", aItems[nHalf])
   QOut("d=", aItems[FirstReal(aItems)])
   // int widens into decimal math
   QOut("e=", Str(nIdx * 1.5, 6, 1))
   // (explicit width: Harbour's
   // derived display widths for
   // var*literal aren't modelled)

RETURN

   // Returns an always-int local: the function's return type resolves to
   // INTEGER and callers may chain it straight into subscripts.
FUNCTION FirstReal( aList AS ARRAY ) AS NUMERIC

   LOCAL nPos := 1 AS NUMERIC

   DO WHILE nPos < Len(aList) .AND. Empty(aList[nPos])
      nPos := nPos + 1
   ENDDO

RETURN nPos
