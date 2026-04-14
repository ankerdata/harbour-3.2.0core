#include "astype.ch"
// Test 30: EXIT (and STATIC) keyword disambiguation with trailing
// preserved `//` comments.
//
// The transpiler runs with comment preservation so comments can
// round-trip into the output. Preserved comments appear in the PP
// token stream between a keyword and the following newline. The
// lexer must skip those comment tokens when deciding whether a
// keyword like EXIT is acting as a statement or an identifier.
//
// In complex.c `case EXIT:` the disambiguation tested
//     HB_PP_TOKEN_ISEOC( pToken->pNext )
// on the raw next token, which is a COMMENT — not EOL, not EOC — so
// EXIT fell through to IDENTIFIER and the parser erred with E0020
// "Incomplete statement or unbalanced delimiters".
//
// Fix: peek through comments using HB_COMP_PEEK before the EOC check
// (complex.c `case EXIT: case STATIC:`). This is the same shape as
// several neighbouring disambiguation sites that were already using
// HB_COMP_PEEK.
//
// Hit in easiposx: fplu (3), mixmatch (3), highlight (2), fdiscnt,
// frepeat, fsminstruction, template, ttlcalc — 13 EXIT+comment lines
// across 8 files.

PROCEDURE Main()
   LOCAL i AS NUMERIC
   LOCAL nHits := 0 AS NUMERIC

   FOR i := 1 TO 10
      IF i > 5
         // bail once we pass the halfway mark
         EXIT
      ENDIF

      nHits++
   NEXT

   QOut("hits: " + LTrim(Str(nHits)))
RETURN
