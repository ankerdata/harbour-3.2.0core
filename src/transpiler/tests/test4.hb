#include "astype.ch"
FUNCTION Main() AS STRING

   LOCAL nVal := 2 AS INTEGER
   LOCAL cResult AS STRING

   SWITCH nVal
      CASE 1
         cResult := "one"
         EXIT
      CASE 2
         cResult := "two"
         EXIT
      OTHERWISE
         cResult := "other"
   ENDSWITCH

RETURN cResult

