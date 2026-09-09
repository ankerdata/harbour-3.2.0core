using System;
using System.Collections.Generic;
using static HbRuntime;
using static Program;

// Test 91: ref-shims at method sends.
//
// C# `ref` is invariant: a typed lvalue cannot bind a `ref dynamic`
// parameter, a `dynamic` one cannot bind `ref bool`, and a member access
// is not a ref-able location at all. Function calls have had the shim —
// a temp of the parameter's type passed by ref and copied back — since
// test57/66/68. Method sends did not: the shim collector only knew
// FUNCALL nodes, so `oObj:Method(@x)` and `::Super:Method(@x)` emitted
// a bare `ref x` (CS1503 in the easipos corpus: EasiZVT's
// @lPrtMerchantReceipt, BrowseDialog's @cSearchFor), and
// `oObj:cField := Func(@oObj:nField)` fell through because the
// assignment target was a member, not a variable (fnb's
// GetProFormaSerialNo). The write-back into an AS INTEGER member takes
// the same (long) coercion an ordinary assignment gets.
// #include "hbclass.ch"
/* USUAL slot, by-ref: a typed caller variable needs the shim. */
public class Clicker
{
    public long nCount = 0;
    public string cLabel = "";

    public dynamic Bump(ref dynamic xValue)
    {
        xValue = xValue + 1;
        return null;

        /* Logical slot, by-ref, returning the new value — used inside an IF so
   the send is hoisted out of the condition. */
    }

    public bool Flip(ref bool lFlag)
    {
        lFlag = !lFlag;
        return lFlag;

        /* ::Super:Method(@param): a decimal parameter into the USUAL slot. */
    }
}

public class Doubler : Clicker
{

    public decimal Bump2(decimal nValue = default)
    {
        {
            dynamic _hbref_nValue = nValue;
            base.Bump(ref _hbref_nValue);
            nValue = _hbref_nValue;
        }
        {
            dynamic _hbref_nValue = nValue;
            base.Bump(ref _hbref_nValue);
            nValue = _hbref_nValue;
        }
        return nValue;

        /* By-ref NUMERIC slot fed from an INTEGER member: ref long vs ref decimal. */
    }
}

public static partial class Program
{
    public static string NextSerial(ref decimal nSerial)
    {
        nSerial++;
        return "S" + HbRuntime.LTrim(HbRuntime.Str(nSerial));
    }

    public static void Main(string[] args)
    {
        Clicker oClicker = new Clicker();
        Doubler oDoubler = new Doubler();
        decimal nLocal = 10;
        bool xFlag = false;

        // decimal into ref dynamic
        {
            dynamic _hbref_nLocal = nLocal;
            oClicker.Bump(ref _hbref_nLocal);
            nLocal = _hbref_nLocal;
        }
        HbRuntime.QOut("local=" + HbRuntime.LTrim(HbRuntime.Str(nLocal)));
        // INTEGER member into ref dynamic
        {
            dynamic _hbref_nCount_0 = oClicker.nCount;
            oClicker.Bump(ref _hbref_nCount_0);
            oClicker.nCount = (long)(_hbref_nCount_0);
        }
        HbRuntime.QOut("member=" + HbRuntime.LTrim(HbRuntime.Str(oClicker.nCount)));
        // dynamic into ref bool, hoisted
        if (oClicker.Flip(ref xFlag))
        {
            HbRuntime.QOut("flag=T");
        }
        else
        {
            HbRuntime.QOut("flag=F");
        }

        HbRuntime.QOut("twice=" + HbRuntime.LTrim(HbRuntime.Str(oDoubler.Bump2(5))));
        // member target, member ref arg
        {
            decimal _hbref_nCount_0 = oClicker.nCount;
            oClicker.cLabel = NextSerial(ref _hbref_nCount_0);
            oClicker.nCount = (long)(_hbref_nCount_0);
        }
        HbRuntime.QOut("serial=" + oClicker.cLabel + " count=" + HbRuntime.LTrim(HbRuntime.Str(oClicker.nCount)));
        return;
    }
}
