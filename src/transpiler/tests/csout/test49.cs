using System;
using System.Collections.Generic;
using static HbRuntime;
using static Program;

// Test 49: Super / className handling.
//
// Covers all four forms the transpiler maps:
//   1. obj:className()        → HbRuntime.CLASSNAME(obj)
//   2. obj:Super():className() → HbRuntime.CLASSNAME(obj.Super())  (the HbSuperRef: parent class)
//   3. ::Super:className()    → HbRuntime.CLASSNAME(this.Super()) (same as 2, colon form)
//   4. ::Super:Method(args)   → base.Method(args)          (inheritance call)
//
// The last form is the important one — Harbour's idiomatic "call the
// parent class's implementation" that C# spells as `base.Method(...)`.

// #include "hbclass.ch"
public class Animal
{

    public dynamic Kind() => "animal";
    public virtual Animal Speak()
    {
        HbRuntime.QOut("Animal::Speak");
        return this;
    }
}

public class Dog : Animal
{

    public override Dog Speak()
    {
        // form 4: → base.Speak()
        base.Speak();
        HbRuntime.QOut("Dog::Speak");
        return this;
    }

    public virtual Dog Identify()
    {
        // form 1
        HbRuntime.QOut("className=" + HbRuntime.Upper(HbRuntime.CLASSNAME(this)));
        // form 3
        HbRuntime.QOut("Super-className=" + HbRuntime.Upper(HbRuntime.CLASSNAME(this.Super())));
        // form 4 (inline parent method)
        HbRuntime.QOut("Kind=" + base.Kind());
        return this;
    }
}

public static partial class Program
{
    public static void Main(string[] args)
    {
        Dog oDog = new Dog();
        Animal oAnimal = new Animal();

        // form 1
        HbRuntime.QOut("oAnimal:className()=" + HbRuntime.Upper(HbRuntime.CLASSNAME(oAnimal)));
        // form 1
        HbRuntime.QOut("oDog:className()=" + HbRuntime.Upper(HbRuntime.CLASSNAME(oDog)));
        // form 2
        HbRuntime.QOut("oDog:Super():className()=" + HbRuntime.Upper(HbRuntime.CLASSNAME(oDog.Super())));

        oDog.Speak();
        oDog.Identify();

        return;
    }
}
