// Regression: a bare BREAK ending a SWITCH CASE is C's break; Harbour
// runs it as the sequence BREAK, so the CASE is never simply left
// (easipos/sockerror.prg, Alex, 2026-09-22). W0033 - EXIT was meant.

FUNCTION Main()
   LOCAL cName := ""
   LOCAL nCode := 2

   SWITCH nCode
   CASE 1
      cName := "one"
      BREAK
   CASE 2
      cName := "two"
      BREAK
   ENDSWITCH

   RETURN cName
