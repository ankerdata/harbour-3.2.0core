using System;
using static HbRuntime;
using static Program;

// Test 73: header #defines used in CLASS VAR INIT positions resolve
// via the per-source Const class.
//
// CLASS VAR INIT values and INLINE method bodies pass through
// hb_csTranslateInline as raw text — they bypass the regular AST emit
// path. That translator looks up identifiers in hbfuncs.tab so a
// Harbour built-in becomes `HbRuntime.NAME`, but used to leave header
// `#define`s as bare identifiers. The result:
//
//   public decimal nPanel = TEST73_BASE_PANEL;   // CS0103
//
// because TEST73_BASE_PANEL is declared on per-source `Test73Const`,
// which isn't in the file's `using static` set. Now the same
// hb_defineMapLookupCanon hook the regular HB_ET_VARIABLE emit path
// uses kicks in here too, producing
//
//   public decimal nPanel = Test73Const.TEST73_BASE_PANEL;
//
// so the C# compiles and the constants flow through at runtime.

// #include "hbclass.ch"
// #include "test73.ch"
public class Test73Holder
{
    public decimal nPanel = Test73Const.TEST73_BASE_PANEL;
    public string cName = Test73Const.TEST73_DEFAULT_NAME;

}

public static partial class Program
{
    public static void Main(string[] args)
    {
        Test73Holder oH = new Test73Holder();
        HbRuntime.QOut("nPanel=" + HbRuntime.AllTrim(HbRuntime.Str(oH.nPanel)));
        HbRuntime.QOut("cName=" + oH.cName);
        return;
    }
}
