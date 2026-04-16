// Regression: macro (&name) substitution — must fail -GS codegen.
// Pattern copied from sharedx/languages.prg and easiposx/xzauto.prg:
//     cField := "NAME"
//     xValue := oRec:&cField
// A macro expands to an identifier at Harbour runtime; C# has no
// equivalent, so the transpiler surfaces an error rather than
// emitting `oRec./* MACRO */` and breaking syntax.

PROCEDURE Main()
   LOCAL cField := "NAME"
   LOCAL xValue

   xValue := oRec:&cField

   RETURN
