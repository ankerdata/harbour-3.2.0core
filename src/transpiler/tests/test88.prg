// Test 88: non-constant declared defaults — nullable at the boundary,
// strict inside.
//
// test87 lifts `DEFAULT p TO <const>` onto the declaration. When the
// default is an expression — `hb_default(@nIndex, oTransaction:
// GetSaleIndex() + 1)`, `DEFAULT dArchive TO Date()` — there is no C#
// constant to lift, and on a strict value slot the guard was dead: an
// omitted argument arrived as `default` (0 / 0001-01-01) and the default
// expression never ran. IncBuffIdx alone had 235 such call sites in the
// easipos corpus, each inserting its line at index 0.
//
// The parameter now goes nullable at the boundary, `decimal? nWidth =
// null`, keeping its Harbour name so named-argument callers are
// unaffected. Where the DEFAULT stood the emitter declares the strict
// local `decimal nWidth_ = nWidth ?? <expr>;` — at that position, not
// hoisted, so a default that reads an earlier LOCAL keeps its order and
// `??` short-circuits like the guard did — and every later reference to
// the parameter emits the local. Explicit NIL takes the default too,
// as in Harbour. By-ref slots are excluded; short overloads forward
// null; methods work like functions.
//
// The same boundary form covers a CONSTANT default on a value slot that
// sits at or before the last by-ref parameter (Tally below): no C#
// default can live there, and a caller in another file that omits the
// slot only knows to pad null because the scan marks it `D` in the
// reftab. A gap after the last by-ref parameter (Accrue's nStep) uses
// named-argument form so the canonical's own default applies.
#include "common.ch"
#include "hbclass.ch"

STATIC snWidth := 7

PROCEDURE Main()
   LOCAL nTot := 100
   LOCAL nCnt := 0
   LOCAL oBox := Box():New()

   ? "pad1=" + Dots( "ab" )
   ? "pad2=" + Dots( "ab", 4 )
   ? "pad3=" + Dots( "ab", NIL )
   ? "next1=" + LTrim( Str( NextIdx( 10 ) ) )
   ? "next2=" + LTrim( Str( NextIdx( 10, 3 ) ) )
   ? "gap="   + LTrim( Str( Stretch( 5, , 2 ) ) )
   ? "acc1="  + LTrim( Str( Accrue() ) )
   Accrue( @nTot )
   ? "acc2="  + LTrim( Str( nTot ) )
   Accrue( @nTot, , 2 )
   ? "acc3="  + LTrim( Str( nTot ) )
   Tally( , @nCnt )
   ? "tally1=" + LTrim( Str( nCnt ) )
   Tally( 3, @nCnt )
   ? "tally2=" + LTrim( Str( nCnt ) )
   ? "box1="  + LTrim( Str( oBox:Widen() ) )
   ? "box2="  + LTrim( Str( oBox:Widen( 5 ) ) )
RETURN

FUNCTION CurWidth()
RETURN snWidth

/* hb_default form; the default is a call, so nothing constant to lift.
   nWidth then reaches a typed callee — proof the body sees the
   normalised `decimal`, not `decimal?`. */
FUNCTION Dots( cText, nWidth )
   hb_default( @nWidth, CurWidth() )
RETURN cText + Replicate( ".", nWidth - Len( cText ) )

/* DEFAULT form; the default reads another parameter. */
FUNCTION NextIdx( nBase, nIdx )
   DEFAULT nIdx TO nBase + 1
RETURN nIdx

/* The default reads a LOCAL computed before the DEFAULT line, so the
   normalising local must be emitted where the DEFAULT is, not hoisted
   to the top. Called with a middle gap, so nFactor arrives via the
   named-argument form and its `= null`. */
FUNCTION Stretch( nValue, nFactor, nTimes )
   LOCAL nBase := nValue * 10
   DEFAULT nFactor TO nBase - 40
   DEFAULT nTimes TO 1
RETURN nValue * nFactor * nTimes

/* nTotal is by-ref (@nTot) and sits first; nStep's default is not a
   constant, so the parameterless short overload forwards null and the
   canonical normalises it. `Accrue( @nTot, , 2 )` gaps nStep after the
   ref: named-argument form, so the canonical's `= null` applies. */
FUNCTION Accrue( nTotal, nStep, nTimes )
   DEFAULT nTotal TO 0
   DEFAULT nStep TO CurWidth() * 2
   DEFAULT nTimes TO 1
   nTotal += nStep * nTimes
RETURN nTotal

/* nStart's default IS a constant, but the slot sits before the by-ref
   nCount, so the canonical cannot carry `= 1`: it goes nullable at the
   boundary instead, the body normalises it, and the reftab's `D` flag
   tells the gapped caller `Tally( , @nCnt )` to pad null. */
PROCEDURE Tally( nStart, nCount )
   DEFAULT nStart TO 1
   nCount := nStart * 10
RETURN

CLASS Box
   VAR nSize INIT 1
   METHOD Widen( nBy )
ENDCLASS

/* The method path: the default reads a member. */
METHOD Widen( nBy ) CLASS Box
   DEFAULT nBy TO ::nSize * 2
   ::nSize += nBy
RETURN ::nSize
