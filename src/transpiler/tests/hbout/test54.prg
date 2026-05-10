#include "astype.ch"
// Test 54: paren preservation around binary infix ops nested in
// higher-precedence parents (typically unary `!` or `-`).
//
// Bug pattern: source `IF !(cPath + aFile[1] == cZipFile)` was
// emitted as C# `if (!cPath + aFile[1] == cZipFile)`, dropping
// the outer parens. The unary `!` then bound to `cPath` only,
// surfacing as CS0023 "operator ! cannot be applied to type
// string". Same shape with `-(a+b)` etc.
//
// 113 of 114 CS0023 errors in easipos collapsed once the paren-
// emit was fixed. Pattern observed in zipunzip.prg, repeated
// across 13 files.

PROCEDURE Main()
   LOCAL cPath := "/tmp/" AS STRING
   LOCAL aFile := {"report.zip"} AS ARRAY
   LOCAL cZipFile := "/tmp/report.zip" AS STRING
   LOCAL nA := 5 AS NUMERIC
   LOCAL nB := 3 AS NUMERIC

   /* The headline case: `!` over a comparison whose left side is
      itself a binary op. Three-level nesting. */
   IF !(cPath + aFile[1] == cZipFile)
      QOut("different")
   ELSE
      QOut("match")
   ENDIF

   /* Negation around an arithmetic expression. */
   QOut("neg= " + LTrim(Str(-(nA + nB))))

   /* `!` over a logical AND — both sides are comparisons. */
   IF !(nA > 0 .AND. nB > 0)
      QOut("one zero")
   ELSE
      QOut("both pos")
   ENDIF

   /* `!` over a logical OR. */
   IF !(nA == 0 .OR. nB == 0)
      QOut("neither zero")
   ELSE
      QOut("some zero")
   ENDIF

RETURN
