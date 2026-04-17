// Regression: bare macro eval `&cExpression` — must fail -GS codegen.
// Pattern copied from sharedx/ormsql.prg:
//     xResult := &cExpression
// A full runtime expression eval has no C# equivalent without an
// interpreter. The transpiler surfaces this as E0016. Note that
// obj:&(name) (dynamic member access) IS supported — it emits
// HbRuntime.GETMEMBER / SETMEMBER / SENDMSG calls. Only the
// standalone `&expr` form is unsupported.

PROCEDURE Main()
   LOCAL cExpression := "1 + 2"
   LOCAL xResult

   xResult := &cExpression

   RETURN
