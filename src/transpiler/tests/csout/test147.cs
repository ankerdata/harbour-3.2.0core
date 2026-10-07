using System;
using static HbRuntime;
using static Program;

// Test 147: a string literal is its bytes.
//
// A Harbour string is bytes, and a source may hold them in any encoding:
// EasiPOS has Windows-1252 (fmde.prg's TSE label, prtinit.prg's printer
// tables) beside UTF-8 (testsetup.prg's receipt layout). The emitter copied
// a literal's bytes into the .cs, which the C# compiler reads as UTF-8: a
// lone high byte became U+FFFD and UTF-8's three bytes one char, so lengths,
// truncation and what was written moved. Each byte is its own char now,
// \u00XX. The output is byte values, which no console encoding can change.
public static partial class Program
{
    public static void Main(string[] args)
    {
        string cEuro147 = "\u00E2\u0082\u00AC/kg = \u00E2\u0082\u00AC";
        string cUmlaut147 = "z\u00E4hler";
        string cEsc147 = "\u001B@";

        HbRuntime.QOut("euro:", HbRuntime.Len(cEuro147), HbRuntime.Asc(cEuro147), HbRuntime.Asc(HbRuntime.SubStr(cEuro147, 2)), HbRuntime.Asc(HbRuntime.SubStr(cEuro147, 3)));
        HbRuntime.QOut("cut to 9:", HbRuntime.Len(HbRuntime.Left(cEuro147, 9)), HbRuntime.Asc(HbRuntime.Right(HbRuntime.Left(cEuro147, 9), 1)));
        HbRuntime.QOut("umlaut:", HbRuntime.Len(cUmlaut147), HbRuntime.Asc(HbRuntime.SubStr(cUmlaut147, 2)));
        HbRuntime.QOut("escape:", HbRuntime.Len(cEsc147), HbRuntime.Asc(cEsc147));

        return;
    }
}
