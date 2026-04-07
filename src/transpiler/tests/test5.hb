#include "astype.ch"
FUNCTION Main() AS INTEGER

   LOCAL i AS INTEGER
   LOCAL nSum := 0 AS INTEGER
   LOCAL aItems := {"apple", "banana", "cherry"} AS ARRAY
   LOCAL cItem AS STRING

   // FOR with STEP
   FOR i := 0 TO 20 STEP 5
      nSum += i
   NEXT

   // FOR counting down with negative STEP
   FOR i := 10 TO 1 STEP -1
      nSum += i
   NEXT

   // FOR EACH with DESCEND
   FOR EACH cItem IN aItems DESCEND
      nSum += Len(cItem)
   NEXT

RETURN nSum

