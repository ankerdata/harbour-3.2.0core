FUNCTION Main()
   
   LOCAL nVal := 2
   LOCAL cResult

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
