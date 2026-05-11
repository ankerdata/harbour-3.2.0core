using System;
using System.Collections.Generic;
using static HbRuntime;
using static Program;

// Test 49: Super / className handling.
//
// Covers all four forms the transpiler maps:
//   1. obj:className()        → obj.className()            (extension on object)
//   2. obj:Super():className() → obj.Super().className()    (HbSuperRef.className)
//   3. ::Super:className()    → this.Super().className()   (same as 2, colon form)
//   4. ::Super:Method(args)   → base.Method(args)          (inheritance call)
//
// The last form is the important one — Harbour's idiomatic "call the
// parent class's implementation" that C# spells as `base.Method(...)`.

// #include "hbclass.ch"
public class Animal
{

    public dynamic Kind() => "animal";
    public dynamic Speak()
    {
        HbRuntime.QOut("Animal::Speak");
        return this;
    }
}

public class Dog : Animal
{

    public dynamic Speak()
    {
        // form 4: → base.Speak()
        base.Speak();
        HbRuntime.QOut("Dog::Speak");
        return this;
    }

    public dynamic Identify()
    {
        // form 1
        HbRuntime.QOut("className=" + HbRuntime.Upper(this.className()));
        // form 3
        HbRuntime.QOut("Super-className=" + HbRuntime.Upper(this.Super().className()));
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
        HbRuntime.QOut("oAnimal:className()=" + HbRuntime.Upper(oAnimal.className()));
        // form 1
        HbRuntime.QOut("oDog:className()=" + HbRuntime.Upper(oDog.className()));
        // form 2
        HbRuntime.QOut("oDog:Super():className()=" + HbRuntime.Upper(oDog.Super().className()));

        oDog.Speak();
        oDog.Identify();

        return;
    }
}
