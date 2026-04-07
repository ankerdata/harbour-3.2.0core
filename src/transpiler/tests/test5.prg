FUNCTION Main()
   
   LOCAL i
   LOCAL nSum := 0
   LOCAL aItems := { "apple", "banana", "cherry" }
   LOCAL cItem

   // FOR with STEP
   FOR i := 0 TO 20 STEP 5
      nSum := nSum + i
   NEXT

   // FOR counting down with negative STEP
   FOR i := 10 TO 1 STEP -1
      nSum := nSum + i
   NEXT

   // FOR EACH with DESCEND
   FOR EACH cItem IN aItems DESCEND
      nSum := nSum + Len( cItem )
   NEXT

RETURN nSum
