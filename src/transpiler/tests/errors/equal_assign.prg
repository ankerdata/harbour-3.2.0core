// Regression: `=` as an assignment statement is E0100 — the source
// writes `:=` (Alex, 2026-09-19). Harbour parses `nX = 5` standing as
// a statement as an assignment, the same HB_EO_ASSIGN `:=` makes, so
// the check sits in the grammar action (harbour.yyc).

PROCEDURE Main()
   LOCAL nX := 1

   nX = 5
   ? nX

   RETURN
