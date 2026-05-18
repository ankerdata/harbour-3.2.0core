using System;
using static HbRuntime;
using static Program;

// Test 62: a file-scoped STATIC function called with an omitted
// middle argument.
//
// A STATIC function is keyed <File>::<Name> in the reftab. The call
// site carries only the bare name, so the parameter lookup that
// drives the emitter's named-argument rewrite — used after a `, ,`
// gap — missed. The emitter then fell back to positional emission
// and dropped the gap slot, shifting every following argument left
// by one. That mis-bound the arguments (surfacing as CS1503).
//
// Tag() is STATIC and called as Tag("hi", , 7) with the middle
// argument omitted. The emitter must keep the gap so nKind lands in
// parameter 3, not parameter 2.
public static partial class Program
{
    public static void Main(string[] args)
    {
        HbRuntime.QOut("gap=" + test62_Tag("hi", nKind: 7));
        HbRuntime.QOut("full=" + test62_Tag("yo", "MID", 3));
        return;
    }

    public static string test62_Tag(string cName = default, string cMiddle = default, decimal nKind = default)
    {
        string cMid = (cMiddle == null ? "-" : cMiddle);
        return cName + "/" + cMid + "/" + HbRuntime.LTrim(HbRuntime.Str(nKind));
    }
}
