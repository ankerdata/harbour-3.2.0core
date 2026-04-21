#include "astype.ch"
// Test 4: SWITCH/CASE/OTHERWISE/EXIT
FUNCTION Main() AS STRING

   LOCAL nVal := 2 AS NUMERIC
   LOCAL cResult AS STRING

   SWITCH nVal
      CASE 1
         cResult := "one"
         QOut("cResult=" + cResult)
         EXIT
      CASE 2
         cResult := "two"
         QOut("cResult=" + cResult)
         EXIT
      OTHERWISE
         cResult := "other"
         QOut("cResult=" + cResult)
   ENDSWITCH

RETURN cResult
