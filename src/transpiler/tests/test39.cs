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
    static string test39_s_cStatic = HbRuntime.CHR(27) + "02" + HbRuntime.CHR(1);
    public static void Main(string[] args)
    {
        string cLocal = "ab" + "cd";
        string cPayload = HbRuntime.CHR(16) + HbRuntime.CHR(4);
        Dictionary<dynamic, dynamic> hEmpty = new Dictionary<dynamic, dynamic> {  };
        Bag oB = new Bag();

        HbRuntime.QOUT("local:  " + cLocal);
        HbRuntime.QOUT("llen:   " + HbRuntime.STR(HbRuntime.LEN(cPayload), 4));
        HbRuntime.QOUT("static: " + HbRuntime.STR(HbRuntime.LEN(test39_s_cStatic), 4));
        HbRuntime.QOUT("class:  " + oB.cHello);
        HbRuntime.QOUT("hsize:  " + HbRuntime.STR(HbRuntime.LEN(hEmpty), 4));
        HbRuntime.QOUT("hclas:  " + HbRuntime.STR(HbRuntime.LEN(oB.hTable), 4));

        return;
    }
}
