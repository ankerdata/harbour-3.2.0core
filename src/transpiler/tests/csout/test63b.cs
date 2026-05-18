using System;
using static HbRuntime;
using static Program;

// Test 63b: the global Render63() that collides by name with
// test63a's file-static one. Its parameter is numeric — a
// deliberately different signature, so that if test63a's static
// leaked into this entry (or vice versa) the mistype would show.
public static partial class Program
{
    public static void Main(string[] args)
    {
        HbRuntime.QOut("b=" + Render63(42));
        return;
    }

    public static string Render63(decimal nValue = default)
    {
        return "#" + HbRuntime.LTrim(HbRuntime.Str(nValue));
    }
}
