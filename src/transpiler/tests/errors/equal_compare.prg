// Regression: `=` as a comparison is E0100 — the source writes `==`
// (Alex, 2026-09-19). On strings `=` follows SET EXACT: under ON
// `"~   " = "~"` is .T., which C#'s exact `==` is not (loadfile.prg's
// `IF cMessage = "~"`).

PROCEDURE Main()
   LOCAL cMessage := "~   "

   IF cMessage = "~"
      ? "blank"
   ENDIF

   RETURN
