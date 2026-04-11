using System;
using System.Collections.Generic;

// Test 14: Two classes with inheritance, constructor params, method chaining

// #include "hbclass.ch"
public class Animal
{
    public string cName { get; set; } = "";
    public decimal nLegs { get; set; } = 4;
    public string cSound { get; set; } = "";

    public object New(string cName = default, string cSound = default)
    {
        this.cName = cName;
        HbRuntime.QOUT("cName=" + this.cName);
        this.cSound = cSound;
        HbRuntime.QOUT("cSound=" + this.cSound);
        return this;
    }

    public string Speak()
    {
        return this.cName + " says: " + this.cSound;
    }

    public string Describe()
    {
        return this.cName + " has " + HbRuntime.STR(this.nLegs) + " legs";
    }
}

public class Dog : Animal
{
    public string cBreed { get; set; } = "";

    public object Init(string cName = default, string cBreed = default)
    {
        this.cName = cName;
        HbRuntime.QOUT("cName=" + this.cName);
        this.cBreed = cBreed;
        HbRuntime.QOUT("cBreed=" + this.cBreed);
        return this;
    }

    public dynamic DescribeFull()
    {
        return this.Describe() + " (" + this.cBreed + ")";
    }
}

public static partial class Program
{
    public static void Main(string[] args)
    {
        Animal oAnimal = (Animal)new Animal().New("Cat", "Meow");
        Dog oDog = (Dog)new Dog().Init("Rex", "Labrador");

        HbRuntime.QOUT("Speak=" + oAnimal.Speak());
        HbRuntime.QOUT("Describe=" + oAnimal.Describe());
        HbRuntime.QOUT("Speak=" + oDog.Speak());
        HbRuntime.QOUT("DescribeFull=" + oDog.DescribeFull());

        return;
    }
}
