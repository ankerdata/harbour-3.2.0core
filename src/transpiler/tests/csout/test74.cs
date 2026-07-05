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
// The legs are built so the type can come ONLY from the name: no
// declaration initializer, and every value arrives through SeedObject,
// whose mixed RETURN types pin its inferred return type to USUAL — so
// neither the initializer inference nor assignment propagation ever
// sees a `Test74Holder():New()`. This is what separates the feature
// from the pre-existing constructor-initializer typing.
//
//   • LOCAL oTest74Holder       → typed Test74Holder purely by name
//   • STATIC soTest74Holder     → file-static, same inference via the
//                                  easipos `so<ClassName>` STATIC form
//   • LOCAL oNotAClass          → suffix matches no class; falls
//                                  through to OBJECT / dynamic
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
        Test74Holder oTest74Holder = default;
        dynamic oNotAClass = default;

        oTest74Holder = SeedObject(true);
        oNotAClass = SeedObject(true);
        test74_soTest74Holder = SeedObject(true);

        oTest74Holder.cTag = "first";
        test74_soTest74Holder.cTag = "static";
        oNotAClass.cTag = "fallback";

        HbRuntime.QOut("local:  " + oTest74Holder.Identify());
        HbRuntime.QOut("static: " + test74_soTest74Holder.Identify());
        HbRuntime.QOut("fallthrough: " + oNotAClass.Identify());
        return;

        // Polymorphic on purpose: the mixed RETURN types (object vs string)
        // make the inferred return type USUAL, so no constructor evidence
        // reaches the call sites above.
    }
    public static dynamic SeedObject(bool lReal = default)
    {
        if (lReal)
        {
            return new Test74Holder();
        }

        return "never";
    }
}
