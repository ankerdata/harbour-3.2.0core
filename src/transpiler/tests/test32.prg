// Test 32: PP command-rule matching with trailing `//` comments.
//
// With comment preservation enabled, a #command line like
//     SET CENTURY ON      // not a set - __SetCentury
// used to hang the preprocessor for tens of seconds per line. The
// rule `#command SET CENTURY <x:ON,OFF,&> => __SetCentury( <(x)> )`
// has a STRSMART (<(x)>) result marker that entered
// hb_pp_matchResultLstAdd's for(;;) walk with a match range that
// included the trailing COMMENT token, and the walk failed to reach
// its stop sentinel.
//
// Fix: in hb_pp_processLine, detach trailing `//` comment tokens from
// the token chain before running #define / #translate / #command rule
// matching, then splice them back onto the expanded result so the
// lexer's comment-preservation hook (complex.c HB_PP_TOKEN_COMMENT
// branch) still captures them as AST nodes for .hb / .cs round-trip.
//
// This test exercises two patterns that previously hung:
//   - A plain #command match (SET CENTURY ON // ...)
//   - DEFAULT var TO value // ... — std.ch's DEFAULT command is the
//     pattern that hit buffprt/ttlcalc E0020 collateral errors.
//
// Hit in easiposx: startup.prg (40+s timeout), buffprt.prg,
// ttlcalc.prg — 3 files cleared.

#include "set.ch"

PROCEDURE Main()
   // #command match + trailing line comment.
   SET CENTURY ON      // not a set - __SetCentury
   SET EXACT ON        // needed for ASORT

   Probe( 1, .T. )
   Probe( 2 )
   Probe()
RETURN

PROCEDURE Probe(nKind, lReady)
   // DEFAULT ... TO ... // comment — std.ch translate rule.
   DEFAULT nKind TO 0             // default kind
   DEFAULT lReady TO .F.          // not ready by default
   IF lReady
      ? "kind=" + LTrim(Str(nKind)) + " ready=Y"
   ELSE
      ? "kind=" + LTrim(Str(nKind)) + " ready=N"
   ENDIF
RETURN
