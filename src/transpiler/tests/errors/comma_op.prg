// Regression: comma-operator in expression context — must fail
// -GS codegen. Pattern copied from easiposx/kpprt.prg:
//     IF GetItemRed(...) .OR. ((oTransaction, IsRefundItem(...) .OR. ...))
// Harbour evaluates `(a, b)` as a sequence returning the last
// value. C# has no equivalent in expression position, and leaving
// the bare `,` in an IF condition breaks downstream parsing.

PROCEDURE Main()
   LOCAL a := 1
   LOCAL b := 2

   IF (a, b) > 0
      ? "yes"
   ENDIF

   RETURN
