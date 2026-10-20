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
public class Box : IHbObject
{
    public decimal nSize = 1;

    public virtual decimal Widen(decimal? nBy = null)
    {
        decimal nBy_ = nBy ?? nSize * 2;
        nSize += nBy_;
        return nSize;
    }
}

public static partial class Program
{
    public static decimal test88_snWidth = 7;
    public static void Main(string[] args)
    {
        decimal nTot = 100;
        decimal nCnt = 0;
        Box oBox = new Box();

        QOut("pad1=" + Dots("ab"));
        QOut("pad2=" + Dots("ab", 4));
        QOut("pad3=" + Dots("ab", null));
        QOut("next1=" + LTrim(Str(NextIdx(10))));
        QOut("next2=" + LTrim(Str(NextIdx(10, 3))));
        QOut("gap=" + LTrim(Str(Stretch(5, nTimes: 2))));
        QOut("acc1=" + LTrim(Str(Accrue())));
        Accrue(ref nTot);
        QOut("acc2=" + LTrim(Str(nTot)));
        Accrue(ref nTot, nTimes: 2);
        QOut("acc3=" + LTrim(Str(nTot)));
        Tally(default(decimal?), ref nCnt);
        QOut("tally1=" + LTrim(Str(nCnt)));
        Tally(3, ref nCnt);
        QOut("tally2=" + LTrim(Str(nCnt)));
        QOut("box1=" + LTrim(Str(oBox.Widen())));
        QOut("box2=" + LTrim(Str(oBox.Widen(5))));
    }

    public static decimal CurWidth()
    {
        return test88_snWidth;

        /* hb_default form; the default is a call, so nothing constant to lift.
   nWidth then reaches a typed callee — proof the body sees the
   normalised `decimal`, not `decimal?`. */
    }
    public static string Dots(string cText = default, decimal? nWidth = null)
    {
        decimal nWidth_ = nWidth ?? CurWidth();
        return cText + Replicate(".", nWidth_ - Len(cText));

        /* DEFAULT form; the default reads another parameter. */
    }
    public static decimal NextIdx(decimal nBase = default, decimal? nIdx = null)
    {
        decimal nIdx_ = nIdx ?? nBase + 1;
        return nIdx_;

        /* The default reads a LOCAL computed before the DEFAULT line, so the
   normalising local must be emitted where the DEFAULT is, not hoisted
   to the top. Called with a middle gap, so nFactor arrives via the
   named-argument form and its `= null`. */
    }
    public static decimal Stretch(decimal nValue = default, decimal? nFactor = null, decimal nTimes = 1)
    {
        decimal nBase = nValue * 10;
        decimal nFactor_ = nFactor ?? nBase - 40;
        return nValue * nFactor_ * nTimes;

        /* nTotal is by-ref (@nTot) and sits first; nStep's default is not a
   constant, so the parameterless short overload forwards null and the
   canonical normalises it. `Accrue( @nTot, , 2 )` gaps nStep after the
   ref: named-argument form, so the canonical's `= null` applies. */
    }
    public static decimal Accrue(ref decimal nTotal, decimal? nStep = null, decimal nTimes = 1)
    {
        decimal nStep_ = nStep ?? CurWidth() * 2;
        nTotal += nStep_ * nTimes;
        return nTotal;

        /* nStart's default IS a constant, but the slot sits before the by-ref
   nCount, so the canonical cannot carry `= 1`: it goes nullable at the
   boundary instead, the body normalises it, and the reftab's `D` flag
   tells the gapped caller `Tally( , @nCnt )` to pad null. */
    }

    public static decimal Accrue()
    {
        decimal _arg0 = 0;
        decimal? _arg1 = null;
        decimal _arg2 = 1;
        return Accrue(ref _arg0, _arg1, _arg2);
    }
    public static void Tally(decimal? nStart, ref decimal nCount)
    {
        decimal nStart_ = nStart ?? 1;
        nCount = nStart_ * 10;

        /* The method path: the default reads a member. */
    }
}
