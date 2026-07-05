using System;
using static HbRuntime;
using static Program;

// Test 74: `o<ClassName>` (and easipos STATIC `so<ClassName>`) infer
// the variable as that specific class, not generic OBJECT.
//
// Prior to the prefix-from-name inference, a local declared
//
//    LOCAL oFooThing
//
// was seeded as OBJECT (Hungarian `o`) and emitted as C# `dynamic` —
// member access went through the DLR, no static-type benefit. With
// the inference, when the post-`o` suffix matches a class registered
// in the reftab, the local takes that class as its static type —
// authors get the static C# binding for free, no `:= FooThing():New()`
// seed allocation needed.
//
// This test exercises three shapes:
//   • LOCAL oTest74Holder       → typed Test74Holder
//   • STATIC soTest74Holder     → file-static, same inference via the
//                                  easipos `so<ClassName>` STATIC form
//   • LOCAL oNotAClass          → falls through to OBJECT / dynamic,
//                                  the existing behaviour
//
// All three pipelines (.prg / .hb / .cs) must produce identical
// output. The .cs side is where the inference matters — the .prg /
// .hb pipelines are dynamic-typed throughout and run regardless.

// #include "hbclass.ch"
public class Test74Holder
{
    public string cTag = "default";

    public string Identify()
    {
        return "Test74Holder:" + this.cTag;
    }
}

public static partial class Program
{
    public static Test74Holder test74_soTest74Holder;
    public static void Main(string[] args)
    {
        Test74Holder oTest74Holder = new Test74Holder();
        // o-prefix but suffix isn't a class
        Test74Holder oNotAClass = new Test74Holder();

        test74_soTest74Holder = new Test74Holder();

        oTest74Holder.cTag = "first";
        test74_soTest74Holder.cTag = "static";
        oNotAClass.cTag = "fallback";

        HbRuntime.QOut("local:  " + oTest74Holder.Identify());
        HbRuntime.QOut("static: " + test74_soTest74Holder.Identify());
        HbRuntime.QOut("fallthrough: " + oNotAClass.Identify());
        return;
    }
}
