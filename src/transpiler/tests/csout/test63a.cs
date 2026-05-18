using System;
using static HbRuntime;
using static Program;

// Test 63a: a file-scoped STATIC function whose name collides with
// a global function defined in another file (test63b's Render63).
//
// The reftab keys them apart — test63a::Render63 vs Render63 — but
// the function-DEFINITION emit must read the file-scoped key. If it
// reads the bare name it picks up test63b's global entry, whose
// parameter is numeric, and emits this static with a `decimal`
// parameter — then `cText` is mistyped and the body's string
// concatenation fails (CS1503).
public static partial class Program
{
    public static void Main(string[] args)
    {
        HbRuntime.QOut("a=" + test63a_Render63("hello"));
        return;
    }

    public static string test63a_Render63(string cText = default)
    {
        return "<" + cText + ">";
    }
}
