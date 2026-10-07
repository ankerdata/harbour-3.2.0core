using System;
using static HbRuntime;
using static Program;

// Test 146: an argument left out of a by-reference slot is NIL every time.
//
// A parameter some caller passes with @ is `ref` in C# for every caller, so a
// caller that leaves it out before an argument it does pass gives
// `ref HbDiscard<T>.Value`. That was one shared field, and a callee that reads
// its parameter before writing it read whatever the last call through the
// slot had written back: EasiPOS's Plu(), whose `DEFAULT nRFOrVoidIndex TO 0`
// is NIL's reading, took a line index an earlier call had left and stopped on
// a bound error.
public static partial class Program
{
    public static decimal test146_Bump146(ref decimal nCount, decimal nStep = default)
    {
        nCount += nStep;
        return nCount;
    }

    public static dynamic test146_Bump146()
    {
        decimal _arg0 = 0;
        decimal _arg1 = default;
        return test146_Bump146(ref _arg0, _arg1);
    }

    public static string test146_Tag146(ref string cTag, string cAdd = default)
    {
        HbRuntime.hb_default(ref cTag, "");
        cTag += cAdd;
        return cTag;
    }

    public static dynamic test146_Tag146()
    {
        string _arg0 = default;
        string _arg1 = default;
        return test146_Tag146(ref _arg0, _arg1);
    }

    public static void Main(string[] args)
    {
        decimal nMine146 = 5;
        string cMine146 = "a";

        HbRuntime.QOut("number, with @:", test146_Bump146(ref nMine146, 10), nMine146);
        HbRuntime.QOut("number, left out:", test146_Bump146(ref HbDiscard<decimal>.Value, 10));
        HbRuntime.QOut("number, left out again:", test146_Bump146(ref HbDiscard<decimal>.Value, 10));

        HbRuntime.QOut("string, with @:", test146_Tag146(ref cMine146, "x"), cMine146);
        HbRuntime.QOut("string, left out:", test146_Tag146(ref HbDiscard<string>.Value, "x"));
        HbRuntime.QOut("string, left out again:", test146_Tag146(ref HbDiscard<string>.Value, "x"));

        return;
    }
}
