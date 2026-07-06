using System;
using System.Collections.Generic;
using static HbRuntime;
using static Program;

// Test 79: INHERIT-aware type relations in the reftab.
//
// The reftab records INHERIT edges (I=<Parent> tail rows), and the
// type machinery uses them two ways:
//   - a subclass argument satisfies a superclass-typed parameter slot
//     (Describe below receives Animal AND Dog with no W0022 conflict
//     and no downgrade-to-USUAL — the slot stays Animal);
//   - RETURN statements yielding different but related classes merge
//     to their nearest common ancestor instead of degrading (Pick
//     returns Dog on one path and Cat on the other; its C# return
//     type must be Animal for both branches to compile).
// Runtime behaviour is checked across prg/hb/cs as usual.

// #include "hbclass.ch"
public class Animal
{
    public string cName = "animal";
    public string cSound = "...";

}

public class Dog : Animal
{

}

public class Cat : Animal
{

}

public static partial class Program
{
    public static void Main(string[] args)
    {
        Animal oAnimal = new Animal();
        Dog oDog = new Dog();
        Animal oPet = default;

        oDog.cName = "rex";
        oDog.cSound = "woof";

        // Same parameter slot fed both the base and the subclass.
        HbRuntime.QOut("a=", Describe(oAnimal));
        HbRuntime.QOut("b=", Describe(oDog));

        // Mixed-class returns: Pick's inferred return type is Animal.
        oPet = Pick(true);
        oPet.cSound = "WOOF";
        HbRuntime.QOut("c=", Describe(oPet));
        oPet = Pick(false);
        HbRuntime.QOut("d=", Describe(oPet));

        return;
    }

    public static string Describe(dynamic oAnimal = default)
    {
        return HbRuntime.RTrim(oAnimal.cName) + " says " + HbRuntime.RTrim(oAnimal.cSound);

        // Two RETURN paths, two sibling classes — merges to Animal via the
        // INHERIT chain rather than conflicting to USUAL.
    }
    public static Animal Pick(bool lDog = default)
    {
        if (lDog)
        {
            return new Dog();
        }

        return new Cat();
    }
}
