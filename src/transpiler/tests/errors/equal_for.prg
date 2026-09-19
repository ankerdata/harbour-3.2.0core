// Regression: `FOR i = 1` is E0100 — the source writes `FOR i := 1`
// (Alex, 2026-09-19).

PROCEDURE Main()
   LOCAL nI

   FOR nI = 1 TO 3
      ? nI
   NEXT

   RETURN
