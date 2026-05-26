// W0024: an assignment / initializer whose RHS type contradicts the
// lvalue's Hungarian-prefix type — the prefix is the contract, so a
// `n`-prefixed variable holding a string (or `a` holding a hash, etc.)
// is a source-level error the transpiler surfaces without halting. The
// `x` prefix is the explicit USUAL escape and is never flagged.

PROCEDURE Main()
   LOCAL aFoo := { => }     // a says ARRAY, init is a HASH — fires
   LOCAL nBar               // declared, no init — no warning yet
   LOCAL xAny := "anything" // x says USUAL — no warning
   nBar := "hello"          // n says NUMERIC, RHS is STRING — fires
   ? aFoo, nBar, xAny
RETURN
