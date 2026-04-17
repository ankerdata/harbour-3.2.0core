#include "astype.ch"
// Test 5: FOR with STEP, FOR counting down, FOR EACH DESCEND
FUNCTION Main() AS NUMERIC

   LOCAL i AS NUMERIC
   LOCAL nSum := 0 AS NUMERIC
   LOCAL aItems := {"apple", "banana", "cherry"} AS ARRAY
   LOCAL cItem AS STRING

   // FOR with STEP
   FOR i := 0 TO 20 STEP 5
      nSum := nSum + i
   NEXT

   QOut("nSum after STEP=" + Str(nSum))

   // FOR counting down with negative STEP
   FOR i := 10 TO 1 STEP -1
      nSum := nSum + i
   NEXT

   QOut("nSum after DOWN=" + Str(nSum))

   // FOR EACH with DESCEND
   FOR EACH cItem IN aItems DESCEND
      nSum := nSum + Len(cItem)
   NEXT

   QOut("nSum=" + Str(nSum))

RETURN nSum
