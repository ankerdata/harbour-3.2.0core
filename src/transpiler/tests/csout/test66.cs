using System;
using static HbRuntime;
using static Program;

// Test 66: ref-invariance shim in both directions and in expression
// context.
//
// C# `ref` is invariant: a `ref dynamic` argument can't bind a typed
// `ref string` parameter, nor can a typed `ref decimal` argument bind a
// `ref dynamic` parameter. The emitter copies each by-ref lvalue through
// a temp of the parameter's own type and copies it back. When the call
// sits inside an `if` condition or a RETURN it must first be hoisted into
// a preceding statement so the temp shuffle has somewhere to live.
//
//   FillBuf : typed by-ref param (cText -> string); caller passes a
//             dynamic local              -> dynamic -> ref string
//   Bump66    : USUAL by-ref param (xVal); caller passes a decimal local
//             -> ref decimal -> ref dynamic
//
// Each is exercised both as a bare statement and inside an if / return.
public static partial class Program
{
    public static bool FillBuf(ref string cText, decimal nLen = default)
    {
        // writes cText back -> by-ref
        cText = HbRuntime.Replicate("x", nLen);
        // logical result for the if/return
        return HbRuntime.Len(cText) == nLen;
    }

    public static dynamic Bump66(ref dynamic xVal)
    {
        // writes xVal back -> by-ref
        xVal = xVal + 1;
        return xVal;
    }

    public static dynamic Grab(decimal nLen = default)
    {
        // untyped -> dynamic local
        dynamic buf = default;
        // call hoisted out of the if
        {
            string _hbref_buf = buf;
            var _hbcall_FillBuf = FillBuf(ref _hbref_buf, nLen);
            buf = _hbref_buf;
            if (_hbcall_FillBuf)
            {
                return buf;
            }
        }

        return "?";
    }

    public static void Main(string[] args)
    {
        // untyped -> dynamic
        dynamic buf = default;
        // decimal
        decimal nCount = 10;
        bool lOK = default;
        // statement-context reverse shim
        {
            string _hbref_buf = buf;
            lOK = FillBuf(ref _hbref_buf, 3);
            buf = _hbref_buf;
        }
        HbRuntime.QOut("buf=" + buf + " ok=" + (lOK ? "Y" : "N"));
        // if-hoist, returns the buffer
        HbRuntime.QOut("grab=" + Grab(5));
        // statement-context original shim
        {
            dynamic _hbref_nCount = nCount;
            Bump66(ref _hbref_nCount);
            nCount = _hbref_nCount;
        }
        HbRuntime.QOut("count=" + HbRuntime.Str(nCount, 3));
        // expression-context original shim
        {
            dynamic _hbref_nCount = nCount;
            var _hbcall_Bump66 = Bump66(ref _hbref_nCount);
            nCount = _hbref_nCount;
            if (_hbcall_Bump66 > 0)
            {
                HbRuntime.QOut("bumped=" + HbRuntime.Str(nCount, 3));
            }
        }

        return;
    }
}
