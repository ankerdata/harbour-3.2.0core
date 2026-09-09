using System;
using System.Collections.Generic;
using static HbRuntime;
using static Program;

// Test 93: a `Class():New(...)` call site refines the constructor's slots.
//
// The refinement walker types a send's receiver to pick the method row
// its arguments refine. A receiver that is the class constructor call
// itself — `Sconce():New(oLantern, "hall")` — is a FUNCALL no return-
// type table knows (a CLASS is not a function row), so the receiver
// stayed untyped and no constructor was ever refined from its callers:
// in the easipos corpus 0 of 18 Dialog `New` methods had a typed slot
// while 804 free-function slots were typed Transaction. The walker now
// types a `Class()` receiver as the class (the emitter already resolved
// the shape, test90), so oLight below is `Lantern` in C# and the member
// access in the constructor binds statically instead of through
// `dynamic`.
// #include "hbclass.ch"
/* oLight is OBJECT by name (no class named Light); Main's call refines
   it to Lantern, so `oLight:nLumens` is a typed member access. */
public class Lantern
{
    public decimal nLumens = 800;

}

public class Sconce
{
    public dynamic oLight;
    public string cLabel = "";

    public dynamic New(Lantern oLight = default, string cRoom = default)
    {
        this.oLight = oLight;
        this.cLabel = cRoom + "/" + HbRuntime.LTrim(HbRuntime.Str(oLight.nLumens));
        return this;
    }
}

public static partial class Program
{
    public static void Main(string[] args)
    {
        Lantern oLantern = new Lantern();
        Sconce oSconce = (Sconce)new Sconce().New(oLantern, "hall");
        HbRuntime.QOut("label=" + oSconce.cLabel);
        return;
    }
}
