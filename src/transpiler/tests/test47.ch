//
// Test 47 companion header — exercises the --preload-list filter.
//
// The preload filter must:
//   - Pass plain `#define NAME VALUE` through to hb_pp_readRules.
//   - Honour `#ifdef` / `#else` / `#endif` (evaluated by the PP
//     against the current define table, which includes any -D flags
//     because the preload loop now runs AFTER hb_compChkSetDefines).
//   - Drop `#xcommand` / `#xtranslate` / `#command` / `#translate`
//     silently. If even one rewrite rule escaped, LOUDOUT_47 below
//     would rewrite as an xcommand and test47.prg's plain-identifier
//     reference to it at runtime would either fail to compile or
//     change semantics — both would be caught by this test.
//   - Drop macro-shaped `#define NAME(x) ...` (registered as rules
//     by the PP).
//
// The .prg only uses the three plain defines; the other directives
// are here purely to prove the filter strips them. If the filter
// regresses and lets any of them through, compilation would either
// fail (MACRO_47 expanded into the body) or the define would
// register under the wrong name.

#ifndef TEST47_CH_
#define TEST47_CH_

#define TEST47_BASE   100

#ifdef TEST47_FLAG
#define TEST47_COND   42
#else
#define TEST47_COND   7
#endif

#define TEST47_LABEL  "filtered"

// These three must be silently dropped by the preload filter.
#xcommand LOUDOUT_47 <x> => QOut( Upper( <x> ) )
#translate DROP_ME_47   => "should-never-appear"
#define    MACRO_47( x )  ( ( x ) * 2 )

#endif /* TEST47_CH_ */
