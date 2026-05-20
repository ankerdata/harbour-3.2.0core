// Test 65: "discard-the-outputs" overload for by-ref OUTPUT params.
//
// ErrText65 has a by-ref output param (lLog) sitting before a by-value
// param (lUpper). Harbour callers routinely ignore the output: they
// omit it, or skip it while still passing a later by-value arg
// (ErrText65(7, , .t.)). C# can't omit a `ref` param, so the emitter
// adds a "by-value" overload that exposes only the by-value params and
// supplies dummy `ref` storage; the skip-the-ref call reaches it via a
// named argument (ErrText65(7, lUpper: true)).
//
// This also exercises the prefix overload (nCode only) and the
// canonical signature (passing @lLog), so all three forms must round
// trip identically through -GT (Harbour) and -GS (C#).

FUNCTION ErrText65( nCode, lLog, lUpper )
   lLog := ( nCode > 0 )            // written back -> marks lLog by-ref
   RETURN iif( lUpper == .t., "CODE", "code" ) + Str( nCode, 2 )

PROCEDURE Main()
   LOCAL lLog := .f.
   ? ErrText65( 5 )                 // omit lLog + lUpper -> short overload
   ? ErrText65( 7, , .t. )          // skip lLog, pass lUpper -> by-value overload
   ? ErrText65( 3, @lLog )          // pass @lLog -> canonical (writes lLog back)
   ? "lLog=" + iif( lLog, "Y", "N" )
RETURN
