using System;
using static HbRuntime;
using static Program;

// Test 71: non-`@` args to by-ref params get a value-in / no-writeback shim.
//
// Harbour treats a parameter as a local copy at the *call* site —
// reassigning it inside the function only affects the caller's
// variable when the caller wrote `@` at the call site (only valid on
// `@var` and `@obj:member`; the parser silently drops a stray `@` on
// `aArr[i]` or `hash["k"]`). The reftab however tags a parameter as
// RW any time the body writes to it, so from a C# binding perspective
// every caller would need `ref`.
//
// The transpiler reconciles this: HB_ET_VARREF (`@var`) and
// HB_ET_REFERENCE (`@obj:member`) write back through the shim;
// every other shape — field access, array element, literal,
// expression, or a plain variable whose static type differs from the
// parameter's — gets a fresh temp initialized from the value, passed
// by `ref`, and *no* writeback. The caller's original storage stays
// put exactly as Harbour value semantics promise.
//
// Consume() splits its input at the first separator, returning the
// prefix and (by side effect) advancing the input past it. We call
// Consume from the four shapes that exercise different shim paths:
//   • `@cVar`         — VARREF, writes back
//   • aArr[1]         — ARRAYAT, shim with no writeback
//   • oObj["cField"]  — hash lookup, shim with no writeback
//   • literal         — STRING, shim with no writeback
// The before/after `?` outputs make the writeback contrast directly
// observable across .prg / .hb / .cs.
public static partial class Program
{
    public static string Consume(ref string cString, string cSep = default)
    {
        string cFirst = "";
        if (!HbRuntime.Empty(cSep) && HbRuntime.HbIn(cSep, cString))
        {
            cFirst = HbRuntime.Left(cString, HbRuntime.At(cSep, cString) - 1);
            cString = HbRuntime.SubStr(cString, HbRuntime.At(cSep, cString) + HbRuntime.Len(cSep));
        }

        return cFirst;
    }

    public static void Main(string[] args)
    {
        string cVar = "alpha,one";
        dynamic[] aArr = new dynamic[] { "beta,two" };
        Dictionary<string, dynamic> oObj = new Dictionary<string, dynamic> {  };
        string cTaken = default;
        oObj["cField"] = "gamma,three";

        /* @cVar — writeback expected: cVar advances past the comma */
        cTaken = Consume(ref cVar, ",");
        HbRuntime.QOut("@var   in=alpha,one  out=" + cVar + "  took=" + cTaken);

        /* aArr[1] — value-in only: aArr[1] must still read "beta,two" after */
        {
            string _hbref_aArr_0 = aArr[0];
            cTaken = Consume(ref _hbref_aArr_0, ",");
        }
        HbRuntime.QOut(" elem  in=beta,two   out=" + aArr[0] + "  took=" + cTaken);

        /* oObj["cField"] — value-in only: the hash entry stays "gamma,three" */
        {
            string _hbref_oObj_0 = oObj["cField"];
            cTaken = Consume(ref _hbref_oObj_0, ",");
        }
        HbRuntime.QOut(" hash  in=gamma,three out=" + oObj["cField"] + "  took=" + cTaken);

        /* literal — there is no storage to write back to */
        {
            string _hbref_arg_0 = "delta,four";
            cTaken = Consume(ref _hbref_arg_0, ",");
        }
        HbRuntime.QOut(" lit                  out=(literal)        took=" + cTaken);
        return;
    }
}
