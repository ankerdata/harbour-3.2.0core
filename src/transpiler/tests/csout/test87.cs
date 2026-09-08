using System;
using static HbRuntime;
using static Program;

// Test 87: declared parameter defaults on strict value slots.
//
// `DEFAULT p TO v` (common.ch: `IF p == NIL ; p := v ; END`) and
// `hb_default(@p, v)` are the optional-parameter idiom. A nilable slot
// emits `T? p = null` and its guard works as written. A strict value
// slot — n/l/d/t Hungarian, non-nullable by the test53 rule — can never
// be null in C#, so the guard was dead code (CS0472 / CS8073) and an
// omitted argument arrived as `default` (false / 0) where Harbour gives
// v. In the easipos corpus that was 223 compiler warnings plus, with no
// warning at all, 135 `hb_default(ref lX, ...)` sites whose null check
// never fires: SaleTrailer's `DEFAULT lKeepChange TO .T.` reached C# as
// `bool lKeepChange = default`.
//
// When v is a C# constant — .T./.F., a numeric literal, a defines-map
// member — it is lifted onto the declaration (`bool lLoud = true`), the
// guard is dropped, and the short overload forwards the same constant.
// Guards on reference-typed slots are untouched.
// #include "common.ch"
// #include "hbclass.ch"
// #include "test87.ch"
public class Thing
{
    public decimal nCount = 0;

    public dynamic Bump(decimal nBy = 1)
    {
        this.nCount += nBy;
        return this;
    }
}

public static partial class Program
{
    public static void Main(string[] args)
    {
        decimal nTot = 10;
        Thing oThing = new Thing();

        Show(1);
        Show(2, false);
        Show(3, true, 7);
        Width();
        Width(10);
        HbRuntime.QOut("neg=" + HbRuntime.LTrim(HbRuntime.Str(Neg())));
        HbRuntime.QOut("acc1=" + HbRuntime.LTrim(HbRuntime.Str(Accum())));
        Accum(ref nTot);
        HbRuntime.QOut("acc2=" + HbRuntime.LTrim(HbRuntime.Str(nTot)));
        Accum(ref nTot, false);
        HbRuntime.QOut("acc3=" + HbRuntime.LTrim(HbRuntime.Str(nTot)));
        oThing.Bump();
        oThing.Bump(5);
        HbRuntime.QOut("count=" + HbRuntime.LTrim(HbRuntime.Str(oThing.nCount)));
        HbRuntime.QOut("name1=" + Name());
        HbRuntime.QOut("name2=" + Name("x"));
        return;

        /* Both DEFAULTs lift: `bool lLoud = true, decimal nTimes = 2`. */
    }
    public static void Show(decimal nId = default, bool lLoud = true, decimal nTimes = 2)
    {
        HbRuntime.QOut("show " + HbRuntime.LTrim(HbRuntime.Str(nId)) + " " + (lLoud ? "T" : "F") + " " + HbRuntime.LTrim(HbRuntime.Str(nTimes)));
        return;

        /* hb_default form, defaulting to a header #define — a Const-class
   member, so still a C# constant: `decimal nW = Test87Const.TEST87_WIDTH`. */
    }
    public static void Width(decimal nW = Test87Const.TEST87_WIDTH)
    {
        HbRuntime.QOut("width=" + HbRuntime.LTrim(HbRuntime.Str(nW)));
        return;

        /* A negative literal. */
    }
    public static decimal Neg(decimal nN = -1)
    {
        return nN;

        /* nTotal is by-ref (the `@nTot` call sites) and sits first, so the
   canonical takes `ref decimal nTotal` and cannot carry a default; the
   parameterless short overload forwards the lifted 0 instead. lDouble
   follows the ref and carries `= true` on the canonical, which is what
   `Accum( @nTot )` relies on. */
    }
    public static decimal Accum(ref decimal nTotal, bool lDouble = true)
    {
        nTotal += (lDouble ? 2 : 1);
        return nTotal;

        /* A reference-typed slot: cName is nilable, `string? cName = null`, and
   its guard stays exactly as before. */
    }

    public static dynamic Accum()
    {
        decimal _arg0 = 0;
        bool _arg1 = true;
        return Accum(ref _arg0, _arg1);
    }
    public static string Name(string? cName = null)
    {
        if (cName == null)
        {
            cName = "anon";
        }
        return cName;

        /* The method path: `decimal nBy = 1`. */
    }
}
