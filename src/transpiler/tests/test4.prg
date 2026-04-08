FUNCTION Main()

   LOCAL nVal := 2
   LOCAL cResult

   SWITCH nVal
   CASE 1
      cResult := "one"
      ? "cResult=" + cResult
      EXIT
   CASE 2
      cResult := "two"
      ? "cResult=" + cResult
      EXIT
   OTHERWISE
      cResult := "other"
      ? "cResult=" + cResult
   ENDSWITCH

RETURN cResult
