using System;
using static HbRuntime;
using static Program;

// Test 57: ref-shim for a typed lvalue passed by-ref to a USUAL
// (`ref dynamic`) parameter.
//
// Harbour's polymorphic by-ref idiom: a function takes `@xVal` and
// fills it with a number, a string, or a logical depending on
// context. The `x`-prefix keeps the parameter USUAL, so the C#
// emit is `ref dynamic xVal`.
//
// C# `ref` is invariant — a caller's `ref decimal` / `ref string`
// lvalue cannot bind to `ref dynamic`. For a call STATEMENT the
// transpiler wraps the call in a brace block that copies each such
// lvalue through a temp named after it (`_hbref_<var>`) and back:
//
//     {
//         dynamic _hbref_nGot = nGot;
//         FetchValue(1, ref _hbref_nGot);
//         nGot = _hbref_nGot;
//     }
//
// Covered here: a plain variable lvalue (@nGot / @cGot), a class
// DATA field lvalue (@oH:nField, the HB_ET_REFERENCE arg form), and
// the `var := Foo(@x)` assignment form. Expression-context calls
// (e.g. `IF Foo(@x)`) are out of scope — they need temp hoisting.

// #include "hbclass.ch"
public class Holder
{
    public decimal nField = 0;

    public dynamic New()
    {
        return this;
    }
}

public static partial class Program
{
    public static void Main(string[] args)
    {
        decimal nGot = 0;
        string cGot = "";
        bool lOk = default;
        Holder oH = new Holder();

        // bare call statement — typed numeric variable by-ref
        {
            dynamic _hbref_nGot = nGot;
            FetchValue(1, ref _hbref_nGot);
            nGot = _hbref_nGot;
        }
        HbRuntime.QOut("num=" + HbRuntime.LTrim(HbRuntime.Str(nGot)));

        // bare call statement — typed string variable by-ref
        {
            dynamic _hbref_cGot = cGot;
            FetchValue(2, ref _hbref_cGot);
            cGot = _hbref_cGot;
        }
        HbRuntime.QOut("str=" + HbRuntime.RTrim(cGot));

        // bare call statement — typed class DATA field by-ref
        {
            dynamic _hbref_nField_1 = oH.nField;
            FetchValue(1, ref _hbref_nField_1);
            oH.nField = _hbref_nField_1;
        }
        HbRuntime.QOut("field=" + HbRuntime.LTrim(HbRuntime.Str(oH.nField)));

        // `var := Foo(@x)` form — the assignment-case shim
        {
            dynamic _hbref_nGot = nGot;
            lOk = FetchValue(1, ref _hbref_nGot);
            nGot = _hbref_nGot;
        }
        HbRuntime.QOut("into=" + HbRuntime.LTrim(HbRuntime.Str(nGot)) + " ok=" + (lOk ? "Y" : "N"));
        return;

        /* `xVal` is x-prefixed -> USUAL -> emits `ref dynamic`. It receives
   both a number and a string across call sites, so it is genuinely
   polymorphic and stays USUAL. */
    }
    public static bool FetchValue(decimal nKind, ref dynamic xVal)
    {
        if (nKind == 1)
        {
            xVal = 42;
        }
        else
        {
            xVal = "hello";
        }

        return true;
    }
}
