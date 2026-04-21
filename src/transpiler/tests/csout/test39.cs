using System;
using static HbRuntime;
using static Program;

// Test 39: initializers that need to survive expression reduction.
//
// Three separate fixes are exercised here:
//
//   a) LOCAL init with adjacent string literals ("ab" + "cd"). Before
//      the fix, hb_compExprGenStatement called HB_EA_REDUCE, which
//      rewrote the HB_EO_PLUS node in place and freed the original
//      pointer — the one the AST had already captured. Emitted .cs
//      came out with an empty init: `string cLocal = ;`.
//
//   b) LOCAL init with CHR() concatenation (CHR(16)+CHR(4)). Same
//      root cause — the reduction folds adjacent HB_ET_STRING results
//      and frees the parent.
//
//   c) Empty hash literal `{ => }` in a class DATA INIT slot. The
//      raw init text is passed through hb_csTranslateInit, which
//      now recognises `{ => }` and emits `new Dictionary<>()`.
//
// Real-code analogues: sharedx/transaction.prg (hash-backed class
// DATAs), easiposx/spoolprt.prg (DLE+EOT CHR-string LOCALs).

// #include "hbclass.ch"
public class Bag
{
    public string cHello { get; set; } = "ab" + "cd";
    public Dictionary<dynamic, dynamic> hTable { get; set; } = new Dictionary<dynamic, dynamic>();

}

public static partial class Program
{
    public static string test39_s_cStatic = HbRuntime.Chr(27) + "02" + HbRuntime.Chr(1);
    public static void Main(string[] args)
    {
        string cLocal = "ab" + "cd";
        string cPayload = HbRuntime.Chr(16) + HbRuntime.Chr(4);
        Dictionary<dynamic, dynamic> hEmpty = new Dictionary<dynamic, dynamic> {  };
        Bag oB = new Bag();

        HbRuntime.QOut("local:  " + cLocal);
        HbRuntime.QOut("llen:   " + HbRuntime.Str(HbRuntime.Len(cPayload), 4));
        HbRuntime.QOut("static: " + HbRuntime.Str(HbRuntime.Len(test39_s_cStatic), 4));
        HbRuntime.QOut("class:  " + oB.cHello);
        HbRuntime.QOut("hsize:  " + HbRuntime.Str(HbRuntime.Len(hEmpty), 4));
        HbRuntime.QOut("hclas:  " + HbRuntime.Str(HbRuntime.Len(oB.hTable), 4));

        return;
    }
}
