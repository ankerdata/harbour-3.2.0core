// Regression: BEGIN SEQUENCE WITH takes only the break idiom,
// { |e| break(e) } or { || break() }, which C# carries as a catch;
// any other error block is E0101 (Alex, 2026-09-22).

PROCEDURE Main()
   LOCAL oError

   BEGIN SEQUENCE WITH {| oErr | QOut( oErr:description ) }
      ? 1 / 0
   RECOVER USING oError
      ? "recovered"
   END SEQUENCE

   RETURN
